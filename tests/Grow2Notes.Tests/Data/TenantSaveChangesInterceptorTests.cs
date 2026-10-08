using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Grow2Notes.Tests.Data;

/// <summary>
/// The tenant save interceptor on a stand-in context, through both <c>SaveChanges</c> and <c>SaveChangesAsync</c>. A
/// second interceptor ends each save where the database would take over, so these tests need no database.
/// </summary>
public sealed class TenantSaveChangesInterceptorTests
{
    private static readonly Guid OrganisationA = Guid.Parse("0b4c1f6e-7a52-4c1d-9e3a-2f8d5b6a7c01");

    private static readonly Guid OrganisationB = Guid.Parse("5d9e2a7b-3c41-4f8e-8b6d-1a2c3e4f5a02");

    public static TheoryData<bool> BothSaves { get; } = [false, true];

    [Theory]
    [MemberData(nameof(BothSaves))]
    public async Task An_added_row_with_no_organisation_is_stamped_with_the_tenant(bool saveAsync)
    {
        var tenant = NoTenant();
        using var db = new StandInContext(tenant);
        var unstamped = new Visit();
        var ofTheTenant = new Visit { OrganisationId = OrganisationA };
        db.Visits.AddRange(unstamped, ofTheTenant);

        using (tenant.Use(OrganisationA))
        {
            await SaveAsync(db, saveAsync);
        }

        Assert.Equal(1, db.Store.Saves);
        Assert.Equal(OrganisationA, unstamped.OrganisationId);
        Assert.Equal(OrganisationA, ofTheTenant.OrganisationId);
    }

    [Theory]
    [MemberData(nameof(BothSaves))]
    public async Task The_tenant_s_rows_are_changed_and_deleted(bool saveAsync)
    {
        var tenant = NoTenant();
        using var db = new StandInContext(tenant);
        var changed = Track(db, OrganisationA);
        var deleted = Track(db, OrganisationA);
        changed.Purpose = "Changed";
        db.Visits.Remove(deleted);

        using (tenant.Use(OrganisationA))
        {
            await SaveAsync(db, saveAsync);
        }

        Assert.Equal(1, db.Store.Saves);
    }

    [Theory]
    [MemberData(nameof(BothSaves))]
    public async Task A_row_added_with_another_organisation_s_ID_is_refused_and_no_row_is_stamped(bool saveAsync)
    {
        var tenant = NoTenant();
        using var db = new StandInContext(tenant);
        var unstamped = new Visit();
        db.Visits.AddRange(unstamped, new Visit { OrganisationId = OrganisationB });

        using (tenant.Use(OrganisationA))
        {
            await AssertRefusedAsync(db, saveAsync, "added");
        }

        Assert.Equal(Guid.Empty, unstamped.OrganisationId);
    }

    [Theory]
    [MemberData(nameof(BothSaves))]
    public async Task A_row_of_another_organisation_that_is_changed_is_refused(bool saveAsync)
    {
        var tenant = NoTenant();
        using var db = new StandInContext(tenant);
        Track(db, OrganisationB).Purpose = "Changed";

        using (tenant.Use(OrganisationA))
        {
            await AssertRefusedAsync(db, saveAsync, "changed");
        }
    }

    [Theory]
    [MemberData(nameof(BothSaves))]
    public async Task A_row_of_the_tenant_moved_to_another_organisation_is_refused(bool saveAsync)
    {
        var tenant = NoTenant();
        using var db = new StandInContext(tenant);
        Track(db, OrganisationA).OrganisationId = OrganisationB;

        using (tenant.Use(OrganisationA))
        {
            await AssertRefusedAsync(db, saveAsync, "changed");
        }
    }

