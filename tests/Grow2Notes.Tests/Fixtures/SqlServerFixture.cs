using Testcontainers.MsSql;

namespace Grow2Notes.Tests.Fixtures;

/// <summary>
/// A real SQL Server in a Docker container, started before its collection's first test and removed after its last.
/// Each test collection that declares this fixture gets a container of its own: the integration tests share one
/// through <see cref="SqlServerCollection"/>, and a test that stops its database belongs in a separate collection, so
/// stopping it cannot break the others.
/// </summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    // An exact cumulative update rather than 2022-latest, which moves with every release, so each machine and each CI
    // run tests against the same engine build. Raise it deliberately, in its own change.
    private const string Image = "mcr.microsoft.com/mssql/server:2022-CU26-ubuntu-22.04";

    private readonly MsSqlContainer container = new MsSqlBuilder(Image).Build();

    /// <summary>The <c>sa</c> connection string, with the host port Docker mapped when the container started.</summary>
    public string ConnectionString => container.GetConnectionString();

    /// <summary>
    /// Stops SQL Server, leaving the container for disposal to remove. Start the app first: a stopped container has no
    /// mapped port, so <see cref="ConnectionString"/> throws. Calling it again does nothing. It is not restarted:
    /// Docker can map a different host port on a restart, which would leave the app holding a connection string that
    /// no longer works.
    /// </summary>
    public Task StopAsync() => container.StopAsync(TestContext.Current.CancellationToken);

    public async ValueTask InitializeAsync() => await container.StartAsync(TestContext.Current.CancellationToken);

    public ValueTask DisposeAsync() => container.DisposeAsync();
}
