using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests.Data;

/// <summary>
/// The model test of design.md §14 M0 <i>Done when</i>: every entity type in the app's model is
/// <see cref="ITenantOwned"/>, with the <c>"Tenant"</c> query filter and <c>OrganisationId</c> as a concurrency token
/// (design.md §5.9 items 2 and 3, D67), or one of the named exceptions. Nothing in the app's model breaks the rule yet,
/// so stand-in models show that the same check finds each breach. Building a model opens no connection, so no test
/// here needs a database.
/// </summary>
public sealed class ModelTenancyTests
{
    // Organisation is the tenant, not one of its rows (design.md §5.3). The rest are design.md §5.9 item 5's: Identity
    // reads AspNetUsers by email before there is a tenant, and its child tables and DataProtectionKeys have no
    // OrganisationId. A name here that the model does not have is a breach too, so the list keeps up with the model.
    private static readonly string[] NamedExceptions =
    [
        "Organisation",
        "AspNetUsers",
        "AspNetUserTokens",
        "AspNetUserPasskeys",
        "AspNetUserClaims",
        "AspNetUserLogins",
        "DataProtectionKeys",
    ];

    [Fact]
    public async Task Every_entity_type_in_the_app_s_model_is_tenant_owned_and_filtered_or_a_named_exception()
    {
        // The app's own services make the context, so its model is the one the app runs with: built at the Identity
        // schema version that Program.cs sets, which adds AspNetUserPasskeys. A context made here by hand could build
        // another, and EF Core would then cache that model for every context of this type in the test run.
        await using var app = new AppWithoutDatabase("Production");
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();

        Assert.Empty(Breaches(db.Model, NamedExceptions));
    }

    [Fact]
    public void A_tenant_owned_entity_type_with_the_Tenant_filter_and_a_named_exception_keep_the_rule()
    {
        using var db = new RuleKeptContext();

        Assert.Empty(Breaches(db.Model, [nameof(Region)]));
    }

    [Fact]
    public void A_named_exception_missing_from_the_model_is_a_breach()
    {
        const string missing = "NoSuchTable";
        using var db = new RuleKeptContext();

        Assert.Equal(
            [new Breach(missing, Fault.NamedExceptionNotInTheModel)],
            Breaches(db.Model, [nameof(Region), missing]));
    }

    [Fact]
    public void An_entity_type_with_OrganisationId_that_is_not_tenant_owned_is_a_breach()
    {
        using var db = new StrayContext();

        Assert.Equal([new Breach(nameof(Stray), Fault.NeitherTenantOwnedNorANamedException)], Breaches(db.Model, []));
    }

    [Fact]
    public void A_tenant_owned_entity_type_whose_context_did_not_apply_the_Tenant_filter_is_a_breach()
    {
        using var db = new UnfilteredContext();

        Assert.Equal(
            [
                new Breach(nameof(Visit), Fault.NoTenantFilter),
                new Breach(nameof(Visit), Fault.OrganisationIdIsNotAConcurrencyToken),
            ],
            Breaches(db.Model, []));
    }

    /// <summary>
    /// Each breach of the rule in <paramref name="model"/>: an entity type that is neither <see cref="ITenantOwned"/>
    /// nor in <paramref name="namedExceptions"/>; an <see cref="ITenantOwned"/> one without the <c>"Tenant"</c> filter,
    /// or whose <c>OrganisationId</c> is not a concurrency token; and a named exception that the model does not have.
    /// An entity type goes by its table, as the named exceptions do.
    /// </summary>
    private static List<Breach> Breaches(IModel model, string[] namedExceptions)
    {
        // An owned type, such as Identity's passkey data, is stored in its owner's rows, so it goes with its owner.
        var entityTypes = model.GetEntityTypes().Where(entityType => !entityType.IsOwned()).ToList();
        var tenantOwned = entityTypes
            .Where(entityType => entityType.ClrType.IsAssignableTo(typeof(ITenantOwned)))
            .ToList();

        return
        [
            .. entityTypes.Except(tenantOwned)
                .Where(entityType => !namedExceptions.Contains(TableOf(entityType)))
                .Select(entityType => new Breach(TableOf(entityType), Fault.NeitherTenantOwnedNorANamedException)),
            .. tenantOwned
                .Where(entityType => entityType.FindDeclaredQueryFilter(TenantQueryFilter.Name) is null)
                .Select(entityType => new Breach(TableOf(entityType), Fault.NoTenantFilter)),
            .. tenantOwned
                .Where(entityType => entityType.FindProperty(nameof(ITenantOwned.OrganisationId))
                    is not { IsConcurrencyToken: true })
                .Select(entityType => new Breach(TableOf(entityType), Fault.OrganisationIdIsNotAConcurrencyToken)),
            .. namedExceptions.Except(entityTypes.Select(TableOf))
                .Select(table => new Breach(table, Fault.NamedExceptionNotInTheModel)),
        ];

        // One mapped to a view or a query rather than a table goes by its own name.
        static string TableOf(IEntityType entityType) => entityType.GetTableName() ?? entityType.DisplayName();
    }

    private enum Fault
    {
        NeitherTenantOwnedNorANamedException,
        NoTenantFilter,
        OrganisationIdIsNotAConcurrencyToken,
        NamedExceptionNotInTheModel,
    }

    private sealed record Breach(string Table, Fault Fault);

    // EF Core builds a model once for each context type, so each stand-in model has a context type of its own.
    private abstract class StandInContext : DbContext
    {
        // No test here runs a query, so the tenant is never read.
        private readonly TenantContext tenant = new(new HttpContextAccessor());

        protected Guid TenantId => tenant.OrganisationId;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) => optionsBuilder.UseSqlServer();
    }

    private sealed class RuleKeptContext : StandInContext
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Visit>();
            modelBuilder.Entity<Region>();
            TenantQueryFilter.Apply(modelBuilder, () => TenantId);
        }
    }

    // TenantQueryFilter.Apply finds tenant-owned entity types by ITenantOwned alone, so it passes Stray by.
    private sealed class StrayContext : StandInContext
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Stray>();
            TenantQueryFilter.Apply(modelBuilder, () => TenantId);
        }
    }

    // As a context would be that forgot to call TenantQueryFilter.Apply.
    private sealed class UnfilteredContext : StandInContext
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.Entity<Visit>();
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

    // A tenant-owned entity that was not marked as one.
    private sealed class Stray
    {
        public Guid Id { get; set; }

        public Guid OrganisationId { get; set; }
    }
}
