using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Features.Auth;
using Grow2Notes.Web.Platform.Audit;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Grow2Notes.Tests.Features.Auth;

/// <summary>
/// <c>POST /api/auth/login/passkey/options</c> (design.md §6.2, §8.2, D97), the options of the passkey step of a sign-in
/// with a password and a passkey, called as the SPA calls it, after the password step, with the credential then sent to
/// <c>POST /api/auth/passkey</c> (<see cref="PasskeySignIn"/>). The users are added to seeded organisation A or B with a
/// password, an authenticator key and two-factor on, and a passkey that a test authenticator holds
/// (<see cref="TestAuthenticator"/>), which Identity's own passkey handler verifies. The app runs on a fake clock
/// (<see cref="TestClock"/>), which times Identity's two-factor cookie. The 429 of the shared permit is in
/// <see cref="Grow2Notes.Tests.Platform.RateLimitsTests"/>, and the endpoint matrix covers who may call it.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class PasskeyStepOptionsEndpointTests : IClassFixture<Grow2NotesFactory>, IAsyncDisposable
{
    private readonly FakeTimeProvider clock = new(TestClock.Start);
    private readonly SeededOrganisations seeded;
    private readonly WebApplicationFactory<Program> app;

    public PasskeyStepOptionsEndpointTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    {
        seeded = sqlServer.Seeded;
        app = factory.WithWebHostBuilder(builder => builder.UseClock(clock));
    }

    // No password step came first, so the request names no account, and there is nothing to audit.
    [Fact]
    public async Task Options_with_no_password_step_before_them_get_a_401_and_no_event()
    {
        using var client = app.CreateHttpsClient();
        var lastEventId = await LastEventIdAsync();

        using var response = await client.PostWithTokenAsync(
            PasskeySignIn.StepOptionsPath, await client.FetchTokenAsync());

        await response.ReadProblemAsync(HttpStatusCode.Unauthorized);
        Assert.Empty(response.SetCookies());
        Assert.Equal(lastEventId, await LastEventIdAsync());
    }

    // A person with an authenticator app has no passkey step, and the refusal leaves the cookie, so their code step
    // still works.
    [Fact]
    public async Task Options_after_the_password_step_of_a_user_with_no_passkey_get_a_401_and_the_code_still_works()
    {
        var user = await app.AddPasswordUserAsync(seeded.A);
        using var client = app.CreateHttpsClient();
        using var passwordStep = await client.SendPasswordAsync(user.Email, PasswordSignIn.Password);
        Assert.Equal(HttpStatusCode.OK, passwordStep.StatusCode);

        using var options = await client.PostWithTokenAsync(
            PasskeySignIn.StepOptionsPath, await client.FetchTokenAsync());

        await options.ReadProblemAsync(HttpStatusCode.Unauthorized);
        Assert.Empty(options.SetCookies());
        using var code = await client.SendCodeAsync(AuthenticatorCodes.Now(user.AuthenticatorKey));
        Assert.Equal(HttpStatusCode.OK, code.StatusCode);
    }

    // The ceremony replaces the password step in Identity's two-factor cookie, so a second call, and the code step,
    // find no account: each accepted password yields one set of options.
    [Fact]
    public async Task Options_list_exactly_the_user_s_passkey_and_replace_the_password_step()
    {
        var user = await app.AddPasswordUserAsync(seeded.A);
        using var authenticator = await TestAuthenticator.RegisterAsync(app.Services, user.Id);
        using var client = app.CreateHttpsClient();
        using var passwordStep = await client.SendPasswordAsync(user.Email, PasswordSignIn.Password);
        Assert.Equal("""{"next":"passkey"}""", await passwordStep.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken));
        var token = await client.FetchTokenAsync();

        using var options = await client.PostWithTokenAsync(PasskeySignIn.StepOptionsPath, token);

        Assert.Equal(HttpStatusCode.OK, options.StatusCode);
        Assert.Equal("application/json", options.Content.Headers.ContentType?.MediaType);
        var json = await options.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(
            [authenticator.CredentialId],
            json.GetProperty("allowCredentials").EnumerateArray().Select(
                credential => credential.GetProperty("id").GetString()));
        Assert.Equal("required", json.GetProperty("userVerification").GetString());
        using var again = await client.PostWithTokenAsync(PasskeySignIn.StepOptionsPath, token);
        await again.ReadProblemAsync(HttpStatusCode.Unauthorized);
        using var code = await client.SendCodeAsync(AuthenticatorCodes.Now(user.AuthenticatorKey));
        await code.ReadProblemAsync(HttpStatusCode.Unauthorized);
    }

    // The whole second step: the credential goes to the passkey sign-in, which signs the person in with its event.
    [Fact]
    public async Task The_passkey_after_the_password_signs_the_user_in_with_its_event()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var user = await app.AddPasswordUserAsync(seeded.A);
        using var authenticator = await TestAuthenticator.RegisterAsync(app.Services, user.Id);
        using var client = app.CreateHttpsClient();
        using var passwordStep = await client.SendPasswordAsync(user.Email, PasswordSignIn.Password);
        Assert.Equal(HttpStatusCode.OK, passwordStep.StatusCode);
        var token = await client.FetchTokenAsync();
        var challenge = await client.FetchPasskeyChallengeAsync(token, PasskeySignIn.StepOptionsPath);

        using var response = await client.SendPasskeyCredentialAsync(authenticator.GetAssertion(challenge), token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var me = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        Assert.Equal(user.Id, me.GetProperty("userId").GetGuid());
        Assert.Contains(response.SetCookies(), cookie => cookie.Name.Value == CookieSignIn.SessionCookie);
        Assert.Equal([AuditEventTypes.Auth.SignInSucceeded], await EventTypesAboutAsync(user.Id));

        // A second later, the session check runs (tests/README.md).
        clock.Advance(TimeSpan.FromSeconds(1));
        using var meAgain = await client.GetAsync("/api/auth/me", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, meAgain.StatusCode);
    }

    // Identity's identified-user check: the ceremony names the account of the password step, so a passkey of another
    // account, even one that is valid, fails, and is audited on the account the credential belongs to, as any failed
    // passkey is.
    [Fact]
    public async Task Another_account_s_passkey_after_the_password_gets_a_401_and_no_session()
    {
        var user = await app.AddPasswordUserAsync(seeded.A);
        using var authenticator = await TestAuthenticator.RegisterAsync(app.Services, user.Id);
        var other = await app.AddPasswordUserAsync(seeded.B);
        using var otherAuthenticator = await TestAuthenticator.RegisterAsync(app.Services, other.Id);
        using var client = app.CreateHttpsClient();
        using var passwordStep = await client.SendPasswordAsync(user.Email, PasswordSignIn.Password);
        Assert.Equal(HttpStatusCode.OK, passwordStep.StatusCode);
        var token = await client.FetchTokenAsync();
        var challenge = await client.FetchPasskeyChallengeAsync(token, PasskeySignIn.StepOptionsPath);

        using var response = await client.SendPasskeyCredentialAsync(otherAuthenticator.GetAssertion(challenge), token);

        await response.ReadProblemAsync(HttpStatusCode.Unauthorized);
        Assert.DoesNotContain(response.SetCookies(), cookie => cookie.Name.Value == CookieSignIn.SessionCookie);
        Assert.Equal([AuditEventTypes.Auth.SignInFailed], await EventTypesAboutAsync(other.Id));
        Assert.Empty(await EventTypesAboutAsync(user.Id));
    }

    // The ceremony lives in Identity's two-factor cookie, for 5 minutes from the options (D69, A142).
    [Fact]
    public async Task A_passkey_sent_more_than_5_minutes_after_the_options_gets_a_401_and_no_session()
    {
        var user = await app.AddPasswordUserAsync(seeded.A);
        using var authenticator = await TestAuthenticator.RegisterAsync(app.Services, user.Id);
        using var client = app.CreateHttpsClient();
        using var passwordStep = await client.SendPasswordAsync(user.Email, PasswordSignIn.Password);
        Assert.Equal(HttpStatusCode.OK, passwordStep.StatusCode);
        var token = await client.FetchTokenAsync();
        var challenge = await client.FetchPasskeyChallengeAsync(token, PasskeySignIn.StepOptionsPath);
        clock.Advance(TwoFactorCookie.Lifetime + TimeSpan.FromSeconds(1));

        using var response = await client.SendPasskeyCredentialAsync(authenticator.GetAssertion(challenge), token);

        await response.ReadProblemAsync(HttpStatusCode.Unauthorized);
        Assert.DoesNotContain(response.SetCookies(), cookie => cookie.Name.Value == CookieSignIn.SessionCookie);
    }

    // The options check nothing about the user's status, which the passkey sign-in checks before it signs anyone in
    // (Grow2NotesSignInManager), so a person deactivated between the two calls is refused there.
    [Fact]
    public async Task A_user_deactivated_between_the_options_and_the_passkey_gets_a_401_and_no_session()
    {
        var user = await app.AddPasswordUserAsync(seeded.A);
        using var authenticator = await TestAuthenticator.RegisterAsync(app.Services, user.Id);
        using var client = app.CreateHttpsClient();
        using var passwordStep = await client.SendPasswordAsync(user.Email, PasswordSignIn.Password);
        Assert.Equal(HttpStatusCode.OK, passwordStep.StatusCode);
        var token = await client.FetchTokenAsync();
        var challenge = await client.FetchPasskeyChallengeAsync(token, PasskeySignIn.StepOptionsPath);
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var loaded = await users.FindByIdAsync(user.Id.ToString());
            Assert.NotNull(loaded);
            loaded.Status = UserStatus.Deactivated;
            Assert.True((await users.UpdateAsync(loaded)).Succeeded);
        }

        using var response = await client.SendPasskeyCredentialAsync(authenticator.GetAssertion(challenge), token);

        await response.ReadProblemAsync(HttpStatusCode.Unauthorized);
        Assert.DoesNotContain(response.SetCookies(), cookie => cookie.Name.Value == CookieSignIn.SessionCookie);
    }

    // The password step refuses a Deactivated person, so nothing sets the cookie that the options need.
    [Fact]
    public async Task A_Deactivated_user_s_password_step_is_refused_so_no_options_can_be_had()
    {
        var user = await app.AddPasswordUserAsync(seeded.A, UserStatus.Deactivated);
        using var authenticator = await TestAuthenticator.RegisterAsync(app.Services, user.Id);
        using var client = app.CreateHttpsClient();
        using var passwordStep = await client.SendPasswordAsync(user.Email, PasswordSignIn.Password);
        await passwordStep.ReadProblemAsync(HttpStatusCode.Unauthorized);

        using var options = await client.PostWithTokenAsync(
            PasskeySignIn.StepOptionsPath, await client.FetchTokenAsync());

        await options.ReadProblemAsync(HttpStatusCode.Unauthorized);
    }

    public ValueTask DisposeAsync() => app.DisposeAsync();

    // The types of the events about the user, in the order written, read in SQL so that neither the tenant filter nor a
    // tracked row decides what is seen.
    private async Task<List<string>> EventTypesAboutAsync(Guid userId)
    {
        await using var scope = app.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Database
            .SqlQuery<string>($"""
                SELECT [EventType] AS [Value] FROM [AuditEvent] WHERE [EntityId] = {userId} ORDER BY [Id]
                """)
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    // The ID of the last event written, by any test: the tests of the collection run one at a time, so none writes
    // one while a test reads it before and after a request.
    private async Task<long?> LastEventIdAsync()
    {
        await using var scope = app.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Database
            .SqlQuery<long?>($"SELECT MAX([Id]) AS [Value] FROM [AuditEvent]")
            .SingleAsync(TestContext.Current.CancellationToken);
    }
}
