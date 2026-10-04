using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace Grow2Notes.Web.Platform;

/// <summary>
/// The design.md §10.8 health endpoints: <c>/healthz</c> for liveness and <c>/healthz/ready</c> with the database
/// check. Both keep the built-in response: the overall status as plain text (<c>200</c>, or <c>503</c> when
/// unhealthy) with no check names, messages or exception details, and headers that forbid caching.
/// </summary>
internal static class HealthEndpoints
{
    public static IServiceCollection AddHealthEndpoints(this IServiceCollection services)
    {
        // Well inside the minute that App Service waits for a health check response. S0 answers SELECT 1 in
        // milliseconds, so a database that takes longer than this is not ready.
        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("database", timeout: TimeSpan.FromSeconds(5));

        return services;
    }

    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // Runs no checks, so it never opens a database connection and the test environment's free serverless
        // database can auto-pause (design.md §10.3).
        endpoints.MapHealthChecks("/healthz", new HealthCheckOptions { Predicate = static _ => false });

        // Runs every check. Production's App Service health check and its alert use this path (design.md §10.1).
        endpoints.MapHealthChecks("/healthz/ready");

        return endpoints;
    }
}
