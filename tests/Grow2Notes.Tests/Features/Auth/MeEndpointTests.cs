using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Grow2Notes.Tests.Features.Auth;

/// <summary>
/// <c>GET /api/auth/me</c> (design.md §6.2), called with the session cookie alone after the cookie sign-in
/// (<see cref="CookieSignIn"/>), on a fake clock (<see cref="TestClock"/>) that moves a second before the call, as a
/// later request of the session: the user's own ID, display name and role, their organisation's name, and the date in
/// Melbourne, as JSON in camelCase that is never stored (§6.1). The endpoint matrix covers who may call it, and
/// <see cref="SignedOutTests"/> what a signed-out caller gets.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class MeEndpointTests : IClassFixture<Grow2NotesFactory>, IAsyncDisposable
{
    private const string Path = "/api/auth/me";

    private readonly FakeTimeProvider clock = new(TestClock.Start);
    private readonly SeededOrganisations seeded;
    private readonly WebApplicationFactory<Program> app;

    public MeEndpointTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    {
        seeded = sqlServer.Seeded;
        app = factory.WithWebHostBuilder(builder => builder.MapCookieSignIn().UseClock(clock));
    }

    [Fact]
    public async Task Answers_the_user_s_own_ID_display_name_and_role_and_their_organisation_s_name()
    {
        var (a, b) = seeded;

        foreach (var (userId, organisationId, role) in new[]
        {
            (a.WorkerId, a.OrganisationId, "Worker"),
            (a.ManagerId, a.OrganisationId, "Manager"),
            (b.ManagerId, b.OrganisationId, "Manager"),
        })
        {
            var (displayName, organisationName) = await NamesFromTheDatabaseAsync(userId, organisationId);

            var me = await GetMeAsync(userId);

            Assert.Equal(userId, me.GetProperty("userId").GetGuid());
            Assert.Equal(displayName, me.GetProperty("displayName").GetString());
            Assert.Equal(role, me.GetProperty("role").GetString());
            Assert.Equal(organisationName, me.GetProperty("organisationName").GetString());
        }
    }

    // The clock starts at 13:30 UTC on 9 October, which is half past midnight on 10 October in Melbourne, in daylight
    // saving time (UTC+11).
    [Fact]
    public async Task Today_is_the_date_in_Melbourne_where_it_differs_from_the_date_in_UTC()
    {
        var me = await GetMeAsync(seeded.A.WorkerId);

        Assert.Equal(new DateOnly(2026, 10, 9), DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime));
        Assert.Equal("2026-10-10", me.GetProperty("today").GetString());
    }

    // design.md §6.2's five, in its order; E03 adds toReviewCount, for managers only.
    [Fact]
    public async Task A_manager_s_answer_has_the_five_properties_and_no_toReviewCount()
    {
        var me = await GetMeAsync(seeded.A.ManagerId);

        Assert.Equal(
            ["userId", "displayName", "role", "organisationName", "today"],
            me.EnumerateObject().Select(property => property.Name));
    }

    [Fact]
    public async Task The_answer_is_never_stored()
    {
        using var client = await SignedInClientAsync(seeded.A.WorkerId);

        using var response = await client.GetAsync(Path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
    }

    public ValueTask DisposeAsync() => app.DisposeAsync();

    // Signs a new client in with the cookie, then moves the clock, so that its next request is a later one of the
    // session, which the session check rebuilds the principal for (tests/README.md).
    private async Task<HttpClient> SignedInClientAsync(Guid userId)
    {
        var client = app.CreateHttpsClient();
        await client.SignInWithCookieAsync(userId);
        clock.Advance(TimeSpan.FromSeconds(1));
        return client;
    }

    private async Task<JsonElement> GetMeAsync(Guid userId)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = await SignedInClientAsync(userId);

        using var response = await client.GetAsync(Path, cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
    }

    // As the rows hold them, read through the app's own services. An organisation is the tenant, not tenant-owned, so
    // reading one needs no tenant.
    private async Task<(string DisplayName, string OrganisationName)> NamesFromTheDatabaseAsync(
        Guid userId, Guid organisationId)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var user = await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>()
            .FindByIdAsync(userId.ToString());
        Assert.NotNull(user);

        var organisation = await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Organisations
            .SingleAsync(o => o.Id == organisationId, TestContext.Current.CancellationToken);
        return (user.DisplayName, organisation.Name);
    }
}
