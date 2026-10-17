using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests.Fixtures;

/// <summary>
/// Calling the API as the SPA does (design.md §9.8 item 2): over HTTPS, with the token that
/// <c>GET /api/auth/antiforgery</c> issued in the <see cref="Header"/> header of each request that changes something,
/// beside the cookie that the endpoint set.
/// </summary>
internal static class AntiforgeryTokens
{
    public const string Header = "X-XSRF-TOKEN";

    /// <summary>
    /// A client of <paramref name="app"/> for the antiforgery endpoint and the <c>/api</c> group, which calls
    /// <c>https://localhost</c>: the antiforgery cookie is <c>Secure</c> always, so antiforgery throws on a request
    /// that is not HTTPS. Like a browser, it keeps the cookies it is sent and sends them back; unlike one, it follows
    /// no redirect, so a test sees the status that the app answered with.
    /// </summary>
    public static HttpClient CreateHttpsClient(this WebApplicationFactory<Program> app) =>
        app.CreateClient(new() { BaseAddress = new("https://localhost"), AllowAutoRedirect = false });

    /// <summary>
    /// Fetches a token issued to the user that <paramref name="client"/> calls as, or to nobody when it is signed out.
    /// The client keeps the cookie that comes with it, without which the token is refused.
    /// </summary>
    public static async Task<string> FetchTokenAsync(this HttpClient client)
    {
        using var response = await client.GetAsync("/api/auth/antiforgery", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        return Assert.Single(response.Headers.GetValues(Header));
    }

    /// <summary>
    /// Posts to <paramref name="path"/> as the SPA does: with <paramref name="token"/> in the <see cref="Header"/>
    /// header, <paramref name="body"/>, if any, as JSON, and <paramref name="ifMatch"/>, a row's ETag, if any, as
    /// <c>If-Match</c>.
    /// </summary>
    public static Task<HttpResponseMessage> PostWithTokenAsync(
        this HttpClient client, string path, string token, object? body = null, string? ifMatch = null) =>
        client.SendWithTokenAsync(HttpMethod.Post, path, token, body, ifMatch);

    /// <summary>
    /// Puts <paramref name="body"/> to <paramref name="path"/> as the SPA does: as JSON, with <paramref name="token"/>
    /// in the <see cref="Header"/> header, and with <paramref name="ifMatch"/>, a row's ETag, if any, as
    /// <c>If-Match</c>.
    /// </summary>
    public static Task<HttpResponseMessage> PutWithTokenAsync(
        this HttpClient client, string path, string token, object body, string? ifMatch) =>
        client.SendWithTokenAsync(HttpMethod.Put, path, token, body, ifMatch);

    /// <summary>
    /// For the app hosted without a database (<see cref="AppWithoutDatabase"/>): Data Protection, which protects the
    /// tokens, keeps its keys in memory, because its key ring is kept in the database.
    /// </summary>
    public static IWebHostBuilder KeepKeysInMemory(this IWebHostBuilder builder) =>
        builder.ConfigureTestServices(services => services.AddDataProtection().UseEphemeralDataProtectionProvider());

    private static async Task<HttpResponseMessage> SendWithTokenAsync(
        this HttpClient client, HttpMethod method, string path, string token, object? body, string? ifMatch)
    {
        using var request = new HttpRequestMessage(method, path)
        {
            Content = body is null ? null : JsonContent.Create(body),
        };
        request.Headers.Add(Header, token);
        if (ifMatch is not null)
        {
            request.Headers.IfMatch.Add(EntityTagHeaderValue.Parse(ifMatch));
        }

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }
}
