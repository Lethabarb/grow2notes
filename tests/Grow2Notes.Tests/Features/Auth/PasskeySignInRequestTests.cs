using System.Net;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using Grow2Notes.Tests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Grow2Notes.Tests.Features.Auth;

/// <summary>
/// The body of <c>POST /api/auth/passkey</c> (design.md §6.2), sent signed out over HTTPS with the antiforgery token,
/// as the SPA sends it. A request without the credential is refused before the endpoint runs, so it never reaches
/// Identity, which would throw. The app is hosted without a database, with its Data Protection keys in memory, so the
/// tests run without Docker; <see cref="PasskeySignInEndpointTests"/> covers the sign-in itself.
/// </summary>
public sealed class PasskeySignInRequestTests : IAsyncDisposable
{
    private readonly AppWithoutDatabase host = new("Production");
    private readonly WebApplicationFactory<Program> app;

    public PasskeySignInRequestTests() => app = host.WithWebHostBuilder(builder => builder.KeepKeysInMemory());

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"credentialJson":null}""")]
    [InlineData("""{"credentialJson":""}""")]
    public async Task A_request_without_a_credential_gets_a_422_validation_failed_problem_naming_credentialJson(
        string body)
    {
        using var client = app.CreateHttpsClient();
        var token = await client.FetchTokenAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, PasskeySignIn.Path)
        {
            Content = new StringContent(body, Encoding.UTF8, MediaTypeNames.Application.Json),
        };
        request.Headers.Add(AntiforgeryTokens.Header, token);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        using var problem = JsonDocument.Parse(await response.ReadProblemAsync(HttpStatusCode.UnprocessableEntity));
        Assert.Equal("validation.failed", problem.RootElement.GetProperty("code").GetString());
        Assert.Equal(
            "credentialJson", Assert.Single(problem.RootElement.GetProperty("errors").EnumerateObject()).Name);
    }

    // Disposing the factory the app was derived from disposes the app too.
    public ValueTask DisposeAsync() => host.DisposeAsync();
}
