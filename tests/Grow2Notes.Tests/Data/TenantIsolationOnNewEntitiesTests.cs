using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Grow2Notes.Tests.Data;

/// <summary>
/// Tenant isolation on SQL Server (design.md §5.9 items 1 to 4): the query filter, the save interceptor, the
/// concurrency token on <c>OrganisationId</c> and a composite foreign key, on two stand-in tenant-owned entities, since
/// the one in the app's model, <c>AuditEvent</c>, has no foreign key and is never changed or deleted (design.md §5.8).
/// They are in a test-only context with a database of its own. Each test has two new organisations, A and B, so the
/// tests can share that database.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class TenantIsolationOnNewEntitiesTests(TenantIsolationOnNewEntitiesTests.StandInDatabase database)
    : IClassFixture<TenantIsolationOnNewEntitiesTests.StandInDatabase>
{
    private readonly Guid organisationA = Guid.NewGuid();

    private readonly Guid organisationB = Guid.NewGuid();

    [Fact]
    public async Task With_no_tenant_a_query_on_a_tenant_owned_table_throws()
    {
        await using var db = Open(NoTenant());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            db.Parents.ToListAsync(TestContext.Current.CancellationToken));

        Assert.StartsWith("There is no tenant", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task With_no_tenant_a_save_that_adds_a_tenant_owned_row_throws_and_writes_nothing()
    {
        var before = await StoredRowsAsync();
        await using var db = Open(NoTenant());
        db.Parents.Add(new StandInParent { Name = "Added" });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            db.SaveChangesAsync(TestContext.Current.CancellationToken));

        Assert.StartsWith("There is no tenant", ex.Message, StringComparison.Ordinal);
        Assert.Equal(before, await StoredRowsAsync());
    }

    [Fact]
    public async Task A_query_returns_only_the_tenant_s_rows()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var parentOfA = await AddParentWithChildAsync(organisationA);
        await AddParentWithChildAsync(organisationB);
        var tenant = NoTenant();
        await using var db = Open(tenant);
        using var useA = tenant.Use(organisationA);

        Assert.Equal([parentOfA.Id], await db.Parents.Select(parent => parent.Id).ToListAsync(cancellationToken));
        Assert.Equal(
            [parentOfA.Children[0].Id], await db.Children.Select(child => child.Id).ToListAsync(cancellationToken));
    }

    [Fact]
    public async Task An_added_row_with_no_organisation_is_stamped_with_the_tenant()
    {
        var parent = new StandInParent { Name = "Added" };
        var tenant = NoTenant();
        await using (var db = Open(tenant))
        using (tenant.Use(organisationA))
        {
            db.Parents.Add(parent);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var stored = Assert.Single(await StoredRowsAsync(), row => row.Id == parent.Id);
        Assert.Equal(organisationA, stored.OrganisationId);
    }

    [Fact]
    public async Task A_parent_and_its_child_added_together_with_no_organisation_are_both_stamped_with_the_tenant()
    {
        var child = new StandInChild { Name = "Added" };
        var parent = new StandInParent { Name = "Added", Children = { child } };
        var tenant = NoTenant();
        await using (var db = Open(tenant))
        using (tenant.Use(organisationA))
        {
            db.Parents.Add(parent);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // When the graph was added, EF Core copied the parent's empty organisation into the child's foreign key, so the
        // stamp had to reach both alike for SQL Server to take the child.
        var stored = await StoredRowsAsync();
        Assert.Equal(new StoredRow(parent.Id, organisationA, null, "Added"), stored.Single(row => row.Id == parent.Id));
        Assert.Equal(
            new StoredRow(child.Id, organisationA, parent.Id, "Added"), stored.Single(row => row.Id == child.Id));
    }

    [Fact]
    public async Task A_row_added_with_another_organisation_s_ID_is_refused_and_nothing_is_written()
    {
        var before = await StoredRowsAsync();
        var tenant = NoTenant();
        await using var db = Open(tenant);
        using var useA = tenant.Use(organisationA);
        db.Parents.AddRange(
            new StandInParent { Name = "Added" },
            new StandInParent { OrganisationId = organisationB, Name = "Added" });

        await AssertRefusedAsync(db, $"{nameof(StandInParent)} row to be added");

        Assert.Equal(before, await StoredRowsAsync());
    }

    [Fact]
    public async Task A_row_of_the_tenant_changed_to_another_organisation_s_ID_is_refused_and_nothing_is_written()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var parentOfA = await AddParentWithChildAsync(organisationA);
        var before = await StoredRowsAsync();
        var tenant = NoTenant();
        await using var db = Open(tenant);
        using var useA = tenant.Use(organisationA);
        var parent = await db.Parents.SingleAsync(row => row.Id == parentOfA.Id, cancellationToken);
        parent.Name = "Changed";

        // The child, because a parent's OrganisationId is part of its alternate key, which EF Core itself refuses to
        // change.
        var child = await db.Children.SingleAsync(row => row.Id == parentOfA.Children[0].Id, cancellationToken);
        child.OrganisationId = organisationB;
        child.Name = "Changed";

        await AssertRefusedAsync(db, $"{nameof(StandInChild)} row to be changed");

        Assert.Equal(before, await StoredRowsAsync());
    }

    [Fact]
    public async Task Another_organisation_s_row_updated_with_the_tenant_s_ID_matches_no_row_and_nothing_is_written()
    {
        var parentOfB = await AddParentWithChildAsync(organisationB);
        var before = await StoredRowsAsync();
        var tenant = NoTenant();
        await using var db = Open(tenant);
        using var useA = tenant.Use(organisationA);

        // Never loaded, so the query filter never saw it, and it carries A's ID, so the interceptor passes it. The
        // concurrency token puts A's ID in the UPDATE's WHERE, which B's row does not match.
        db.Parents.Update(new StandInParent { Id = parentOfB.Id, OrganisationId = organisationA, Name = "Hijacked" });

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
            db.SaveChangesAsync(TestContext.Current.CancellationToken));

        Assert.Equal(before, await StoredRowsAsync());
    }

    [Fact]
    public async Task Another_organisation_s_row_deleted_with_the_tenant_s_ID_matches_no_row_and_nothing_is_written()
    {
        var parentOfB = await AddParentWithChildAsync(organisationB);
        var before = await StoredRowsAsync();
        var tenant = NoTenant();
        await using var db = Open(tenant);
        using var useA = tenant.Use(organisationA);

        // The child, because the foreign key from it would stop the delete of B's parent, token or not.
        db.Children.Remove(
            new StandInChild { Id = parentOfB.Children[0].Id, OrganisationId = organisationA, Name = "Hijacked" });

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
            db.SaveChangesAsync(TestContext.Current.CancellationToken));

        Assert.Equal(before, await StoredRowsAsync());
    }

    [Fact]
    public async Task The_tenant_s_own_rows_loaded_through_the_query_filter_are_updated_and_deleted()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var parentOfA = await AddParentWithChildAsync(organisationA);
        var childOfA = parentOfA.Children[0];
        var tenant = NoTenant();
        await using (var db = Open(tenant))
        using (tenant.Use(organisationA))
        {
            var parent = await db.Parents.SingleAsync(row => row.Id == parentOfA.Id, cancellationToken);
            parent.Name = "Changed";
            db.Children.Remove(await db.Children.SingleAsync(row => row.Id == childOfA.Id, cancellationToken));
            await db.SaveChangesAsync(cancellationToken);
        }

        var stored = await StoredRowsAsync();
        Assert.Equal(
            new StoredRow(parentOfA.Id, organisationA, null, "Changed"), stored.Single(row => row.Id == parentOfA.Id));
        Assert.DoesNotContain(stored, row => row.Id == childOfA.Id);
    }

    [Fact]
    public async Task A_child_of_the_tenant_that_points_at_another_organisation_s_parent_is_refused_by_SQL_Server()
    {
        var parentOfB = await AddParentWithChildAsync(organisationB);
        var tenant = NoTenant();
        await using var db = Open(tenant);
        using var useA = tenant.Use(organisationA);

        // It carries A's ID, so the interceptor passes it as one of the tenant's rows. The parent table has no key
        // (A, the ID of B's parent), so only the composite foreign key stops the link.
        db.Children.Add(new StandInChild { OrganisationId = organisationA, ParentId = parentOfB.Id, Name = "Added" });
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() =>
            db.SaveChangesAsync(TestContext.Current.CancellationToken));

        // 547: the statement conflicted with a constraint.
        var refusal = Assert.IsType<SqlException>(ex.InnerException);
        Assert.Equal(547, refusal.Number);
        Assert.Contains(
            "\"FK_StandInChild_StandInParent_OrganisationId_ParentId\"", refusal.Message, StringComparison.Ordinal);
    }

    private static TenantContext NoTenant() => new(new HttpContextAccessor());

    // Refused by the interceptor, which names the first row it refuses.
    private static async Task AssertRefusedAsync(StandInContext db, string refused)
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            db.SaveChangesAsync(TestContext.Current.CancellationToken));

        Assert.StartsWith($"Nothing was saved: {refused} ", ex.Message, StringComparison.Ordinal);
    }

    private StandInContext Open(ITenantContext tenant) => new(database.ConnectionString, tenant);

    // Added as the organisation itself would add them: saved under its own tenant, through the interceptor.
    private async Task<StandInParent> AddParentWithChildAsync(Guid organisationId)
    {
        var parent = new StandInParent
        {
            OrganisationId = organisationId,
            Name = "Seeded",
            Children = { new StandInChild { Name = "Seeded" } },
        };
        var tenant = NoTenant();
        await using var db = Open(tenant);
        db.Parents.Add(parent);
        using (tenant.Use(organisationId))
        {
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        return parent;
    }

    // Every row of both tables, read in SQL, so that neither the query filter nor a context's tracked rows decide
    // what is seen.
    private async Task<List<StoredRow>> StoredRowsAsync()
    {
        await using var db = Open(NoTenant());
        return await db.Database.SqlQueryRaw<StoredRow>("""
            SELECT [Id], [OrganisationId], NULL AS [ParentId], [Name] FROM [StandInParent]
            UNION ALL
            SELECT [Id], [OrganisationId], [ParentId], [Name] FROM [StandInChild]
            ORDER BY [Id]
            """).ToListAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// The stand-ins' database, created from their model before the class's first test, since the app's migrations
    /// know nothing of them, and dropped after its last.
    /// </summary>
    public sealed class StandInDatabase(SqlServerFixture sqlServer) : IAsyncLifetime
    {
        internal string ConnectionString => sqlServer.ConnectionStringFor("TenantIsolationOnNewEntities");

        public async ValueTask InitializeAsync()
        {
            await using var db = new StandInContext(ConnectionString, NoTenant());
            await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            await using var db = new StandInContext(ConnectionString, NoTenant());
            await db.Database.EnsureDeletedAsync(TestContext.Current.CancellationToken);
        }
    }

    /// <summary>
    /// Set up as the app's context is, with its conventions and with the tenant query filter and save interceptor
    /// reading this context's own tenant, and linked as design.md §5.10 links tenant-owned entities: the child's
    /// <c>(OrganisationId, ParentId)</c> is a foreign key to the parent's alternate key <c>(OrganisationId, Id)</c>.
    /// </summary>
    private sealed class StandInContext(string connectionString, ITenantContext tenant) : DbContext
    {
        public DbSet<StandInParent> Parents => Set<StandInParent>();

        public DbSet<StandInChild> Children => Set<StandInChild>();

        private Guid TenantId => tenant.OrganisationId;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) =>
            optionsBuilder.UseSqlServer(connectionString).AddInterceptors(new TenantSaveChangesInterceptor(tenant));

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder) =>
            ModelConventions.Apply(configurationBuilder);

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<StandInParent>(parent =>
            {
                parent.HasAlternateKey(p => new { p.OrganisationId, p.Id });
                parent.HasMany(p => p.Children).WithOne()
                    .HasForeignKey(c => new { c.OrganisationId, c.ParentId })
                    .HasPrincipalKey(p => new { p.OrganisationId, p.Id });
            });

            TenantQueryFilter.Apply(modelBuilder, () => TenantId);
        }
    }

    private sealed class StandInParent : ITenantOwned
    {
        public Guid Id { get; set; }

        public Guid OrganisationId { get; set; }

        public required string Name { get; set; }

        public List<StandInChild> Children { get; } = [];
    }

    private sealed class StandInChild : ITenantOwned
    {
        public Guid Id { get; set; }

        public Guid OrganisationId { get; set; }

        public Guid ParentId { get; set; }

        public required string Name { get; set; }
    }

    private sealed record StoredRow(Guid Id, Guid OrganisationId, Guid? ParentId, string Name);
}
