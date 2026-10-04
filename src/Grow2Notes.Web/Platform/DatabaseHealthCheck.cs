using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Grow2Notes.Web.Platform;

/// <summary>
/// Opens a connection to the application database and runs a trivial query, for <c>/healthz/ready</c>
/// (design.md §10.8). Any exception, including the registration's timeout, is reported by the health check service
/// as unhealthy.
/// </summary>
internal sealed class DatabaseHealthCheck(IConfiguration configuration) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        // Read on every check, not at startup, so a missing or wrong connection string leaves the app live but not
        // ready, rather than stopping it.
        await using var connection = new SqlConnection(configuration.GetConnectionString("Grow2Notes"));
        await connection.OpenAsync(cancellationToken);

        // Opening can hand back a pooled connection without reaching the server; a round trip proves it answers now.
        await using var command = new SqlCommand("SELECT 1", connection);
        await command.ExecuteScalarAsync(cancellationToken);

        return HealthCheckResult.Healthy();
    }
}
