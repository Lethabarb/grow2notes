using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Grow2Notes.Tests.Features.Auth;

/// <summary>
/// A change to a user's row applies on their session's next request (design.md §8.4, §8.6), because the session check
/// compares the security stamp and rebuilds the principal from the row on every request: a rotated stamp, as
/// deactivation and a sign-in reset rotate it, ends the session, and a role change or a new display name, which leave
/// the stamp as it is, show in the rebuilt principal. Each test signs in with the cookie sign-in
/// (<see cref="CookieSignIn"/>) on a fake clock (<see cref="TestClock"/>) that moves a second before every request, so
/// the check runs on each, and changes a user it adds to seeded organisation A, as tests/README.md's seeded-rows rule
/// asks.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class NextRequestTests : IClassFixture<Grow2NotesFactory>, IAsyncDisposable
{
    private const string MePath = "/api/auth/me";

    private const string ManagerPolicyPath = "/api/test-only/manager-policy";

    private readonly FakeTimeProvider clock = new(TestClock.Start);
    private readonly SeededOrganisation a;
    private readonly WebApplicationFactory<Program> app;

    public NextRequestTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    {
        a = sqlServer.Seeded.A;
        app = factory.WithWebHostBuilder(builder => builder
            .MapCookieSignIn()
            .UseClock(clock)
            .ConfigureServices(services => services.AddSingleton<IStartupFilter>(new ManagerPolicyEndpoint())));
    }

    [Fact]
    public async Task After_the_security_stamp_changes_the_next_request_gets_401_and_the_session_cookie_is_cleared()
    {
        var userId = await AddUserToAAsync(UserRole.Worker, "Sam Taylor");
        using var client = app.CreateHttpsClient();
        await client.SignInWithCookieAsync(userId);
        // Let in until the stamp changes.
        await MeAsync(client);

        // As deactivation and a sign-in reset rotate it (design.md §8.6).
        await ChangeUserAsync(userId, (users, user) => users.UpdateSecurityStampAsync(user));
        using var response = await NextRequestAsync(client, MePath);

        await response.ReadProblemAsync(HttpStatusCode.Unauthorized);
        response.AssertClearsTheSessionCookie();
    }

    // The stamp is left as it is, so only the role in the principal rebuilt from the row can refuse the request, and
    // the session goes on, as the worker the user now is.
    [Fact]
    public async Task A_manager_made_a_worker_gets_403_from_a_manager_endpoint_on_the_next_request_and_stays_signed_in()
    {
        var userId = await AddUserToAAsync(UserRole.Manager, "Alex Morgan");
        using var client = app.CreateHttpsClient();
        await client.SignInWithCookieAsync(userId);
        using var asManager = await NextRequestAsync(client, ManagerPolicyPath);
        Assert.Equal(HttpStatusCode.OK, asManager.StatusCode);
        var stamp = (await LoadUserAsync(userId)).SecurityStamp;

        await ChangeUserAsync(userId, (users, user) =>
        {
            user.Role = UserRole.Worker;
            return users.UpdateAsync(user);
        });
        var changed = await LoadUserAsync(userId);
        Assert.Equal(UserRole.Worker, changed.Role);
        Assert.Equal(stamp, changed.SecurityStamp);
        using var asWorker = await NextRequestAsync(client, ManagerPolicyPath);

        await asWorker.ReadProblemAsync(HttpStatusCode.Forbidden);
        Assert.Equal("Worker", (await MeAsync(client)).GetProperty("role").GetString());
    }

    [Fact]
    public async Task A_changed_display_name_shows_in_the_next_api_auth_me()
    {
        var userId = await AddUserToAAsync(UserRole.Worker, "Jamie Park");
        using var client = app.CreateHttpsClient();
        await client.SignInWithCookieAsync(userId);
        Assert.Equal("Jamie Park", (await MeAsync(client)).GetProperty("displayName").GetString());

        await ChangeUserAsync(userId, (users, user) =>
        {
            user.DisplayName = "Jamie Park-Lee";
            return users.UpdateAsync(user);
        });

        Assert.Equal("Jamie Park-Lee", (await MeAsync(client)).GetProperty("displayName").GetString());
    }

    public ValueTask DisposeAsync() => app.DisposeAsync();

    private static void AssertSucceeded(IdentityResult result) =>
        Assert.True(result.Succeeded, string.Join(" ", result.Errors.Select(error => error.Code)));

    // A second after the last request, so the session check runs on this one (tests/README.md).
    private async Task<HttpResponseMessage> NextRequestAsync(HttpClient client, string path)
    {
        clock.Advance(TimeSpan.FromSeconds(1));
        return await client.GetAsync(path, TestContext.Current.CancellationToken);
    }

    private async Task<JsonElement> MeAsync(HttpClient client)
    {
        using var response = await NextRequestAsync(client, MePath);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
    }

    // An Active user of A, as setup leaves one (design.md §8.1) apart from the passkey or password, which the cookie
    // sign-in does not need.
    private async Task<Guid> AddUserToAAsync(UserRole role, string displayName)
    {
        // An address of its own, which no other test or run of this one uses.
        var email = $"{Guid.NewGuid():N}@example.org";
        var now = clock.GetUtcNow().UtcDateTime;
        var user = new ApplicationUser
        {
            OrganisationId = a.OrganisationId,
            DisplayName = displayName,
            Role = role,
            Status = UserStatus.Active,
            InvitedAtUtc = now,
            InvitedByUserId = a.ManagerId,
            ActivatedAtUtc = now,
            UserName = email,
            Email = email,
            EmailConfirmed = true,
        };
        await using var scope = app.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        AssertSucceeded(await users.CreateAsync(user));
        return user.Id;
    }

    // Through the app's UserManager, on the user as loaded afresh, as the app changes a user.
    private async Task ChangeUserAsync(
        Guid userId, Func<UserManager<ApplicationUser>, ApplicationUser, Task<IdentityResult>> change)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByIdAsync(userId.ToString());
        Assert.NotNull(user);
        AssertSucceeded(await change(users, user));
    }

    // As the row holds the user now.
    private async Task<ApplicationUser> LoadUserAsync(Guid userId)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var user = await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>()
            .FindByIdAsync(userId.ToString());
        Assert.NotNull(user);
        return user;
    }

    /// <summary>
    /// Maps an endpoint with the <see cref="Policies.Manager"/> policy, at <see cref="ManagerPolicyPath"/>, which
    /// answers <c>200</c> to a manager. It is outside the <c>/api</c> group, so the policy is all that stands before
    /// it. It is routed ahead of the app's own routing, which then leaves the endpoint already chosen, so it runs where
    /// the app's own endpoints do, after the app's authorization.
    /// </summary>
    private sealed class ManagerPolicyEndpoint : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.UseRouting();
            next(app);
            app.UseEndpoints(endpoints => endpoints.MapGet(ManagerPolicyPath, () => TypedResults.Ok())
                .RequireAuthorization(Policies.Manager));
        };
    }
}
