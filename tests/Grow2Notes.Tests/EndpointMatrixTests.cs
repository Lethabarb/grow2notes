using Grow2Notes.Tests.Fixtures;
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
/// row of <see cref="EndpointMatrix"/>; and an endpoint with no row, or a row with no endpoint, fails the run. The app
/// is hosted with no test-only endpoint, so the matrix calls the endpoints that it maps in production.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class EndpointMatrixTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    : IClassFixture<Grow2NotesFactory>
{
    private const string TestOnlyPath = "/test-only/no-row";

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
        using var client = factory.CreateClient().SignInAs(caller, sqlServer.Seeded);
        List<string> differences = [];

        foreach (var call in EndpointMatrix.CallsTo(EndpointsOf(factory)))
        {
            if (EndpointMatrix.RowOf(call.Endpoint) is not { } row)
            {
                differences.Add($"{call.Method} {call.Endpoint.RoutePattern.RawText}: no row");
                continue;
            }

            using var request = await call.RequestAsync();
            // Before sending, which makes the request's URI absolute.
            var name = $"{call.Method} {request.RequestUri}";

            using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

            var expected = row.StatusFor(caller);
            if (response.StatusCode != expected)
            {
                differences.Add($"{name}: {(int)response.StatusCode}, not its row's {(int)expected}");
            }
        }

        // Every difference at once, rather than the first.
        Assert.True(differences is [], string.Join(Environment.NewLine, ["Not as the matrix says:", .. differences]));
    }

    private static EndpointDataSource EndpointsOf(WebApplicationFactory<Program> app) =>
        app.Services.GetRequiredService<EndpointDataSource>();

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
}