    [Theory]
    [MemberData(nameof(BothSaves))]
    public async Task A_row_of_another_organisation_moved_to_the_tenant_is_refused(bool saveAsync)
    {
        var tenant = NoTenant();
        using var db = new StandInContext(tenant);
        Track(db, OrganisationB).OrganisationId = OrganisationA;

        using (tenant.Use(OrganisationA))
        {
            await AssertRefusedAsync(db, saveAsync, "changed");
        }
    }

    [Theory]
    [MemberData(nameof(BothSaves))]
    public async Task A_row_of_another_organisation_that_is_deleted_is_refused(bool saveAsync)
    {
        var tenant = NoTenant();
        using var db = new StandInContext(tenant);
        db.Visits.Remove(Track(db, OrganisationB));

        using (tenant.Use(OrganisationA))
        {
            await AssertRefusedAsync(db, saveAsync, "deleted");
        }
    }

    [Theory]
    [MemberData(nameof(BothSaves))]
    public async Task A_save_that_adds_changes_or_deletes_no_tenant_owned_row_needs_no_tenant(bool saveAsync)
    {
        using var db = new StandInContext(NoTenant());
        Track(db, OrganisationB);
        db.Regions.Add(new Region());

        await SaveAsync(db, saveAsync);

        Assert.Equal(1, db.Store.Saves);
    }

    [Theory]
    [MemberData(nameof(BothSaves))]
    public async Task A_save_that_adds_a_tenant_owned_row_with_no_tenant_throws(bool saveAsync)
    {
        using var db = new StandInContext(NoTenant());
        db.Visits.Add(new Visit());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => SaveAsync(db, saveAsync));

        Assert.StartsWith("There is no tenant", ex.Message, StringComparison.Ordinal);
        Assert.Equal(0, db.Store.Saves);
    }

    private static TenantContext NoTenant() => new(new HttpContextAccessor());

    // As if loaded from the database: tracked as Unchanged, with the organisation it was loaded with as its original
    // value.
    private static Visit Track(StandInContext db, Guid organisationId)
    {
        var visit = new Visit { Id = Guid.NewGuid(), OrganisationId = organisationId, Purpose = "Loaded" };
        db.Visits.Attach(visit);
        return visit;
    }

    private static async Task SaveAsync(StandInContext db, bool saveAsync)
    {
        if (saveAsync)
        {
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        else
        {
            db.SaveChanges();
        }
    }

    private static async Task AssertRefusedAsync(StandInContext db, bool saveAsync, string verb)
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => SaveAsync(db, saveAsync));

        Assert.StartsWith(
            $"Nothing was saved: {nameof(Visit)} row to be {verb} ", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(OrganisationA.ToString(), ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(OrganisationB.ToString(), ex.Message, StringComparison.OrdinalIgnoreCase);

        // The save stopped before it reached the point where the database would be written.
        Assert.Equal(0, db.Store.Saves);
    }

    private sealed class StandInContext(ITenantContext tenant) : DbContext
    {
        public DbSet<Visit> Visits => Set<Visit>();

        public DbSet<Region> Regions => Set<Region>();

        public InsteadOfTheDatabase Store { get; } = new();

        // The tenant interceptor first, so it sees each save before the stand-in for the database ends it.
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) =>
            optionsBuilder.UseSqlServer().AddInterceptors(new TenantSaveChangesInterceptor(tenant), Store);
    }

    /// <summary>Ends each save that reaches it, as though the database had written it, and counts them.</summary>
    private sealed class InsteadOfTheDatabase : SaveChangesInterceptor
    {
        public int Saves { get; private set; }

        public override InterceptionResult<int> SavingChanges(
            DbContextEventData eventData, InterceptionResult<int> result)
        {
            Saves++;
            return InterceptionResult<int>.SuppressWithResult(0);
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(SavingChanges(eventData, result));
        }
    }

    private sealed class Visit : ITenantOwned
    {
        public Guid Id { get; set; }

        public Guid OrganisationId { get; set; }

        public string Purpose { get; set; } = "";
    }

    private sealed class Region
    {
        public Guid Id { get; set; }
    }
}
