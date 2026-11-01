using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Features.Users;
using Grow2Notes.Web.Platform.Audit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

namespace Grow2Notes.Tests.Features.Auth;

/// <summary>
/// <c>POST /api/auth/setup/password-and-passkey</c> (design.md §6.2, §8.1 steps 4 and 5, D97), called as the SPA calls
/// it: signed out, over HTTPS, after <c>POST /api/auth/setup/start</c> with a link of the test's own
/// (<see cref="SetupLinks"/>) and <c>POST /api/auth/setup/passkey/options</c>, with the one antiforgery token that the
/// SPA fetches before the start, and with the password and the credential. The app is hosted on a fake clock with the
/// test-only passkey handler (<see cref="TestPasskeyHandler"/>), which only attests, so the passkey step of the
/// sign-in that follows is proved by <see cref="PasskeyStepOptionsEndpointTests"/> and end to end. The endpoint matrix
/// covers callers with no enrolment cookie, and <see cref="SetupPasskeyEndpointTests"/> the passkey path alone, which
/// this one leaves as it was.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class SetupPasswordAndPasskeyEndpointTests : IClassFixture<Grow2NotesFactory>, IAsyncDisposable
{
    private const string Path = "/api/auth/setup/password-and-passkey";

    private const string OptionsPath = "/api/auth/setup/passkey/options";

    // "crèmebrûlées": exactly 12 lowercase letters, accented ones among them, which only design.md §8.4's rule accepts.
    private const string Password = "crèmebrûlées";

    private readonly FakeTimeProvider clock = new(TestClock.Start);
    private readonly RecordingLoggerProvider logs = new();
    private readonly Grow2NotesFactory factory;
    private readonly SeededOrganisations seeded;
    private readonly WebApplicationFactory<Program> app;

    public SetupPasswordAndPasskeyEndpointTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    {
        this.factory = factory;
        seeded = sqlServer.Seeded;
        app = factory.WithWebHostBuilder(builder => builder
            .UseClock(clock)
            .UseTestPasskeyHandler()
            .ConfigureLogging(logging => logging.AddProvider(logs)));
    }

    // The password and one passkey, with two-factor on: the (d) combination. The person takes two minutes at the
    // passkey prompt, so the activation is completion's time. The rotated stamp ends the link.
    [Fact]
    public async Task Completion_adds_the_password_and_the_passkey_turns_two_factor_on_and_writes_one_event()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var link = await InviteAsync();
        var stamp = await factory.SecurityStampAsync(link.UserId);
        using var client = app.CreateHttpsClient();
        var token = await StartWithOptionsAsync(client, link);
        clock.Advance(TimeSpan.FromMinutes(2));

        using (var response = await CompleteAsync(client, token))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
            Assert.Contains(response.SetCookies(), cookie => cookie.Name.Value == CookieSignIn.SessionCookie);
            var me = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
            Assert.Equal(link.UserId, me.GetProperty("userId").GetString());
        }

        var after = await ReadAsync(link);
        Assert.True(after.HasPassword);
        Assert.Equal("Grow2Notes", Assert.Single(after.PasskeyNames));
        Assert.True(after.User.TwoFactorEnabled);
        Assert.Equal(UserStatus.Active, after.User.Status);
        Assert.Equal(TestClock.Start.AddMinutes(2).UtcDateTime, after.User.ActivatedAtUtc);
        Assert.True(after.User.EmailConfirmed);
        Assert.NotEqual(stamp, after.User.SecurityStamp);
        Assert.Equal([AuditEventTypes.Auth.SetupCompleted], after.EventTypes);
        using var again = app.CreateHttpsClient();
        await again.AssertSetupLinkEndedAsync(link);
    }

    // §8.1's second-sign-in test for the password and the passkey: the password step asks for the passkey, and no code
    // signs the person in, though the invite stored an authenticator key and the address is confirmed.
    [Fact]
    public async Task After_completion_the_password_step_asks_for_the_passkey_and_the_code_step_refuses()
    {
        var link = await InviteAsync();
        using (var client = app.CreateHttpsClient())
        {
            using var completion = await CompleteAsync(client, await StartWithOptionsAsync(client, link));
            Assert.Equal(HttpStatusCode.OK, completion.StatusCode);
        }

        using var signIn = app.CreateHttpsClient();
        using var passwordStep = await signIn.SendPasswordAsync(link.Email, Password);

        Assert.Equal(HttpStatusCode.OK, passwordStep.StatusCode);
        Assert.Equal(
            """{"next":"passkey"}""",
            await passwordStep.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.DoesNotContain(passwordStep.SetCookies(), cookie => cookie.Name.Value == CookieSignIn.SessionCookie);
        using var code = await signIn.SendCodeAsync(AuthenticatorCodes.Now((await ReadAsync(link)).Key));
        await code.ReadProblemAsync(HttpStatusCode.Unauthorized);
        Assert.DoesNotContain(code.SetCookies(), cookie => cookie.Name.Value == CookieSignIn.SessionCookie);
    }

    [Fact]
    public async Task Completion_without_the_enrolment_cookie_gets_401_and_changes_nothing()
    {
        var link = await InviteAsync();
        var stamp = await factory.SecurityStampAsync(link.UserId);
        using var client = app.CreateHttpsClient();

        using var response = await CompleteAsync(client, await client.FetchTokenAsync());

        await response.ReadProblemAsync(HttpStatusCode.Unauthorized);
        await AssertNothingChangedAsync(link, stamp);
    }

    // The password is checked before the credential, which only reads too, so a refused password leaves the ceremony
    // unused: the same options still serve a completion with a password that is accepted.
    [Theory]
    [InlineData("elevenchars")]
    [InlineData("            ")]
    public async Task A_short_or_whitespace_password_gets_422_for_the_password_with_the_ceremony_unused(
        string password)
    {
        var link = await InviteAsync();
        var stamp = await factory.SecurityStampAsync(link.UserId);
        using var client = app.CreateHttpsClient();
        var token = await StartWithOptionsAsync(client, link);

        using (var response = await CompleteAsync(client, token, password))
        {
            await AssertRefusedAsync(response, "password");
        }

        await AssertNothingChangedAsync(link, stamp);
        using var again = await CompleteAsync(client, token);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
    }

    // The built-in validation answers before the endpoint runs.
    [Theory]
    [InlineData("password")]
    [InlineData("credentialJson")]
    public async Task A_body_without_one_of_the_fields_gets_422_for_it_and_changes_nothing(string missing)
    {
        var link = await InviteAsync();
        var stamp = await factory.SecurityStampAsync(link.UserId);
        using var client = app.CreateHttpsClient();
        var token = await StartWithOptionsAsync(client, link);
        object body = missing == "password"
            ? new { credentialJson = TestPasskeyHandler.Credential }
            : new { password = Password };

        using var response = await client.PostWithTokenAsync(Path, token, body);

        await AssertRefusedAsync(response, missing);
        await AssertNothingChangedAsync(link, stamp);
    }

    [Fact]
    public async Task A_credential_that_Identity_refuses_gets_422_for_the_credential_and_changes_nothing()
    {
        var link = await InviteAsync();
        var stamp = await factory.SecurityStampAsync(link.UserId);
        using var client = app.CreateHttpsClient();
        var token = await StartWithOptionsAsync(client, link);

        using var response = await CompleteAsync(client, token, credentialJson: TestPasskeyHandler.RefusedCredential);

        await AssertRefusedAsync(response, "credentialJson");
        await AssertNothingChangedAsync(link, stamp);
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

        await AssertRefusedAsync(response, "credentialJson");
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

        await AssertRefusedAsync(response, "credentialJson");
        await AssertNothingChangedAsync(first, firstStamp);
        await AssertNothingChangedAsync(second, secondStamp);
    }

    // The event is written, inside the transaction, before the writer throws, so only the rollback removes it, with
    // the password, the passkey and two-factor sign-in saved before it. The sign-in comes after the commit, so none
    // starts.
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

    // Two devices finish setup from one link at once, each with a ceremony of its own: the laptop's transaction loads
    // the user, and the phone's completion commits before the laptop's first save. Exactly one stands.
    [Fact]
    public async Task Two_completions_at_once_commit_exactly_one_and_write_one_event()
    {
        var link = await InviteAsync();
        var gate = new HoldsTheFirstSaveOfTheUser(Guid.Parse(link.UserId));
        await using var gated = gate.On(app);
        using var laptop = gated.CreateHttpsClient();
        using var phone = gated.CreateHttpsClient();
        var laptopToken = await StartWithOptionsAsync(laptop, link);
        var phoneToken = await StartWithOptionsAsync(phone, link);

        var laptopCompletion = CompleteAsync(laptop, laptopToken);
        try
        {
            await gate.Reached.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            using var phoneCompletion = await CompleteAsync(phone, phoneToken);
            Assert.Equal(HttpStatusCode.OK, phoneCompletion.StatusCode);
        }
        finally
        {
            gate.Release();
        }

        using var response = await laptopCompletion;

        await response.ReadProblemAsync(HttpStatusCode.InternalServerError);
        Assert.DoesNotContain(response.SetCookies(), cookie => cookie.Name.Value == CookieSignIn.SessionCookie);
        var after = await ReadAsync(link);
        Assert.Single(after.PasskeyNames);
        Assert.Equal([AuditEventTypes.Auth.SetupCompleted], after.EventTypes);
    }

    // A manager's reset takes away the password, the passkey and two-factor sign-in (§8.6), and the person can then
    // choose again, the password only included.
    [Fact]
    public async Task After_a_manager_resets_a_person_sign_in_they_can_set_up_a_password_only()
    {
        var link = await InviteAsync();
        using (var first = app.CreateHttpsClient())
        {
            using var completion = await CompleteAsync(first, await StartWithOptionsAsync(first, link));
            Assert.Equal(HttpStatusCode.OK, completion.StatusCode);
        }

        var newLink = await ResetAsync(link);
        var reset = await ReadAsync(link);
        Assert.False(reset.HasPassword);
        Assert.Empty(reset.PasskeyNames);
        Assert.False(reset.User.TwoFactorEnabled);
        using var client = app.CreateHttpsClient();
        var (token, _) = await client.StartSetupAsync(newLink);

        using var response = await client.PostWithTokenAsync(
            "/api/auth/setup/password-only", token, new { password = Password });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var after = await ReadAsync(link);
        Assert.True(after.HasPassword);
        Assert.Empty(after.PasskeyNames);
        Assert.False(after.User.TwoFactorEnabled);
    }

    // design.md §9.5: no log entry holds a secret, the password and the credential included.
    [Fact]
    public async Task No_log_entry_holds_the_password_or_the_credential()
    {
        var link = await InviteAsync();
        using var client = app.CreateHttpsClient();
        var token = await StartWithOptionsAsync(client, link);
        using (var refusal = await CompleteAsync(client, token, credentialJson: TestPasskeyHandler.RefusedCredential))
        {
            Assert.Equal(HttpStatusCode.UnprocessableEntity, refusal.StatusCode);
        }

        using (var completion = await CompleteAsync(client, token))
        {
            Assert.Equal(HttpStatusCode.OK, completion.StatusCode);
        }

        Assert.NotEmpty(logs.Entries);
        string[] secrets = [Password, TestPasskeyHandler.Credential, TestPasskeyHandler.RefusedCredential];
        Assert.All(logs.Entries, entry =>
        {
            string?[] logged =
            [
                entry.Message,
                entry.Exception?.ToString(),
                .. entry.Values.Select(value => Convert.ToString(value.Value, CultureInfo.InvariantCulture)),
            ];
            Assert.DoesNotContain(
                logged,
                text => text is not null && secrets.Any(secret => text.Contains(secret, StringComparison.Ordinal)));
        });
    }

    public ValueTask DisposeAsync() => app.DisposeAsync();

    // As view F does before the press: starts setup, then asks for the options, which begin the ceremony. Returns the
    // antiforgery token, fetched before the start, which the SPA keeps for both later calls.
    private static async Task<string> StartWithOptionsAsync(HttpClient client, SetupLink link)
    {
        var (token, _) = await client.StartSetupAsync(link);
        using var options = await client.PostWithTokenAsync(OptionsPath, token);
        Assert.Equal(HttpStatusCode.OK, options.StatusCode);
        return token;
    }

    // As the SPA sends it: the password as typed, and the credential.
    private static Task<HttpResponseMessage> CompleteAsync(
        HttpClient client,
        string token,
        string password = Password,
        string credentialJson = TestPasskeyHandler.Credential) =>
        client.PostWithTokenAsync(Path, token, new { password, credentialJson });

    // A refusal keeps the person on the step, with no session, and names the one field.
    private static async Task AssertRefusedAsync(HttpResponseMessage response, string field)
    {
        using var problem = JsonDocument.Parse(await response.ReadProblemAsync(HttpStatusCode.UnprocessableEntity));
        Assert.Equal("validation.failed", problem.RootElement.GetProperty("code").GetString());
        Assert.Equal([field], problem.RootElement.GetProperty("errors").EnumerateObject().Select(error => error.Name));
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.DoesNotContain(response.SetCookies(), cookie => cookie.Name.Value == CookieSignIn.SessionCookie);
    }

    // As the invite left the user: Invited, with no password, no passkey, two-factor sign-in off, the stamp that the
    // link holds and no event.
    private async Task AssertNothingChangedAsync(SetupLink link, string? stamp)
    {
        var after = await ReadAsync(link);
        Assert.Equal(UserStatus.Invited, after.User.Status);
        Assert.Null(after.User.ActivatedAtUtc);
        Assert.False(after.User.EmailConfirmed);
        Assert.False(after.HasPassword);
        Assert.Empty(after.PasskeyNames);
        Assert.False(after.User.TwoFactorEnabled);
        Assert.Equal(stamp, after.User.SecurityStamp);
        Assert.Empty(after.EventTypes);
    }

    private Task<SetupLink> InviteAsync() => factory.InviteAsync(seeded.A);

    // As a manager's Reset sign-in does, through the reset step, which the endpoint tests of Reset sign-in cover.
    private async Task<SetupLink> ResetAsync(SetupLink link)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByIdAsync(link.UserId);
        Assert.NotNull(user);
        var setupLink = await scope.ServiceProvider.GetRequiredService<SignInReset>().ResetAsync(user);
        Assert.NotNull(setupLink);
        return SetupLinks.Read(setupLink, link.Email);
    }

    // Read afresh, in a scope of its own, and the events in SQL, so that neither the tenant filter nor a tracked row
    // decides what is seen. The invite core writes no event, so every event about the user is completion's.
    private async Task<Account> ReadAsync(SetupLink link)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByIdAsync(link.UserId);
        Assert.NotNull(user);
        var key = await users.GetAuthenticatorKeyAsync(user);
        Assert.NotNull(key);
        var eventTypes = await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Database
            .SqlQuery<string>($"""
                SELECT [EventType] AS [Value] FROM [AuditEvent] WHERE [EntityId] = {user.Id} ORDER BY [Id]
                """)
            .ToListAsync(TestContext.Current.CancellationToken);
        return new(
            user,
            await users.HasPasswordAsync(user),
            key,
            [.. (await users.GetPasskeysAsync(user)).Select(passkey => passkey.Name)],
            eventTypes);
    }

    // The user's row, whether they have a password, their authenticator key, the names of their passkeys, and the types
    // of the events about them.
    private sealed record Account(
        ApplicationUser User, bool HasPassword, string Key, List<string?> PasskeyNames, List<string> EventTypes);

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
}
