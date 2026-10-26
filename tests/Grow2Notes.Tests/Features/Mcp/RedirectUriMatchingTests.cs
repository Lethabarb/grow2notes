using System.Net;
using Grow2Notes.Tests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using OpenIddict.Abstractions;

namespace Grow2Notes.Tests.Features.Mcp;

/// <summary>
/// Spike question 5 (S07.01.03): a native assistant, one that is registered with loopback redirect URIs and no port,
/// is called back on whichever port it is listening on, and on no other path (RFC 8252 §7.3). If a case fails in CI,
/// the gap goes to S07.01.06, which owns how the redirect URI is matched; it is not fixed here.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class RedirectUriMatchingTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    : IClassFixture<Grow2NotesFactory>, IAsyncLifetime
{
    private const string ClientId = "native-assistant";

    private WebApplicationFactory<Program> app = null!;

    public async ValueTask InitializeAsync()
    {
        app = await factory.OnDatabaseOfItsOwnAsync(sqlServer, "McpRedirectUriMatching");
        await app.RegisterClientAsync(
            ClientId, OpenIddictConstants.ApplicationTypes.Native, "http://127.0.0.1/cb", "http://localhost/cb");
    }

    public async ValueTask DisposeAsync() => await app.DisposeAsync();

    [Theory]
    [InlineData("http://127.0.0.1:51234/cb")]
    [InlineData("http://localhost:3118/cb")]
    public async Task A_loopback_redirect_URI_matches_on_any_port(string redirectUri)
    {
        var status = await AuthorizeAsync(redirectUri);

        Assert.Equal(HttpStatusCode.NotFound, status);
    }

    [Theory]
    [InlineData("http://127.0.0.1:51234/other")]
    [InlineData("http://localhost:3118/other")]
    public async Task A_loopback_redirect_URI_on_another_path_is_refused(string redirectUri)
    {
        var status = await AuthorizeAsync(redirectUri);

        Assert.NotEqual(HttpStatusCode.NotFound, status);
    }

    // Validated and passed on is a 404 (the consent step is a later task's), so 404 means OpenIddict accepted it.
    private async Task<HttpStatusCode> AuthorizeAsync(string redirectUri)
    {
        using var client = app.CreateAssistantClient();
        using var response = await client.GetAsync(
            "/oauth/authorize?" + McpTestApp.AuthorizeQuery(ClientId, redirectUri),
            TestContext.Current.CancellationToken);
        return response.StatusCode;
    }
}
