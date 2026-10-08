using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Grow2Notes.Tests.Fixtures;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests.Platform;

/// <summary>
/// Minimal APIs' built-in validation through the app's whole pipeline, at test-only endpoints: one whose body has
/// required fields, and one whose parameter has a range. The tests host the app without a database, so they run
/// without Docker.
/// </summary>
public sealed class ValidationTests
{
    // Under /api, as every endpoint with a request body will be.
    private const string Path = "/api/test-only/validated";

    [Fact]
    public async Task A_body_without_its_required_fields_gets_a_422_validation_failed_problem_naming_each_field()
    {
        await using var app = new AppWithoutDatabase("Production");
        using var client = app.WithWebHostBuilder(MapTestOnlyEndpointsWithTheBodyDescribed).CreateClient();

        using var response = await client.PostAsJsonAsync(
            Path, new { part = new { } }, TestContext.Current.CancellationToken);

        using var problem = JsonDocument.Parse(await response.ReadProblemAsync(HttpStatusCode.UnprocessableEntity));
        var root = problem.RootElement;
        Assert.Equal("validation.failed", root.GetProperty("code").GetString());

        // ASP.NET Core's type for a 422, not for the 400 that the validation answers by itself.
        Assert.Equal(
            TypedResults.Problem(statusCode: StatusCodes.Status422UnprocessableEntity).ProblemDetails.Type,
            root.GetProperty("type").GetString());

        // Each field as the body's JSON names it, the one in a part of the body by its path, each with its message.
        var errors = root.GetProperty("errors").EnumerateObject().ToList();
        Assert.Equal(["name", "part.value"], errors.Select(field => field.Name).Order(StringComparer.Ordinal));
        Assert.All(errors, field => Assert.Equal(
            JsonValueKind.String, Assert.Single(field.Value.EnumerateArray()).ValueKind));
    }

    [Fact]
    public async Task A_body_with_its_required_fields_reaches_the_endpoint()
    {
        await using var app = new AppWithoutDatabase("Production");
        using var client = app.WithWebHostBuilder(MapTestOnlyEndpointsWithTheBodyDescribed).CreateClient();

        using var response = await client.PostAsJsonAsync(
            Path, new { name = "Name", part = new { value = "Value" } }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    // With the app's own AddValidation call alone, which is what turns validation on for the app's endpoints: the
    // test project's call, which the body tests need, would turn it on by itself.
    [Fact]
    public async Task A_parameter_out_of_its_range_gets_a_422_validation_failed_problem_from_the_apps_own_validation()
    {
        await using var app = new AppWithoutDatabase("Production");
        using var client = app.WithWebHostBuilder(MapTestOnlyEndpoints).CreateClient();

        using var response = await client.GetAsync($"{Path}?count=50", TestContext.Current.CancellationToken);

        using var problem = JsonDocument.Parse(await response.ReadProblemAsync(HttpStatusCode.UnprocessableEntity));
        var root = problem.RootElement;
        Assert.Equal("validation.failed", root.GetProperty("code").GetString());
        var field = Assert.Single(root.GetProperty("errors").EnumerateObject());
        Assert.Equal("count", field.Name);
        Assert.Equal(JsonValueKind.String, Assert.Single(field.Value.EnumerateArray()).ValueKind);
    }

    private static void MapTestOnlyEndpoints(IWebHostBuilder builder) =>
        builder.ConfigureServices(services => services.AddSingleton<IStartupFilter>(new TestOnlyEndpoints()));

    private static void MapTestOnlyEndpointsWithTheBodyDescribed(IWebHostBuilder builder)
    {
        MapTestOnlyEndpoints(builder);

        // The validation source generator describes the request types of the endpoints in its own project, and gives
        // them to that project's AddValidation call. This one is the test project's, for the test-only body, and must
        // stay its only one: the generator names its output the same for every call, so a second fails the build.
        builder.ConfigureServices(services => services.AddValidation());
    }

    // Public, as the app's request types are: the validation source generator passes over a type that is not.
    public sealed record TestOnlyBody([property: Required] string? Name, TestOnlyPart? Part);

    public sealed record TestOnlyPart([property: Required] string? Value);

    /// <summary>
    /// Maps the endpoints ahead of the app's own routing, which then leaves the endpoint already chosen, so they run
    /// where the app's own endpoints do, with the app's validation and problem details.
    /// </summary>
    private sealed class TestOnlyEndpoints : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.UseRouting();
            next(app);
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapPost(Path, (TestOnlyBody _) => TypedResults.NoContent()).AllowAnonymous();
                endpoints.MapGet(Path, ([Range(1, 10)] int count) => TypedResults.NoContent()).AllowAnonymous();
            });
        };
    }
}
