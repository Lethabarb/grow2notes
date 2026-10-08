using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Grow2Notes.Tests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Grow2Notes.Tests.Platform;

/// <summary>
/// The <c>/api</c> group's checks of a request that changes something (design.md §9.8 items 2 and 3), its antiforgery
/// token and the type of its body, signed out, at a test-only endpoint in the group
/// (<see cref="TestOnlyApiEndpoint"/>), which reads no body, over HTTPS. The app is hosted without a database, with its
/// Data Protection keys in memory, so the tests run without Docker; <see cref="ApiGroupOnSqlServerTests"/> covers
/// signing in.
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

    // With no body, so with no Content-Type either.
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

    // In the form field where antiforgery looks by default when the header has none, as a plain HTML form sends it. The
    // form is refused for its type before the token is looked for.
    [Fact]
    public async Task A_token_in_a_form_field_rather_than_the_header_is_refused()
    {
        using var client = app.CreateHttpsClient();
        var token = await client.FetchTokenAsync();
        using var request = Request("POST", token: null);
        request.Content = new FormUrlEncodedContent([new("__RequestVerificationToken", token)]);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        await response.ReadProblemAsync(HttpStatusCode.UnsupportedMediaType);
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

    // A form post (design.md §9.8 item 3), with everything else a request needs to get through.
    [Theory]
    [MemberData(nameof(StateChangingMethods))]
    public async Task A_form_post_with_a_valid_token_gets_a_415_problem_with_no_code_that_is_never_stored(
        string method)
    {
        using var client = app.CreateHttpsClient();
        var token = await client.FetchTokenAsync();
        using var request = Request(method, token);
        request.Content = new FormUrlEncodedContent([new("name", "value")]);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        using var problem = JsonDocument.Parse(await response.ReadProblemAsync(HttpStatusCode.UnsupportedMediaType));
        Assert.False(problem.RootElement.TryGetProperty("code", out _));
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
    }

    // Each body is JSON text, so only its declared type refuses it; the last declares none.
    [Theory]
    [InlineData("text/plain; charset=utf-8")]
    [InlineData("multipart/form-data; boundary=part")]
    [InlineData("application/problem+json")]
    [InlineData(null)]
    public async Task A_body_that_is_not_declared_application_json_is_refused(string? contentType)
    {
        using var client = app.CreateHttpsClient();
        var token = await client.FetchTokenAsync();
        using var request = Request("POST", token);
        request.Content = Body(contentType, "{}");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        await response.ReadProblemAsync(HttpStatusCode.UnsupportedMediaType);
    }

    [Fact]
    public async Task A_type_other_than_application_json_is_refused_with_an_empty_body_too()
    {
        using var client = app.CreateHttpsClient();
        var token = await client.FetchTokenAsync();
        using var request = Request("POST", token);
        request.Content = Body("text/plain", "");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        await response.ReadProblemAsync(HttpStatusCode.UnsupportedMediaType);
    }

    [Theory]
    [InlineData("application/json")]
    [InlineData("application/json; charset=utf-8")]
    [InlineData("Application/JSON")]
    public async Task A_JSON_body_with_a_token_gets_through(string contentType)
    {
        using var client = app.CreateHttpsClient();
        var token = await client.FetchTokenAsync();
        using var request = Request("POST", token);
        request.Content = Body(contentType, "{}");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    // A JSON body passes the type check, and the token check still refuses the request.
    [Fact]
    public async Task A_JSON_body_without_a_token_is_refused()
    {
        using var client = app.CreateHttpsClient();
        await client.FetchTokenAsync();
        using var request = Request("POST", token: null);
        request.Content = Body("application/json", "{}");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        await response.ReadProblemAsync(HttpStatusCode.BadRequest);
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

    // With no Content-Type when contentType is null.
    private static ByteArrayContent Body(string? contentType, string text)
    {
        var body = new ByteArrayContent(Encoding.UTF8.GetBytes(text));
        body.Headers.ContentType = contentType is null ? null : MediaTypeHeaderValue.Parse(contentType);
        return body;
    }
}
