using System.Net;
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
    /// For the app hosted without a database (<see cref="AppWithoutDatabase"/>): Data Protection, which protects the
    /// tokens, keeps its keys in memory, because its key ring is kept in the database.
    /// </summary>
    public static IWebHostBuilder KeepKeysInMemory(this IWebHostBuilder builder) =>
        builder.ConfigureTestServices(services => services.AddDataProtection().UseEphemeralDataProtectionProvider());
}
