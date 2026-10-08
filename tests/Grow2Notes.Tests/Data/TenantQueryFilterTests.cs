using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Grow2Notes.Tests.Data;

/// <summary>
/// The tenant query filter on a stand-in context, since no entity in the app's model is tenant-owned yet. Building a
/// model and writing a query's SQL open no connection, so these tests need no database.
/// </summary>
public sealed class TenantQueryFilterTests
{
    private static readonly Guid OrganisationA = Guid.Parse("0b4c1f6e-7a52-4c1d-9e3a-2f8d5b6a7c01");

    private static readonly Guid OrganisationB = Guid.Parse("5d9e2a7b-3c41-4f8e-8b6d-1a2c3e4f5a02");

    [Fact]
    public void Tenant_owned_entity_types_alone_get_the_named_Tenant_filter_and_building_the_model_needs_no_tenant()
    {
        using var db = new StandInContext(NoTenant());

        var visit = db.Model.FindEntityType(typeof(Visit))!;
        var region = db.Model.FindEntityType(typeof(Region))!;

        Assert.Equal(TenantQueryFilter.Name, Assert.Single(visit.GetDeclaredQueryFilters()).Key);
        Assert.Empty(region.GetDeclaredQueryFilters());
    }

    [Fact]
    public void Each_context_queries_as_its_own_tenant_though_they_share_one_model()
    {
        var tenantA = NoTenant();
        var tenantB = NoTenant();
        using var dbA = new StandInContext(tenantA);
        using var dbB = new StandInContext(tenantB);

        // Before either has a tenant, so every test here builds the model without one, whichever runs first.
        Assert.Same(dbA.Model, dbB.Model);

        using var useA = tenantA.Use(OrganisationA);
        using var useB = tenantB.Use(OrganisationB);
        var sqlA = dbA.Visits.ToQueryString();
        var sqlB = dbB.Visits.ToQueryString();

        Assert.Contains(OrganisationA.ToString(), sqlA, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(OrganisationB.ToString(), sqlA, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(OrganisationB.ToString(), sqlB, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(OrganisationA.ToString(), sqlB, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Only_a_query_on_a_tenant_owned_table_needs_a_tenant()
    {
        using var db = new StandInContext(NoTenant());

        Assert.StartsWith("SELECT", db.Regions.ToQueryString(), StringComparison.Ordinal);
        var ex = Assert.Throws<InvalidOperationException>(() => db.Visits.ToQueryString());
        Assert.StartsWith("There is no tenant", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_tenant_read_other_than_as_a_property_of_the_context_is_refused()
    {
        var tenant = NoTenant();

        Assert.Throws<ArgumentException>(() =>
            TenantQueryFilter.Apply(new ModelBuilder(), () => tenant.OrganisationId));
    }

    private static TenantContext NoTenant() => new(new HttpContextAccessor());

    private sealed class StandInContext(ITenantContext tenant) : DbContext
    {
        public DbSet<Visit> Visits => Set<Visit>();

        public DbSet<Region> Regions => Set<Region>();

        private Guid TenantId => tenant.OrganisationId;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) => optionsBuilder.UseSqlServer();

        protected override void OnModelCreating(ModelBuilder modelBuilder) =>
            TenantQueryFilter.Apply(modelBuilder, () => TenantId);
    }

    private sealed class Visit : ITenantOwned
    {
        public Guid Id { get; set; }

        public Guid OrganisationId { get; set; }
    }

    private sealed class Region
    {
        public Guid Id { get; set; }
    }
}
