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
    public static Task<HttpResponseMessage> SignInWithPasskeyAsync(
        this HttpClient client, TestAuthenticator authenticator) =>
        client.SignInWithPasskeyAsync(challenge => authenticator.GetAssertion(challenge));

    /// <summary>
    /// Signs <paramref name="client"/> in as the overload with an authenticator does, but sends the credential that
    /// <paramref name="credentialFor"/> gives for the options' challenge, such as an assertion that fails.
    /// </summary>
    public static async Task<HttpResponseMessage> SignInWithPasskeyAsync(
        this HttpClient client, Func<string, string> credentialFor)
    {
        var token = await client.FetchTokenAsync();
        var challenge = await client.FetchPasskeyChallengeAsync(token);
        return await client.SendPasskeyCredentialAsync(credentialFor(challenge), token);
    }

    /// <summary>
    /// Asks for passkey request options with <paramref name="token"/>, and returns their challenge. The answer sets the
    /// ceremony's cookie, which replaces any earlier ceremony of <paramref name="client"/>'s.
    /// </summary>
    public static async Task<string> FetchPasskeyChallengeAsync(this HttpClient client, string token)
    {
        // With no body, as the SPA asks for them.
        using var response = await client.PostWithTokenAsync(OptionsPath, token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var options = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        var challenge = options.GetProperty("challenge").GetString();
        Assert.NotNull(challenge);
        return challenge;
    }

    /// <summary>
    /// Sends <paramref name="credentialJson"/> to the sign-in endpoint with <paramref name="token"/>, as the SPA sends
    /// a credential, from a page at <paramref name="origin"/>, and returns the response.
    /// </summary>
    public static async Task<HttpResponseMessage> SendPasskeyCredentialAsync(
        this HttpClient client, string credentialJson, string token, string origin = Grow2NotesFactory.Origin)
    {
        using var request = Request(credentialJson, origin);
        request.Headers.Add(AntiforgeryTokens.Header, token);
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// A request to the sign-in endpoint that sends <paramref name="credentialJson"/>, as the SPA sends a credential,
    /// from a page at <paramref name="origin"/>, without the antiforgery token, which the caller adds.
    /// </summary>
    /// <remarks>
    /// It carries the <c>Origin</c> header that a browser sends with every <c>POST</c>, naming the page that asked for
    /// the credential. Identity's own origin check compares the credential's origin with it; the app's compares it with
    /// <c>App:Origin</c> (design.md A54).
    /// </remarks>
    public static HttpRequestMessage Request(string credentialJson, string origin = Grow2NotesFactory.Origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, Path)
        {
            Content = JsonContent.Create(new { credentialJson }),
        };
        request.Headers.Add(HeaderNames.Origin, origin);
        return request;
    }
}
