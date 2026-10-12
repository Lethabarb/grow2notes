using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform.Audit;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Grow2Notes.Tests.Features.Auth;

/// <summary>
/// <c>POST /api/auth/setup/passkey</c> (design.md §6.2, §8.1 steps 4 and 5), called as the SPA calls it: signed out,
/// over HTTPS, after <c>POST /api/auth/setup/start</c> with a link of the test's own (<see cref="SetupLinks"/>) and
/// <c>POST /api/auth/setup/passkey/options</c>, with the one antiforgery token that the SPA fetches before the start,
/// and with the credential and the name that the SPA sends. The app is hosted on a fake clock with the test-only
/// passkey handler (<see cref="TestPasskeyHandler"/>), and gives each request a client address, which the test server
/// does not. The endpoint matrix covers callers with no enrolment cookie, and
/// <see cref="SetupPasskeyOptionsEndpointTests"/> the enrolment check that every setup step after the start makes.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class SetupPasskeyEndpointTests : IClassFixture<Grow2NotesFactory>, IAsyncDisposable
{
    private const string Path = "/api/auth/setup/passkey";

    private const string OptionsPath = "/api/auth/setup/passkey/options";

    // A documentation address (RFC 5737), outside App Service's front-end networks, so the app keeps it as the
    // client's.
    private const string ClientAddress = "203.0.113.7";

    private readonly FakeTimeProvider clock = new(TestClock.Start);
    private readonly Grow2NotesFactory factory;
    private readonly SeededOrganisations seeded;
    private readonly WebApplicationFactory<Program> app;

    public SetupPasskeyEndpointTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    {
        this.factory = factory;
        seeded = sqlServer.Seeded;
        app = factory.WithWebHostBuilder(builder => builder
            .UseClock(clock)
            .UseTestPasskeyHandler()
            .ConfigureServices(services => services.AddSingleton<IStartupFilter>(new FromClientAddress())));
    }

    // The clock starts at 13:30 UTC on 9 October, which is half past midnight on 10 October in Melbourne.
    [Fact]
    public async Task Completion_answers_the_user_s_Me_and_signs_them_in_with_the_session_cookie()
    {
        var link = await InviteAsync();
        using var client = app.CreateHttpsClient();
        var token = await StartWithOptionsAsync(client, link);

        using var response = await CompleteAsync(client, token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        var me = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(
            ["userId", "displayName", "role", "organisationName", "today"],
            me.EnumerateObject().Select(property => property.Name));
        Assert.Equal(link.UserId, me.GetProperty("userId").GetString());
        Assert.Equal(SetupLinks.DisplayName, me.GetProperty("displayName").GetString());
        Assert.Equal("Worker", me.GetProperty("role").GetString());
        Assert.Equal(
            await OrganisationNameAsync(seeded.A.OrganisationId), me.GetProperty("organisationName").GetString());
        Assert.Equal("2026-10-10", me.GetProperty("today").GetString());
        Assert.Contains(response.SetCookies(), cookie => cookie.Name.Value == CookieSignIn.SessionCookie);

        // A second later, so that the session check compares the cookie's security stamp with the row's, which
        // completion rotated (tests/README.md).
        clock.Advance(TimeSpan.FromSeconds(1));
        using var meAgain = await client.GetAsync("/api/auth/me", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, meAgain.StatusCode);
        var session = await meAgain.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(link.UserId, session.GetProperty("userId").GetString());
    }

    // The person takes two minutes at the passkey prompt, so the activation is completion's time, not the start's.
    [Fact]
    public async Task Completion_stores_the_passkey_activates_the_user_rotates_the_stamp_and_writes_the_event()
    {
        var link = await InviteAsync();
        var stamp = await factory.SecurityStampAsync(link.UserId);
        using var client = app.CreateHttpsClient();
        var token = await StartWithOptionsAsync(client, link);
        clock.Advance(TimeSpan.FromMinutes(2));

        using (var response = await CompleteAsync(client, token))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        var (user, passkeys, events) = await ReadAsync(link.UserId);
        var completedAtUtc = TestClock.Start.AddMinutes(2).UtcDateTime;
        Assert.Equal(UserStatus.Active, user.Status);
        Assert.Equal(completedAtUtc, user.ActivatedAtUtc);
        Assert.True(user.EmailConfirmed);
        Assert.NotEqual(stamp, user.SecurityStamp);
        Assert.Equal("Grow2Notes", Assert.Single(passkeys).Name);
        Assert.Equal(
            new StoredEvent(
                seeded.A.OrganisationId, completedAtUtc, user.Id, AuditEventTypes.Auth.SetupCompleted,
                AuditEntityTypes.User, user.Id, ParticipantId: null, Details: null, ClientAddress),
            Assert.Single(events));
    }

    // The rotated stamp ends the link (the fourth criterion), started afresh, as from another tab.
    [Fact]
    public async Task After_completion_the_same_link_gets_410()
    {
        var link = await InviteAsync();
        using (var client = app.CreateHttpsClient())
        {
            using var response = await CompleteAsync(client, await StartWithOptionsAsync(client, link));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        using var again = app.CreateHttpsClient();
        using var start = await again.PostWithTokenAsync(
            SetupLinks.StartPath, await again.FetchTokenAsync(), new { userId = link.UserId, token = link.Token });

        using var problem = JsonDocument.Parse(await start.ReadProblemAsync(HttpStatusCode.Gone));
        Assert.Equal("auth.setup_link_invalid", problem.RootElement.GetProperty("code").GetString());
    }

    // As after a double tap: the SPA fetches a token for the session that completion started, and the browser still
    // holds the enrolment cookie, whose user is now Active. Nothing is stored twice.
    [Fact]
    public async Task A_second_completion_after_one_that_succeeded_gets_401()
    {
        var link = await InviteAsync();
        using var client = app.CreateHttpsClient();
        using (var first = await CompleteAsync(client, await StartWithOptionsAsync(client, link)))
        {
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        }

        using var second = await CompleteAsync(client, await client.FetchTokenAsync());

        await second.ReadProblemAsync(HttpStatusCode.Unauthorized);
        Assert.Equal("no-store", second.Headers.CacheControl?.ToString());
        var (_, passkeys, events) = await ReadAsync(link.UserId);
        Assert.Single(passkeys);
        Assert.Single(events);
    }

    // A manager deactivates the user while the person is at the passkey prompt: after the enrolment check, before
    // completion loads the user again.
    [Fact]
    public Task A_user_deactivated_during_the_passkey_prompt_gets_401_and_stays_deactivated() =>
        AssertChangeAtThePromptWinsAsync(UserStatus.Deactivated);

    // A manager sends a new link, with a resend or a Reset sign-in, while the person is at the passkey prompt, which
    // leaves the user Invited with a new stamp: completion then finds the user Invited, but not with the stamp that the
    // enrolment check matched (A53).
    [Fact]
    public Task A_user_sent_a_new_link_during_the_passkey_prompt_gets_401_and_stays_Invited() =>
        AssertChangeAtThePromptWinsAsync(UserStatus.Invited);

    // View E sends options up to 4 minutes old to the prompt (passkey.ts), and the person takes some of the prompt's
    // time, so the ceremony that the options start must last the 5 minutes for which Identity keeps it in its
    // two-factor cookie (D69), to the second. A refusal a second later changes nothing, and new options work.
    [Fact]
    public async Task A_ceremony_completes_until_5_minutes_after_its_options_and_not_a_second_longer()
    {
        var link = await InviteAsync();
        var stamp = await factory.SecurityStampAsync(link.UserId);
        using var client = app.CreateHttpsClient();
        var token = await StartWithOptionsAsync(client, link);

        clock.Advance(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1));
        using (var aSecondLonger = await CompleteAsync(client, token))
        {
            await AssertCredentialRefusedAsync(aSecondLonger);
        }

        await AssertNothingChangedAsync(link, stamp);
        using (var options = await client.PostWithTokenAsync(OptionsPath, token))
        {
            Assert.Equal(HttpStatusCode.OK, options.StatusCode);
        }

        clock.Advance(TimeSpan.FromMinutes(5));
        using var atFiveMinutes = await CompleteAsync(client, token);

        Assert.Equal(HttpStatusCode.OK, atFiveMinutes.StatusCode);
    }

    // The built-in validation answers before the endpoint runs.
    [Fact]
    public async Task A_body_without_the_credential_gets_422_and_changes_nothing()
    {
        var link = await InviteAsync();
        var stamp = await factory.SecurityStampAsync(link.UserId);
        using var client = app.CreateHttpsClient();
        var token = await StartWithOptionsAsync(client, link);

        using var response = await client.PostWithTokenAsync(Path, token, new { name = "Grow2Notes" });

        await AssertCredentialRefusedAsync(response);
        await AssertNothingChangedAsync(link, stamp);
    }

    [Fact]
    public async Task A_credential_that_Identity_refuses_gets_422_and_changes_nothing()
    {
        var link = await InviteAsync();
        var stamp = await factory.SecurityStampAsync(link.UserId);
        using var client = app.CreateHttpsClient();
        var token = await StartWithOptionsAsync(client, link);

        using var response = await CompleteAsync(client, token, TestPasskeyHandler.RefusedCredential);

        await AssertCredentialRefusedAsync(response);
        await AssertNothingChangedAsync(link, stamp);
    }

    // One browser asks for options with one link, then starts setup with another, whose enrolment cookie replaces the
    // first's while the ceremony is still the first user's.
    [Fact]
    public async Task A_credential_made_from_another_user_s_options_gets_422_and_changes_neither_user()
    {
        var first = await InviteAsync();
        var second = await InviteAsync();
        var firstStamp = await factory.SecurityStampAsync(first.UserId);
        var secondStamp = await factory.SecurityStampAsync(second.UserId);
        using var client = app.CreateHttpsClient();
        await StartWithOptionsAsync(client, first);
        var (token, _) = await client.StartSetupAsync(second);

        using var response = await CompleteAsync(client, token);

        await AssertCredentialRefusedAsync(response);
        await AssertNothingChangedAsync(first, firstStamp);
        await AssertNothingChangedAsync(second, secondStamp);
    }

    // So no ceremony is under way, which Identity throws for.
    [Fact]
    public async Task A_completion_with_no_options_requested_first_gets_422_and_changes_nothing()
    {
        var link = await InviteAsync();
        var stamp = await factory.SecurityStampAsync(link.UserId);
        using var client = app.CreateHttpsClient();
        var (token, _) = await client.StartSetupAsync(link);

        using var response = await CompleteAsync(client, token);

        await AssertCredentialRefusedAsync(response);
        await AssertNothingChangedAsync(link, stamp);
    }

    // The event is written, inside the transaction, before the writer throws, so only the rollback removes it, with
    // the passkey and the user's changes saved before it. The sign-in comes after the commit, so none starts.
    [Fact]
    public async Task With_an_audit_writer_that_throws_nothing_commits_and_no_session_starts()
    {
        await using var failing = app.WithWebHostBuilder(builder => builder.ConfigureTestServices(
            services => services.AddScoped<IAuditWriter>(
                provider => new FailsAfterWriting(ActivatorUtilities.CreateInstance<AuditWriter>(provider)))));
        var link = await InviteAsync();
        var stamp = await factory.SecurityStampAsync(link.UserId);
        using var client = failing.CreateHttpsClient();
        var token = await StartWithOptionsAsync(client, link);

        using var response = await CompleteAsync(client, token);

        await response.ReadProblemAsync(HttpStatusCode.InternalServerError);
        Assert.DoesNotContain(response.SetCookies(), cookie => cookie.Name.Value == CookieSignIn.SessionCookie);
        await AssertNothingChangedAsync(link, stamp);
    }

    // The request's own tenant is B's, from the session that the test-only sign-in gives it, so only a tenant taken
    // from the user that the enrolment cookie names puts the event in A (the fifth criterion).
    [Fact]
    public async Task Signed_in_as_B_s_manager_completing_A_s_user_s_setup_writes_the_event_in_A()
    {
        var link = await InviteAsync();
        using var client = app.CreateHttpsClient().SignInAs(seeded.B.ManagerId);
        var token = await StartWithOptionsAsync(client, link);

        using (var response = await CompleteAsync(client, token))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        var (_, _, events) = await ReadAsync(link.UserId);
        Assert.Equal(seeded.A.OrganisationId, Assert.Single(events).OrganisationId);
    }

    // The enrolment cookie never becomes the request's user, so the token, which the antiforgery endpoint issued to
    // nobody before the start, still matches the request's user at completion (S00.04.02's Notes, from S00.01.04).
    [Fact]
    public async Task An_antiforgery_token_fetched_before_the_start_is_accepted_at_completion()
    {
        var link = await InviteAsync();
        using var client = app.CreateHttpsClient();
        var (tokenBeforeTheStart, _) = await client.StartSetupAsync(link);
        using (var options = await client.PostWithTokenAsync(OptionsPath, tokenBeforeTheStart))
        {
            Assert.Equal(HttpStatusCode.OK, options.StatusCode);
        }

        using var response = await CompleteAsync(client, tokenBeforeTheStart);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    public ValueTask DisposeAsync() => app.DisposeAsync();

    // A refusal keeps the person at the passkey step, with no session.
    private static async Task AssertCredentialRefusedAsync(HttpResponseMessage response)
    {
        using var problem = JsonDocument.Parse(await response.ReadProblemAsync(HttpStatusCode.UnprocessableEntity));
        Assert.Equal("validation.failed", problem.RootElement.GetProperty("code").GetString());
        Assert.Equal(
            ["credentialJson"],
            problem.RootElement.GetProperty("errors").EnumerateObject().Select(field => field.Name));
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.DoesNotContain(response.SetCookies(), cookie => cookie.Name.Value == CookieSignIn.SessionCookie);
    }

    // As the invite left the user: Invited, with no passkey, the stamp that the link holds and no event.
    private async Task AssertNothingChangedAsync(SetupLink link, string? stamp)
    {
        var (user, passkeys, events) = await ReadAsync(link.UserId);
        Assert.Equal(UserStatus.Invited, user.Status);
        Assert.Null(user.ActivatedAtUtc);
        Assert.False(user.EmailConfirmed);
        Assert.Equal(stamp, user.SecurityStamp);
        Assert.Empty(passkeys);
        Assert.Empty(events);
    }

    private Task<SetupLink> InviteAsync() => factory.InviteAsync(seeded.A);

    // With a passkey handler that, as the attestation ends, gives the user status and rotates the stamp, completion
    // gets 401 with no session, and the user keeps that status, with no passkey and no event.
    private async Task AssertChangeAtThePromptWinsAsync(UserStatus status)
    {
        await using var changing = app.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddScoped<IPasskeyHandler<ApplicationUser>>(provider => new ChangesTheUserAtThePrompt(
                ActivatorUtilities.CreateInstance<TestPasskeyHandler.Handler>(provider),
                provider.GetRequiredService<IServiceScopeFactory>(),
                status))));
        var link = await InviteAsync();
        using var client = changing.CreateHttpsClient();
        var token = await StartWithOptionsAsync(client, link);

        using var response = await CompleteAsync(client, token);

        await response.ReadProblemAsync(HttpStatusCode.Unauthorized);
        Assert.DoesNotContain(response.SetCookies(), cookie => cookie.Name.Value == CookieSignIn.SessionCookie);
        var (user, passkeys, events) = await ReadAsync(link.UserId);
        Assert.Equal(status, user.Status);
        Assert.Null(user.ActivatedAtUtc);
        Assert.Empty(passkeys);
        Assert.Empty(events);
    }

    // As view E does before a tap: starts setup, then asks for the options, which begin the ceremony. Returns the
    // antiforgery token, fetched before the start, which the SPA keeps for both later calls.
    private static async Task<string> StartWithOptionsAsync(HttpClient client, SetupLink link)
    {
        var (token, _) = await client.StartSetupAsync(link);
        using var options = await client.PostWithTokenAsync(OptionsPath, token);
        Assert.Equal(HttpStatusCode.OK, options.StatusCode);
        return token;
    }

    // As the SPA sends it, with D70's name, which the endpoint does not read.
    private static Task<HttpResponseMessage> CompleteAsync(
        HttpClient client, string token, string credentialJson = TestPasskeyHandler.Credential) =>
        client.PostWithTokenAsync(Path, token, new { credentialJson, name = "Grow2Notes" });

    // Read afresh, in a scope of its own, and the events in SQL, so that neither the tenant filter nor a tracked row
    // decides what is seen. The invite core writes no event, so every event about the user is completion's.
    private async Task<(ApplicationUser User, IList<UserPasskeyInfo> Passkeys, List<StoredEvent> Events)> ReadAsync(
        string userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByIdAsync(userId);
        Assert.NotNull(user);
        var events = await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Database
            .SqlQuery<StoredEvent>($"""
                SELECT [OrganisationId], [OccurredAtUtc], [ActorUserId], [EventType], [EntityType], [EntityId],
                       [ParticipantId], [Details], [IpAddress]
                FROM [AuditEvent]
                WHERE [EntityId] = {user.Id}
                """).ToListAsync(TestContext.Current.CancellationToken);
        return (user, await users.GetPasskeysAsync(user), events);
    }

    // An organisation is the tenant, not tenant-owned, so reading one needs no tenant.
    private async Task<string> OrganisationNameAsync(Guid organisationId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Organisations
            .Where(organisation => organisation.Id == organisationId)
            .Select(organisation => organisation.Name)
            .SingleAsync(TestContext.Current.CancellationToken);
    }

    private sealed record StoredEvent(
        Guid OrganisationId,
        DateTime OccurredAtUtc,
        Guid? ActorUserId,
        string EventType,
        string? EntityType,
        Guid? EntityId,
        Guid? ParticipantId,
        string? Details,
        string? IpAddress);

    // Writes the event, then throws, so that the event and everything saved before it are in the transaction.
    private sealed class FailsAfterWriting(IAuditWriter writer) : IAuditWriter
    {
        public async Task WriteAsync(
            Guid organisationId,
            Guid? actorUserId,
            string eventType,
            string? entityType,
            Guid? entityId,
            Guid? participantId,
            JsonObject? details,
            CancellationToken cancellationToken = default)
        {
            await writer.WriteAsync(
                organisationId, actorUserId, eventType, entityType, entityId, participantId, details,
                cancellationToken);
            throw new InvalidOperationException("The audit writer failed after writing the event.");
        }
    }

    // The test handler, which changes the user as the attestation ends, as design.md §8.6's deactivation and reset do:
    // the status set and the stamp rotated, in a scope of its own, as a manager's request would.
    private sealed class ChangesTheUserAtThePrompt(
        IPasskeyHandler<ApplicationUser> handler, IServiceScopeFactory scopes, UserStatus status)
        : IPasskeyHandler<ApplicationUser>
    {
        public Task<PasskeyCreationOptionsResult> MakeCreationOptionsAsync(
            PasskeyUserEntity userEntity, HttpContext httpContext) =>
            handler.MakeCreationOptionsAsync(userEntity, httpContext);

        public async Task<PasskeyAttestationResult> PerformAttestationAsync(PasskeyAttestationContext context)
        {
            var result = await handler.PerformAttestationAsync(context);
            Assert.True(result.Succeeded);
            await using var scope = scopes.CreateAsyncScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await users.FindByIdAsync(result.UserEntity.Id);
            Assert.NotNull(user);
            user.Status = status;
            Assert.True((await users.UpdateSecurityStampAsync(user)).Succeeded);
            return result;
        }

        public Task<PasskeyRequestOptionsResult> MakeRequestOptionsAsync(
            ApplicationUser? user, HttpContext httpContext) =>
            handler.MakeRequestOptionsAsync(user, httpContext);

        public Task<PasskeyAssertionResult<ApplicationUser>> PerformAssertionAsync(PasskeyAssertionContext context) =>
            handler.PerformAssertionAsync(context);
    }

    /// <summary>
    /// Gives every request <see cref="ClientAddress"/> as its client address, before the app's middleware, as the
    /// server's socket gives one: the test server gives none, and the audit event records it.
    /// </summary>
    private sealed class FromClientAddress : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, nextMiddleware) =>
            {
                context.Connection.RemoteIpAddress = IPAddress.Parse(ClientAddress);
                return nextMiddleware(context);
            });
            next(app);
        };
    }
}
