using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;

namespace Grow2Notes.Tests.Fixtures;

/// <summary>
/// A real SQL Server in a Docker container, started before its collection's first test and removed after its last, with
/// the app's database created by the app's migrations. Each test collection that declares this fixture gets a container
/// of its own: the integration tests share one through <see cref="SqlServerCollection"/>, and a test that stops its
/// database belongs in a separate collection, so stopping it cannot break the others.
/// </summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    // An exact cumulative update rather than 2022-latest, which moves with every release, so each machine and each CI
    // run tests against the same engine build. Raise it deliberately, in its own change.
    private const string Image = "mcr.microsoft.com/mssql/server:2022-CU26-ubuntu-22.04";

    // The name the app's database has in LocalDB too. The container's own connection string names master, which is a
    // system database.
    private const string Database = "Grow2Notes";

    private readonly MsSqlContainer container = new MsSqlBuilder(Image).Build();

    /// <summary>
    /// The <c>sa</c> connection string of the app's database, with the host port Docker mapped when the container
    /// started. Every test in the collection shares this database, so a test finds its rows by their keys and never
    /// assumes that a table is empty.
    /// </summary>
    public string ConnectionString => ConnectionStringFor(Database);

    /// <summary>
    /// Creates a database on this server for one test's use only, with the app's migrations applied, and returns its
    /// <c>sa</c> connection string. It is for a test that must know every row in a table.
    /// </summary>
    public async Task<string> CreateDatabaseAsync(string name, CancellationToken cancellationToken)
    {
        var connectionString = ConnectionStringFor(name);
        await MigrateAsync(connectionString, cancellationToken);
        return connectionString;
    }

    /// <summary>
    /// Stops SQL Server, leaving the container for disposal to remove. Start the app first: a stopped container has no
    /// mapped port, so <see cref="ConnectionString"/> throws. Calling it again does nothing. It is not restarted:
    /// Docker can map a different host port on a restart, which would leave the app holding a connection string that
    /// no longer works.
    /// </summary>
    public Task StopAsync() => container.StopAsync(TestContext.Current.CancellationToken);

    public async ValueTask InitializeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await container.StartAsync(cancellationToken);
        await CreateDatabaseAsync(Database, cancellationToken);
    }

    public ValueTask DisposeAsync() => container.DisposeAsync();

    /// <summary>
    /// Creates the database if it does not exist and applies the app's migrations to it. The app never migrates itself
    /// (design.md §10.6); a deploy runs the migrations before the new code starts, and this does the same before any
    /// test starts the app, which also lets Data Protection load its keys as the app starts.
    /// </summary>
    private static async Task MigrateAsync(string connectionString, CancellationToken cancellationToken)
    {
        // The context takes Identity's schema version from these options in the application's services, which
        // Program.cs sets to Version3. If the two ever disagree, the model differs from the migrations' snapshot and
        // migrating throws, so they cannot drift apart unnoticed.
        await using var services = new ServiceCollection()
            .Configure<IdentityOptions>(o => o.Stores.SchemaVersion = IdentitySchemaVersions.Version3)
            .BuildServiceProvider();
        var options = new DbContextOptionsBuilder<Grow2NotesDbContext>()
            .UseSqlServer(connectionString)
            .UseApplicationServiceProvider(services)
            .Options;

        // Migrating reads no tenant-owned rows, so it runs with no tenant.
        await using var db = new Grow2NotesDbContext(options, new TenantContext(new HttpContextAccessor()));
        await db.Database.MigrateAsync(cancellationToken);
    }

    private string ConnectionStringFor(string database) =>
        new SqlConnectionStringBuilder(container.GetConnectionString()) { InitialCatalog = database }.ConnectionString;
}
