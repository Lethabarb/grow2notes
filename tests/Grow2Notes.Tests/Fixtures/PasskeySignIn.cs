using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Net.Http.Headers;

namespace Grow2Notes.Tests.Fixtures;

/// <summary>
/// Signing in with a passkey through the app's own endpoints (design.md §6.2, §8.2), as the SPA does, with a
/// <see cref="TestAuthenticator"/> in place of the browser's prompt.
/// </summary>
internal static class PasskeySignIn
{
    /// <summary>The sign-in endpoint, which takes the credential.</summary>
    public const string Path = "/api/auth/passkey";

    private const string OptionsPath = "/api/auth/passkey/options";

    /// <summary>
    /// Signs <paramref name="client"/> in with <paramref name="authenticator"/>'s passkey, and returns the sign-in's
    /// response: fetches an antiforgery token, asks for passkey request options, which set the ceremony's cookie, and
    /// sends the authenticator's assertion over their challenge. The client is one from
    /// <see cref="AntiforgeryTokens.CreateHttpsClient"/>, which keeps the cookies it is sent, the session cookie among
    /// them, and sends them with every later request.
    /// </summary>
    public static async Task<HttpResponseMessage> SignInWithPasskeyAsync(
        this HttpClient client, TestAuthenticator authenticator)
    {
        var token = await client.FetchTokenAsync();
        var challenge = await FetchChallengeAsync(client, token);

        using var request = Request(authenticator.GetAssertion(challenge));
        request.Headers.Add(AntiforgeryTokens.Header, token);
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// A request to the sign-in endpoint that sends <paramref name="credentialJson"/>, as the SPA sends a credential,
    /// without the antiforgery token, which the caller adds.
    /// </summary>
    /// <remarks>
    /// It carries the <c>Origin</c> header that a browser sends with every <c>POST</c>, naming the page that the
    /// credential was signed for, as Identity checks the one against the other.
    /// </remarks>
    public static HttpRequestMessage Request(string credentialJson)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, Path)
        {
            Content = JsonContent.Create(new { credentialJson }),
        };
        request.Headers.Add(HeaderNames.Origin, Grow2NotesFactory.Origin);
        return request;
    }

    // With no body, as the SPA asks for them.
    private static async Task<string> FetchChallengeAsync(HttpClient client, string token)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var request = new HttpRequestMessage(HttpMethod.Post, OptionsPath);
        request.Headers.Add(AntiforgeryTokens.Header, token);

        using var response = await client.SendAsync(request, cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var options = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var challenge = options.GetProperty("challenge").GetString();
        Assert.NotNull(challenge);
        return challenge;
    }
}
