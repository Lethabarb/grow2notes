using System.Net;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using Grow2Notes.Tests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Grow2Notes.Tests.Features.Auth;

/// <summary>
/// The body of <c>POST /api/auth/login</c> (design.md §6.2), sent signed out over HTTPS with the antiforgery token, as
/// the SPA sends it. A request without the email or the password, or with one that is empty or only whitespace, is
/// refused before the endpoint runs, naming each that is missing. The app is hosted without a database, with its Data
/// Protection keys in memory, so the tests run without Docker; <see cref="PasswordSignInEndpointTests"/> covers the
/// sign-in itself.
/// </summary>
public sealed class PasswordSignInRequestTests : IAsyncDisposable
{
    private readonly AppWithoutDatabase host = new("Production");
    private readonly WebApplicationFactory<Program> app;

    public PasswordSignInRequestTests() => app = host.WithWebHostBuilder(builder => builder.KeepKeysInMemory());

    [Theory]
    [InlineData("{}", "email password")]
    [InlineData("""{"email":null,"password":null}""", "email password")]
    [InlineData("""{"password":"Made-up passw0rd"}""", "email")]
    [InlineData("""{"email":"","password":"Made-up passw0rd"}""", "email")]
    [InlineData("""{"email":"sam@example.org"}""", "password")]
    [InlineData("""{"email":"sam@example.org","password":""}""", "password")]
    [InlineData("""{"email":"sam@example.org","password":"   "}""", "password")]
    public async Task A_request_without_an_email_or_a_password_gets_a_422_validation_failed_problem_naming_each(
        string body, string missing)
    {
        using var client = app.CreateHttpsClient();
        var token = await client.FetchTokenAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, PasswordSignIn.Path)
        {
            Content = new StringContent(body, Encoding.UTF8, MediaTypeNames.Application.Json),
        };
        request.Headers.Add(AntiforgeryTokens.Header, token);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        using var problem = JsonDocument.Parse(await response.ReadProblemAsync(HttpStatusCode.UnprocessableEntity));
        Assert.Equal("validation.failed", problem.RootElement.GetProperty("code").GetString());
        Assert.Equal(
            missing.Split(' '),
            problem.RootElement.GetProperty("errors").EnumerateObject().Select(error => error.Name)
                .Order(StringComparer.Ordinal));
    }

    // Disposing the factory the app was derived from disposes the app too.
    public ValueTask DisposeAsync() => host.DisposeAsync();
}
