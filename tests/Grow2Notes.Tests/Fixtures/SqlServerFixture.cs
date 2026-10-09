using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;

namespace Grow2Notes.Tests.Fixtures;

/// <summary>
/// A real SQL Server in a Docker container, started before its collection's first test and removed after its last, with
/// the app's database created by the app's migrations and holding the <see cref="Seeded"/> organisations. Each test
/// collection that declares this fixture gets a container of its own: the integration tests share one through
/// <see cref="SqlServerCollection"/>, and a test that stops its database belongs in a separate collection, so stopping
/// it cannot break the others.
/// </summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    // An exact cumulative update rather than 2022-latest, which moves with every release, so each machine and each CI
    // run tests against the same engine build. Raise it deliberately, in its own change.
    private const string Image = "mcr.microsoft.com/mssql/server:2022-CU26-ubuntu-22.04";

    // The name the app's database has in LocalDB too. The container's own connection string names master, which is a
    // system database.
    private const string Database = "Grow2Notes";

    private static readonly DateTime SeededAtUtc = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly MsSqlContainer container = new MsSqlBuilder(Image).Build();

    /// <summary>
    /// The <c>sa</c> connection string of the app's database, with the host port Docker mapped when the container
    /// started. Every test in the collection shares this database, so a test finds its rows by their keys and never
    /// assumes that a table is empty.
    /// </summary>
    public string ConnectionString => ConnectionStringFor(Database);

    /// <summary>
    /// The IDs of the two made-up organisations in the app's database, each with an Active manager and an Active
    /// worker, which are there before the first test. Tests leave the seeded rows as they are (tests/README.md).
    /// </summary>
    public SeededOrganisations Seeded { get; private set; } = null!;

    /// <summary>
    /// Creates a database on this server for one test's use only, with the app's migrations applied and nothing
    /// seeded, and returns its <c>sa</c> connection string. It is for a test that must know every row in a table.
    /// </summary>
    public async Task<string> CreateDatabaseAsync(string name, CancellationToken cancellationToken)
    {
        var connectionString = ConnectionStringFor(name);
        await using var services = AppDataServices(connectionString);
        await MigrateAsync(services, cancellationToken);
        return connectionString;
    }

    /// <summary>
    /// The <c>sa</c> connection string of a database named <paramref name="database"/> on this server, which need not
    /// exist yet. It is for a test-only context, whose test creates the database from that context's model.
    /// </summary>
    public string ConnectionStringFor(string database) =>
        new SqlConnectionStringBuilder(container.GetConnectionString()) { InitialCatalog = database }.ConnectionString;

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
        await using var services = AppDataServices(ConnectionString);
        await MigrateAsync(services, cancellationToken);
        Seeded = await SeedAsync(services, cancellationToken);
    }

    public ValueTask DisposeAsync() => container.DisposeAsync();

    /// <summary>
    /// The app's context on the database at <paramref name="connectionString"/>, with what migrating and seeding use
    /// registered as Program.cs registers it: the tenant, and Identity's user manager and store at Program.cs's schema
    /// version. Neither uses the sign-in manager, the claims factory or the session cookie, so they are left out.
    /// </summary>
    private static ServiceProvider AppDataServices(string connectionString)
    {
        var services = new ServiceCollection();
        services.AddDbContext<Grow2NotesDbContext>(options => options.UseSqlServer(connectionString));

        // The context takes Identity's schema version from these options, which Program.cs sets to Version3. If the two
        // ever disagree, the model differs from the migrations' snapshot and migrating throws, so they cannot drift
        // apart unnoticed.
        services.AddIdentityCore<ApplicationUser>(o => o.Stores.SchemaVersion = IdentitySchemaVersions.Version3)
            .AddEntityFrameworkStores<Grow2NotesDbContext>();

        // Outside a request, so with no tenant until a Use block sets one.
        services.AddHttpContextAccessor();
        services.AddScoped<ITenantContext, TenantContext>();
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Creates the database if it does not exist and applies the app's migrations to it. The app never migrates itself
    /// (design.md §10.6); a deploy runs the migrations before the new code starts, and this does the same before any
    /// test starts the app, which also lets Data Protection load its keys as the app starts.
    /// </summary>
    private static async Task MigrateAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        // Migrating reads no tenant-owned rows, so it runs with no tenant.
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        await db.Database.MigrateAsync(cancellationToken);
    }

    /// <summary>
    /// Adds the made-up organisations and users (A35) of the two-organisation tests (design.md §5.9 item 7). The users
    /// are created through <see cref="UserManager{TUser}"/>, as the app creates accounts, so each has the normalised
    /// email and user name and the security stamp that Identity reads when it signs someone in.
    /// </summary>
    private static async Task<SeededOrganisations> SeedAsync(
        IServiceProvider services, CancellationToken cancellationToken)
    {
        // An organisation is the tenant, not tenant-owned, and AspNetUsers is not filtered, so this needs no tenant.
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        return new(
            await AddOrganisationAsync("Seeded organisation A",
                ("Jordan Ellis", "jordan.ellis@example.org"), ("Casey Nguyen", "casey.nguyen@example.org")),
            await AddOrganisationAsync("Seeded organisation B",
                ("Morgan Reid", "morgan.reid@example.org"), ("Riley Chen", "riley.chen@example.org")));

        async Task<SeededOrganisation> AddOrganisationAsync(
            string name, (string Name, string Email) manager, (string Name, string Email) worker)
        {
            var organisation = new Organisation { Name = name, CreatedAtUtc = SeededAtUtc };
            db.Organisations.Add(organisation);
            await db.SaveChangesAsync(cancellationToken);

            // The manager stands in for the one the admin bootstrap command invites, so the manager invites the worker.
            var managerId = await AddUserAsync(organisation.Id, UserRole.Manager, manager, invitedByUserId: null);
            var workerId = await AddUserAsync(organisation.Id, UserRole.Worker, worker, invitedByUserId: managerId);
            return new(organisation.Id, managerId, workerId);
        }

        async Task<Guid> AddUserAsync(
            Guid organisationId, UserRole role, (string Name, string Email) person, Guid? invitedByUserId)
        {
            // As setup completion leaves an account (design.md §8.1), apart from the passkey or password it adds.
            var user = new ApplicationUser
            {
                OrganisationId = organisationId,
                DisplayName = person.Name,
                Role = role,
                Status = UserStatus.Active,
                InvitedAtUtc = SeededAtUtc,
                InvitedByUserId = invitedByUserId,
                ActivatedAtUtc = SeededAtUtc,
                UserName = person.Email,
                Email = person.Email,
                EmailConfirmed = true,
            };
            var result = await userManager.CreateAsync(user);
            return result.Succeeded
                ? user.Id
                : throw new InvalidOperationException(
                    $"Identity refused a seeded user: {string.Join(" ", result.Errors.Select(e => e.Code))}.");
        }
    }
}
