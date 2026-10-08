using System.Net;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests;

/// <summary>
/// The endpoint matrix (design.md §2, §9.1, §9.2): every endpoint in the app's <c>EndpointDataSource</c>, called
/// signed out, as seeded organisation A's worker and manager, and as B's manager, answers each with the status in its
/// row of <see cref="EndpointMatrix"/>; and an endpoint with no row, or a row with no endpoint, fails the run. A call
/// that changes something sends the caller's antiforgery token, and is made twice more, without it and with a
/// form-encoded body, which the <c>/api</c> group refuses (§9.8). The app is hosted with no test-only endpoint, so the
/// matrix calls the endpoints that it maps in production.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class EndpointMatrixTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    : IClassFixture<Grow2NotesFactory>
{
    private const string TestOnlyPath = "/test-only/no-row";

    // Mapped in the /api group, whose prefix its route pattern then has.
    private const string RefusingInTheGroupRoute = "/test-only/refusing-in-the-group";

    private const string OutsideTheGroupPath = "/api/test-only/outside-the-group";

    private const string RefusingOutsideTheGroupPath = "/api/test-only/refusing-outside-the-group";

    public static TheoryData<Caller> Callers { get; } = new(Enum.GetValues<Caller>());

    [Fact]
    public void Every_endpoint_has_a_row_and_every_row_an_endpoint()
    {
        var (endpointsWithNoRow, rowsWithNoEndpoint) = EndpointMatrix.Gaps(EndpointsOf(factory));

        Assert.Empty(endpointsWithNoRow);
        Assert.Empty(rowsWithNoEndpoint);
    }

    [Fact]
    public async Task An_endpoint_mapped_without_a_row_fails_the_run()
    {
        await using var app = factory.WithWebHostBuilder(builder => builder.ConfigureServices(
            services => services.AddSingleton<IStartupFilter>(new TestOnlyEndpoint())));

        var (endpointsWithNoRow, rowsWithNoEndpoint) = EndpointMatrix.Gaps(EndpointsOf(app));

        Assert.Equal([$"GET {TestOnlyPath}"], endpointsWithNoRow);
        Assert.Empty(rowsWithNoEndpoint);
    }

    [Theory]
    [MemberData(nameof(Callers))]
    public async Task Every_endpoint_answers_the_caller_with_the_status_in_its_row(Caller caller)
    {
        using var client = factory.CreateHttpsClient().SignInAs(caller, sqlServer.Seeded);
        var token = await client.FetchTokenAsync();
        List<string> differences = [];

        foreach (var call in EndpointMatrix.CallsTo(EndpointsOf(factory)))
        {
            if (EndpointMatrix.RowOf(call.Endpoint) is { } row)
            {
                differences.AddRange(await DifferencesAsync(client, token, call, row.StatusFor(caller)));
            }
            else
            {
                differences.Add($"{call.Method} {call.Endpoint.RoutePattern.RawText}: no row");
            }
        }

        // Every difference at once, rather than the first.
        Assert.True(differences is [], string.Join(Environment.NewLine, ["Not as the matrix says:", .. differences]));
    }

    // Each endpoint gets the token and answers as expected with it, but only those in the /api group refuse a call
    // without it, or with a form body. Two refuse every caller with a 401 of their own, as sign-in refuses bad
    // credentials, though anyone may call them: the one in the group answers 400 without a token and 415 with a form
    // body, and the one outside it 401 to both, failing the run.
    [Fact]
    public async Task An_endpoint_that_changes_something_outside_the_api_group_fails_the_run()
    {
        Dictionary<string, HttpStatusCode> statuses = new()
        {
            [TestOnlyApiEndpoint.Path] = HttpStatusCode.NoContent,
            ["/api" + RefusingInTheGroupRoute] = HttpStatusCode.Unauthorized,
            [OutsideTheGroupPath] = HttpStatusCode.NoContent,
            [RefusingOutsideTheGroupPath] = HttpStatusCode.Unauthorized,
        };
        await using var app = factory.WithWebHostBuilder(builder =>
        {
            TestOnlyApiEndpoint.MapIn(builder);
            builder.ConfigureServices(
                services => services.AddSingleton<IStartupFilter>(new TestOnlyEndpointsThatChangeSomething()));
        });
        using var client = app.CreateHttpsClient();
        var token = await client.FetchTokenAsync();
        List<string> differences = [];

        foreach (var call in EndpointMatrix.CallsTo(EndpointsOf(app)))
        {
            if (statuses.TryGetValue(call.Endpoint.RoutePattern.RawText ?? "", out var status))
            {
                differences.AddRange(await DifferencesAsync(client, token, call, status));
            }
        }

        Assert.Equal(
            [
                $"POST {OutsideTheGroupPath} with a form body: 204, not 415",
                $"POST {OutsideTheGroupPath} without a token: 204, not 400",
                $"POST {RefusingOutsideTheGroupPath} with a form body: 401, not 415",
                $"POST {RefusingOutsideTheGroupPath} without a token: 401, not 400",
            ],
            differences.Order(StringComparer.Ordinal));
    }

    private static EndpointDataSource EndpointsOf(WebApplicationFactory<Program> app) =>
        app.Services.GetRequiredService<EndpointDataSource>();

    /// <summary>
    /// Makes <paramref name="call"/> with <paramref name="client"/>, and lists how its status differs from
    /// <paramref name="expected"/>. A call that changes something sends <paramref name="token"/>, and is made twice
    /// more, each refused before the endpoint runs: without the token, which the <c>/api</c> group's filter answers
    /// <c>400</c>; and with it but with a form-encoded body in place of the row's, which the filter answers <c>415</c>,
    /// as minimal APIs' binding does first at an endpoint that reads JSON. Of a row's statuses only authorization's
    /// come before both (binding and validation run first too, but a row's requests are valid), so at an endpoint that
    /// is not <c>AllowAnonymous</c>, a row's <c>401</c> or <c>403</c> is the answer to both calls too; at one that is,
    /// such as sign-in (design.md §6.2), the endpoint gives it, after the filter.
    /// </summary>
    private static async Task<List<string>> DifferencesAsync(
        HttpClient client, string token, EndpointCall call, HttpStatusCode expected)
    {
        List<string> differences = [];
        await SendAsync(call.ChangesState ? token : null, expected, "");
        if (call.ChangesState)
        {
            var refusedByAuthorization = expected is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
                && call.Endpoint.Metadata.GetMetadata<IAllowAnonymous>() is null;
            await SendAsync(
                withToken: null, refusedByAuthorization ? expected : HttpStatusCode.BadRequest, " without a token");

            // As a plain HTML form posts it (design.md §9.8 item 3).
            await SendAsync(
                token, refusedByAuthorization ? expected : HttpStatusCode.UnsupportedMediaType, " with a form body",
                new FormUrlEncodedContent([new("name", "value")]));
        }

        return differences;

        // The row's builder is asked again for each request, so each gets a record of its own to act on.
        async Task SendAsync(string? withToken, HttpStatusCode status, string how, HttpContent? body = null)
        {
            using var request = await call.RequestAsync();
            if (withToken is not null)
            {
                request.Headers.Add(AntiforgeryTokens.Header, withToken);
            }

            if (body is not null)
            {
                request.Content?.Dispose();
                request.Content = body;
            }

            // Before sending, which makes the request's URI absolute.
            var name = $"{call.Method} {request.RequestUri}{how}";

            using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

            if (response.StatusCode != status)
            {
                differences.Add($"{name}: {(int)response.StatusCode}, not {(int)status}");
            }
        }
    }

    /// <summary>
    /// Maps an endpoint with no row, as a story that forgot to add one would. Anyone may call it, and still it needs a
    /// row.
    /// </summary>
    private sealed class TestOnlyEndpoint : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.UseRouting();
            next(app);
            app.UseEndpoints(endpoints => endpoints.MapGet(TestOnlyPath, () => TypedResults.NoContent())
                .AllowAnonymous());
        };
    }

    /// <summary>
    /// Maps POST endpoints that anyone may call: one outside the <c>/api</c> group that answers <c>204</c>, as a story
    /// that mapped an API endpoint outside the group would; and, in the group and outside it, one that refuses every
    /// caller with a <c>401</c> of its own, as sign-in refuses bad credentials.
    /// </summary>
    private sealed class TestOnlyEndpointsThatChangeSomething : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.UseRouting();
            next(app);
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapApiGroup().MapPost(RefusingInTheGroupRoute, () => TypedResults.Unauthorized())
                    .AllowAnonymous();
                endpoints.MapPost(OutsideTheGroupPath, () => TypedResults.NoContent()).AllowAnonymous();
                endpoints.MapPost(RefusingOutsideTheGroupPath, () => TypedResults.Unauthorized()).AllowAnonymous();
            });
        };
    }
}
