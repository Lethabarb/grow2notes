using System.Net;
using Grow2Notes.Web.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests.Fixtures;

/// <summary>
/// Signing in with a password through the app's own endpoints (design.md §6.2, §8.2), as the SPA does: the password
/// step, then the code step, with a code from the user's authenticator key (<see cref="AuthenticatorCodes"/>), for a
/// user that a test adds with <see cref="AddPasswordUserAsync"/>.
/// </summary>
internal static class PasswordSignIn
{
    /// <summary>The password step, which takes the email and the password.</summary>
    public const string Path = "/api/auth/login";

    /// <summary>The code step, which takes the code from the person's authenticator app.</summary>
    public const string CodePath = "/api/auth/login/totp";

    /// <summary>
    /// The password of every user that <see cref="AddPasswordUserAsync"/> adds: made up (A35), and allowed by
    /// Identity's default password rules, which want 6 characters, a digit, a capital, a lowercase letter and a symbol,
    /// and by design.md §8.4's, which want 12 characters and nothing else.
    /// </summary>
    public const string Password = "Made-up passw0rd";

    /// <summary>The display name of every user that <see cref="AddPasswordUserAsync"/> adds.</summary>
    public const string DisplayName = "Sam Taylor";

    /// <summary>
    /// Adds a worker to <paramref name="organisation"/> through the app's <c>UserManager</c>, as setup's password path
    /// leaves one (design.md §8.1): with <see cref="Password"/>, an authenticator key, made as the invite makes it,
    /// and two-factor on, unless <paramref name="twoFactorEnabled"/> turns it off; Active unless
    /// <paramref name="status"/> says otherwise; and with an address that no other test or run uses. The seeded users
    /// have no password, and other tests rely on their lockout (tests/README.md).
    /// </summary>
    public static async Task<PasswordUser> AddPasswordUserAsync(
        this WebApplicationFactory<Program> app,
        SeededOrganisation organisation,
        UserStatus status = UserStatus.Active,
        bool twoFactorEnabled = true)
    {
        var email = $"{Guid.NewGuid():N}@example.org";
        await using var scope = app.Services.CreateAsyncScope();
        var now = scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        var user = new ApplicationUser
        {
            OrganisationId = organisation.OrganisationId,
            DisplayName = DisplayName,
            Role = UserRole.Worker,
            Status = status,
            InvitedAtUtc = now,
            InvitedByUserId = organisation.ManagerId,
            ActivatedAtUtc = now,
            UserName = email,
            Email = email,
            EmailConfirmed = true,
        };
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        AssertSucceeded(await users.CreateAsync(user, Password));
        AssertSucceeded(await users.ResetAuthenticatorKeyAsync(user));
        AssertSucceeded(await users.SetTwoFactorEnabledAsync(user, twoFactorEnabled));
        var key = await users.GetAuthenticatorKeyAsync(user);
        Assert.NotNull(key);
        return new(user.Id, email, key);
    }

    /// <summary>
    /// Signs <paramref name="client"/> in with <paramref name="email"/>, <paramref name="password"/> and
    /// <paramref name="code"/>, and returns the code step's response: sends the password, checks that the password
    /// step asked for the code, and sends the code. The client is one from
    /// <see cref="AntiforgeryTokens.CreateHttpsClient"/>, which keeps the cookies it is sent, the session cookie among
    /// them, and sends them with every later request.
    /// </summary>
    public static async Task<HttpResponseMessage> SignInWithPasswordAsync(
        this HttpClient client, string email, string password, string code)
    {
        using var passwordStep = await client.SendPasswordAsync(email, password);
        Assert.Equal(HttpStatusCode.OK, passwordStep.StatusCode);
        Assert.Equal(
            """{"next":"totp"}""",
            await passwordStep.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return await client.SendCodeAsync(code);
    }

    /// <summary>
    /// Sends <paramref name="email"/> and <paramref name="password"/> to the password step as the SPA sends them, with
    /// an antiforgery token that it fetches first, and returns the response. The client keeps the cookies it is sent,
    /// Identity's two-factor cookie among them, which holds the code step.
    /// </summary>
    public static async Task<HttpResponseMessage> SendPasswordAsync(
        this HttpClient client, string email, string password) =>
        await client.PostWithTokenAsync(Path, await client.FetchTokenAsync(), new { email, password });

    /// <summary>
    /// Sends <paramref name="code"/> to the code step as the SPA sends it, with an antiforgery token that it fetches
    /// first, and returns the response. The code step signs in the account in the two-factor cookie that the client
    /// holds, if it holds one.
    /// </summary>
    public static async Task<HttpResponseMessage> SendCodeAsync(this HttpClient client, string code) =>
        await client.PostWithTokenAsync(CodePath, await client.FetchTokenAsync(), new { code });

    private static void AssertSucceeded(IdentityResult result) =>
        Assert.True(result.Succeeded, string.Join(" ", result.Errors.Select(error => error.Code)));
}

/// <summary>
/// A user that <see cref="PasswordSignIn.AddPasswordUserAsync"/> added: their ID, their address, and the key that
/// their authenticator app holds, in base32, from which <see cref="AuthenticatorCodes"/> computes their codes.
/// </summary>
internal sealed record PasswordUser(Guid Id, string Email, string AuthenticatorKey);
