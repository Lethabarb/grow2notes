namespace Grow2Notes.Tests.Fixtures;

/// <summary>
/// Signing in with a password through the app's own endpoints (design.md §6.2, §8.2), as the SPA does.
/// </summary>
internal static class PasswordSignIn
{
    /// <summary>The password step, which takes the email and the password.</summary>
    public const string Path = "/api/auth/login";

    /// <summary>
    /// Sends <paramref name="email"/> and <paramref name="password"/> to the password step as the SPA sends them, with
    /// an antiforgery token that it fetches first, and returns the response. The client is one from
    /// <see cref="AntiforgeryTokens.CreateHttpsClient"/>, which keeps the cookies it is sent, Identity's two-factor
    /// cookie among them, and sends them with every later request.
    /// </summary>
    public static async Task<HttpResponseMessage> SendPasswordAsync(
        this HttpClient client, string email, string password) =>
        await client.PostWithTokenAsync(Path, await client.FetchTokenAsync(), new { email, password });
}
