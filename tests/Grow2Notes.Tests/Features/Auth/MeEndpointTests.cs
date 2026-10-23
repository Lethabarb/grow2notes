using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Features.Auth;
using Grow2Notes.Web.Platform;
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
/// Melbourne, as JSON in camelCase that is never stored (§6.1); for a manager, the number of the organisation's notes
/// in To review, which the tests count in an organisation of their own (<see cref="TestOrganisations"/>), as other
/// tests flag notes in the seeded ones; for a worker, no such property. The endpoint matrix covers who may call it, and
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

    // design.md §6.2's five, in its order, and toReviewCount for a manager: the organisation's notes in To review, two,
    // and not the reviewed note, the unflagged one, the draft, the discarded draft or B's flagged note.
    [Fact]
    public async Task A_manager_s_answer_has_six_properties_and_toReviewCount_counts_only_their_notes_in_To_review()
    {
        var organisation = await AddOrganisationWithNotesAsync();

        var me = await GetMeAsync(organisation.ManagerId);

        Assert.Equal(
            ["userId", "displayName", "role", "organisationName", "today", "toReviewCount"],
            me.EnumerateObject().Select(property => property.Name));
        Assert.Equal(2, me.GetProperty("toReviewCount").GetInt32());
    }

    // 0 is sent, not left out: only a worker has no count.
    [Fact]
    public async Task A_manager_with_no_note_in_To_review_gets_toReviewCount_0()
    {
        var organisation = await app.Services.AddOrganisationAsync();

        var me = await GetMeAsync(organisation.ManagerId);

        Assert.Equal(0, me.GetProperty("toReviewCount").GetInt32());
    }

    // The Flagged link's count and the To review list agree, as only a submitted note has a flag status and a note in
    // To review always has a flagged version.
    [Fact]
    public async Task The_count_equals_the_length_of_the_To_review_list()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var organisation = await AddOrganisationWithNotesAsync();
        using var client = app.CreateHttpsClient().SignInAs(organisation.ManagerId);

        var list = await client.GetFromJsonAsync<JsonElement>("/api/reviews?status=toReview", cancellationToken);
        var me = await GetMeAsync(organisation.ManagerId);

        Assert.Equal(list.GetArrayLength(), me.GetProperty("toReviewCount").GetInt32());
    }

    [Fact]
    public async Task A_worker_s_answer_has_the_five_properties_and_no_toReviewCount()
    {
        var organisation = await AddOrganisationWithNotesAsync();

        var me = await GetMeAsync(organisation.WorkerId);

        Assert.Equal(
            ["userId", "displayName", "role", "organisationName", "today"],
            me.EnumerateObject().Select(property => property.Name));
    }

    // Setup and the sign-in endpoints answer with OfUserAsync, inside the user's tenant as they do, so the count is the
    // same there as in this endpoint's answer.
    [Fact]
    public async Task The_sign_in_and_setup_answer_carries_the_count_for_a_manager_and_none_for_a_worker()
    {
        var organisation = await AddOrganisationWithNotesAsync();

        Assert.Equal(2, (await OfUserAsync(organisation, organisation.ManagerId)).ToReviewCount);
        Assert.Null((await OfUserAsync(organisation, organisation.WorkerId)).ToReviewCount);
    }

    // The status is compared with a literal, so the query is a seek on the filtered index (OrganisationId, FlagStatus)
    // WHERE FlagStatus = 1, which a parameter would not match. Writing the SQL opens no connection.
    [Fact]
    public async Task The_notes_in_To_review_compare_FlagStatus_with_the_literal_1()
    {
        await using var noDatabase = new AppWithoutDatabase("Production");
        await using var scope = noDatabase.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        string sql;

        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(Guid.NewGuid()))
        {
            sql = MeEndpoint.ToReviewNotes(db).ToQueryString();
        }

        Assert.Matches(@"\[FlagStatus\] = (CAST\()?1\b", sql);
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

    // An organisation of its own with two notes in To review, and, none of them counted, a reviewed note, an unflagged
    // one, a draft, a discarded draft, and a flagged note in the seeded organisation B.
    private async Task<SeededOrganisation> AddOrganisationWithNotesAsync()
    {
        var services = app.Services;
        var organisation = await services.AddOrganisationAsync();
        var noteDate = new DateOnly(2026, 10, 2);
        var startedAtUtc = new DateTime(2026, 10, 1, 23, 14, 0, DateTimeKind.Utc);
        var submittedAtUtc = new DateTime(2026, 10, 2, 6, 42, 0, DateTimeKind.Utc);

        for (var i = 0; i < 2; i++)
        {
            await services.AddSubmittedAsync(
                organisation, Submitted((await services.AddParticipantAsync(organisation)).Id, organisation));
        }

        await services.AddSubmittedAsync(
            organisation,
            Submitted((await services.AddParticipantAsync(organisation)).Id, organisation, FlagStatus.Reviewed));
        await services.AddSubmittedAsync(
            organisation,
            Submitted((await services.AddParticipantAsync(organisation)).Id, organisation, FlagStatus.None));
        await services.AddDraftAsync(
            organisation,
            new((await services.AddParticipantAsync(organisation)).Id, noteDate, organisation.WorkerId, startedAtUtc));
        var discarded = await services.AddDraftAsync(
            organisation,
            new((await services.AddParticipantAsync(organisation)).Id, noteDate, organisation.WorkerId, startedAtUtc));
        await services.DiscardAsync(organisation, discarded, submittedAtUtc);
        await services.AddSubmittedAsync(
            seeded.B, Submitted((await services.AddParticipantAsync(seeded.B)).Id, seeded.B));
        return organisation;

        // Flagged, so in To review, unless the status given says otherwise: None is unflagged.
        TestSubmittedNote Submitted(Guid participantId, SeededOrganisation owner, FlagStatus? flagStatus = null) =>
            new(
                participantId,
                noteDate,
                owner.WorkerId,
                startedAtUtc,
                submittedAtUtc,
                IsFlagged: flagStatus != FlagStatus.None,
                FlagReason: flagStatus == FlagStatus.None ? null : "Jane seemed unwell.",
                FlagStatus: flagStatus);
    }

    // What the sign-in and setup endpoints answer with, for the user of the ID given, read inside their tenant as those
    // endpoints do.
    private async Task<Me> OfUserAsync(SeededOrganisation organisation, Guid userId)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var user = await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>()
            .FindByIdAsync(userId.ToString());
        Assert.NotNull(user);

        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(organisation.OrganisationId))
        {
            return await MeEndpoint.OfUserAsync(
                user,
                scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>(),
                scope.ServiceProvider.GetRequiredService<MelbourneClock>(),
                TestContext.Current.CancellationToken);
        }
    }

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
