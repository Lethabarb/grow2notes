using System.Net;
using System.Text.Json;
using Grow2Notes.Tests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Grow2Notes.Tests.Platform;

/// <summary>
/// The <c>/api</c> group's antiforgery check (design.md §9.8 item 2), signed out, at a test-only endpoint in the group
/// (<see cref="TestOnlyApiEndpoint"/>), over HTTPS. The app is hosted without a database, with its Data Protection keys
/// in memory, so the tests run without Docker; <see cref="ApiGroupOnSqlServerTests"/> covers signing in.
/// </summary>
public sealed class ApiGroupTests : IAsyncDisposable
{
    private readonly AppWithoutDatabase host = new("Production");
    private readonly WebApplicationFactory<Program> app;

    public ApiGroupTests() =>
        app = host.WithWebHostBuilder(builder =>
        {
            TestOnlyApiEndpoint.MapIn(builder);
            builder.KeepKeysInMemory();
        });

    public static TheoryData<string> StateChangingMethods { get; } = ["POST", "PUT", "DELETE"];

    [Theory]
    [MemberData(nameof(StateChangingMethods))]
    public async Task A_request_with_a_token_and_its_cookie_gets_through(string method)
    {
        using var client = app.CreateHttpsClient();
        var token = await client.FetchTokenAsync();
        using var request = Request(method, token);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    // With the cookie, as a browser sends it with a forged request, but no token.
    [Theory]
    [MemberData(nameof(StateChangingMethods))]
    public async Task A_request_without_a_token_gets_a_400_problem_with_no_code_that_is_never_stored(string method)
    {
        using var client = app.CreateHttpsClient();
        await client.FetchTokenAsync();
        using var request = Request(method, token: null);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        using var problem = JsonDocument.Parse(await response.ReadProblemAsync(HttpStatusCode.BadRequest));
        Assert.False(problem.RootElement.TryGetProperty("code", out _));
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task A_token_without_its_cookie_is_refused()
    {
        using var issuedTo = app.CreateHttpsClient();
        var token = await issuedTo.FetchTokenAsync();
        using var client = app.CreateHttpsClient();
        using var request = Request("POST", token);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        await response.ReadProblemAsync(HttpStatusCode.BadRequest);
    }

    // Each client stands for a browser with a cookie of its own.
    [Fact]
    public async Task A_token_that_came_with_another_cookie_is_refused()
    {
        using var other = app.CreateHttpsClient();
        var othersToken = await other.FetchTokenAsync();
        using var client = app.CreateHttpsClient();
        await client.FetchTokenAsync();
        using var request = Request("POST", othersToken);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        await response.ReadProblemAsync(HttpStatusCode.BadRequest);
    }

    // In the form field where antiforgery looks by default when the header has none.
    [Fact]
    public async Task A_token_in_a_form_field_rather_than_the_header_is_refused()
    {
        using var client = app.CreateHttpsClient();
        var token = await client.FetchTokenAsync();
        using var request = Request("POST", token: null);
        request.Content = new FormUrlEncodedContent([new("__RequestVerificationToken", token)]);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        await response.ReadProblemAsync(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_value_that_is_no_token_is_refused()
    {
        using var client = app.CreateHttpsClient();
        await client.FetchTokenAsync();
        using var request = Request("POST", "not-a-token");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        await response.ReadProblemAsync(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_GET_needs_no_token_or_cookie()
    {
        using var client = app.CreateHttpsClient();
        using var request = Request("GET", token: null);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    // The unknown-/api catch-all is outside the group, so the group's filter never answers in its place.
    [Theory]
    [MemberData(nameof(StateChangingMethods))]
    public async Task An_unknown_api_path_is_a_404_problem_without_a_token(string method)
    {
        using var client = app.CreateHttpsClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), "/api/no-such-path");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        await response.ReadProblemAsync(HttpStatusCode.NotFound);
    }

    // Disposing the factory the app was derived from disposes the app too.
    public ValueTask DisposeAsync() => host.DisposeAsync();

    private static HttpRequestMessage Request(string method, string? token)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), TestOnlyApiEndpoint.Path);
        if (token is not null)
        {
            request.Headers.Add(AntiforgeryTokens.Header, token);
        }

        return request;
    }
}
