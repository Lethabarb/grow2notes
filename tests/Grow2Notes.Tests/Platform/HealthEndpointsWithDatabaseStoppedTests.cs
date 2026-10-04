using System.Net;
using Grow2Notes.Tests.Fixtures;

namespace Grow2Notes.Tests.Platform;

/// <summary>
/// A SQL Server container of its own for the tests that stop it, so the shared one in
/// <see cref="SqlServerCollection"/> keeps running.
/// </summary>
[CollectionDefinition]
public sealed class DatabaseStoppedCollection : ICollectionFixture<SqlServerFixture>;

[Collection<DatabaseStoppedCollection>]
public sealed class HealthEndpointsWithDatabaseStoppedTests(SqlServerFixture sqlServer, Grow2NotesFactory factory)
    : IClassFixture<Grow2NotesFactory>
{
    [Fact]
    public async Task Readiness_returns_503_when_the_database_is_unreachable()
    {
        using var client = factory.CreateClient();
        await sqlServer.StopAsync();

        using var response = await client.GetAsync("/healthz/ready", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task Liveness_returns_200_without_calling_the_database()
    {
        using var client = factory.CreateClient();
        await sqlServer.StopAsync();

        // Any database call would fail now, as readiness shows, so a 200 means liveness made none.
        using var response = await client.GetAsync("/healthz", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
