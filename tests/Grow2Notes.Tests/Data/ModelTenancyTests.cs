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
/// (design.md §5.9 items 2 and 3, A48), or one of the named exceptions; every owned type is stored wholly in its
/// owner's table; and every foreign key from an <see cref="ITenantOwned"/> entity type keeps its rows to their
/// organisation (design.md §5.1 <i>Tenancy</i>, §5.9 item 4). Nothing in the app's model breaks the rule yet, and no
/// entity type in it is tenant-owned, so stand-in models show that the same check finds each breach. Building a model
/// opens no connection, so no test here needs a database.
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
    public async Task Every_entity_type_and_foreign_key_in_the_app_s_model_keeps_the_tenancy_rule()
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

    [Fact]
    public void An_owned_type_with_a_table_of_its_own_is_a_breach_and_one_wholly_in_its_owner_s_table_is_not()
    {
        using var db = new OwnedTypesContext();

        Assert.Equal(
            [
                new Breach(nameof(ShiftHandover), Fault.OwnedTypeWithATableOfItsOwn),
                new Breach(nameof(ShiftIncident), Fault.OwnedTypeWithATableOfItsOwn),
                new Breach(nameof(ShiftTask), Fault.OwnedTypeWithATableOfItsOwn),
            ],
            Breaches(db.Model, []));
    }

    [Fact]
    public void Composite_links_matched_on_OrganisationId_and_OrganisationId_alone_to_Organisation_keep_the_rule()
    {
        using var db = new LinkedContext();

        Assert.Empty(Breaches(db.Model, [nameof(Organisation), nameof(ApplicationUser)]));
    }

    [Fact]
    public void A_link_by_ID_alone_to_a_tenant_owned_entity_type_or_to_a_user_is_a_breach()
    {
        using var db = new SingleColumnLinksContext();

        Assert.Equal(
            [
                new Breach(nameof(VisitNote), Fault.ForeignKeyDoesNotMatchOrganisationId, "AuthorUserId"),
                new Breach(nameof(VisitNote), Fault.ForeignKeyDoesNotMatchOrganisationId, "VisitId"),
            ],
            Breaches(db.Model, [nameof(ApplicationUser)]));
    }

    [Fact]
    public void A_composite_link_that_pairs_OrganisationId_with_another_principal_property_is_a_breach()
    {
        using var db = new MismatchedLinkContext();

        Assert.Equal(
            [new Breach(nameof(VisitNote), Fault.ForeignKeyDoesNotMatchOrganisationId, "OrganisationId, VisitId")],
            Breaches(db.Model, []));
    }

    [Fact]
    public void A_link_to_Organisation_that_is_not_OrganisationId_alone_is_a_breach()
    {
        const Fault fault = Fault.ForeignKeyToOrganisationIsNotOrganisationIdAlone;
        using var db = new OrganisationLinksContext();

        Assert.Equal(
            [
                new Breach(nameof(Referral), fault, "FromOrganisationId"),
                new Breach(nameof(Referral), fault, "OrganisationId, OrganisationName"),
            ],
            Breaches(db.Model, [nameof(Organisation)]));
    }

    /// <summary>
    /// Each breach of the rule in <paramref name="model"/>: an entity type that is neither <see cref="ITenantOwned"/>
    /// nor in <paramref name="namedExceptions"/>; an owned type not wholly in its owner's table; an
    /// <see cref="ITenantOwned"/> entity type without the <c>"Tenant"</c> filter, or whose <c>OrganisationId</c> is not
    /// a concurrency token; a named exception that the model does not have; and a foreign key from an
    /// <see cref="ITenantOwned"/> entity type that does not keep its row to its organisation. One to another
    /// <see cref="ITenantOwned"/> entity type or to <see cref="ApplicationUser"/> must pair the row's
    /// <c>OrganisationId</c> with the principal's, so that the database refuses a link to another organisation's row;
    /// the principal key it points at includes <c>OrganisationId</c>, as an alternate key such as
    /// <c>(OrganisationId, Id)</c> does, which <c>AspNetUsers</c> has. One to <see cref="Organisation"/> must be
    /// <c>OrganisationId</c> alone, the row's own organisation. An entity type goes by its table, as the named
    /// exceptions do; an owned type by each table it has of its own; and a foreign key by its dependent's table and its
    /// properties.
    /// </summary>
    /// <remarks>
    /// A foreign key from a named exception, such as Identity's from its child tables to <c>AspNetUsers</c>, is outside
    /// the rule: design.md §5.1 sets it for the foreign keys of tenant-owned tables.
    /// </remarks>
    private static List<Breach> Breaches(IModel model, string[] namedExceptions)
    {
        const string organisationId = nameof(ITenantOwned.OrganisationId);

        // An owned type wholly in its owner's table, as a JSON column (Identity's passkey data) or as more of the
        // owner's columns, is in the owner's rows, so it goes with its owner. A table of its own, whole (OwnsMany,
        // ToTable) or split off (SplitToTable), is a breach: its rows have no OrganisationId, the interceptor sees only
        // the owner, and the owner's concurrency token is matched only in the owner's table, so a save could change
        // another organisation's rows. Nor can an owned type be ITenantOwned, since TenantQueryFilter.Apply throws on
        // one. A tenant-owned child row is an ITenantOwned entity type with a composite foreign key instead (design.md
        // §5.1 Tenancy).
        var entityTypes = model.GetEntityTypes().Where(entityType => !entityType.IsOwned()).ToList();
        var ownedTypesTablesOfTheirOwn = model.GetEntityTypes()
            .SelectMany(entityType => entityType.FindOwnership() is { PrincipalEntityType: var owner }
                ? entityType.GetTableMappings()
                    .Select(mapping => StoreObjectIdentifier.Table(mapping.Table.Name, mapping.Table.Schema))
                    .Where(table => table != StoreObjectIdentifier.Create(owner, StoreObjectType.Table))
                : [])
            .ToList();
        var tenantOwned = entityTypes
            .Where(entityType => entityType.ClrType.IsAssignableTo(typeof(ITenantOwned)))
            .ToList();
        var foreignKeys = tenantOwned.SelectMany(entityType => entityType.GetDeclaredForeignKeys()).ToList();

        return
        [
            .. entityTypes.Except(tenantOwned)
                .Where(entityType => !namedExceptions.Contains(TableOf(entityType)))
                .Select(entityType => new Breach(TableOf(entityType), Fault.NeitherTenantOwnedNorANamedException)),
            .. ownedTypesTablesOfTheirOwn.Select(table => new Breach(table.Name, Fault.OwnedTypeWithATableOfItsOwn)),
            .. tenantOwned
                .Where(entityType => entityType.FindDeclaredQueryFilter(TenantQueryFilter.Name) is null)
                .Select(entityType => new Breach(TableOf(entityType), Fault.NoTenantFilter)),
            .. tenantOwned
                .Where(entityType => entityType.FindProperty(organisationId) is not { IsConcurrencyToken: true })
                .Select(entityType => new Breach(TableOf(entityType), Fault.OrganisationIdIsNotAConcurrencyToken)),
            .. namedExceptions.Except(entityTypes.Select(TableOf))
                .Select(table => new Breach(table, Fault.NamedExceptionNotInTheModel)),
            .. foreignKeys
                .Where(foreignKey => (tenantOwned.Contains(foreignKey.PrincipalEntityType)
                        || foreignKey.PrincipalEntityType.ClrType == typeof(ApplicationUser))
                    && !foreignKey.Properties.Zip(foreignKey.PrincipalKey.Properties)
                        .Any(pair => pair.First.Name == organisationId && pair.Second.Name == organisationId))
                .Select(foreignKey => BreachOf(foreignKey, Fault.ForeignKeyDoesNotMatchOrganisationId)),
            .. foreignKeys
                .Where(foreignKey => foreignKey.PrincipalEntityType.ClrType == typeof(Organisation)
                    && (foreignKey.Properties is not [{ Name: organisationId }]
                        || !foreignKey.PrincipalKey.IsPrimaryKey()))
                .Select(foreignKey => BreachOf(foreignKey, Fault.ForeignKeyToOrganisationIsNotOrganisationIdAlone)),
        ];

        // One mapped to a view or a query rather than a table goes by its own name.
        static string TableOf(IEntityType entityType) => entityType.GetTableName() ?? entityType.DisplayName();

        static Breach BreachOf(IForeignKey foreignKey, Fault fault) => new(
            TableOf(foreignKey.DeclaringEntityType),
            fault,
            string.Join(", ", foreignKey.Properties.Select(property => property.Name)));
    }

    private enum Fault
    {
        NeitherTenantOwnedNorANamedException,
        OwnedTypeWithATableOfItsOwn,
        NoTenantFilter,
        OrganisationIdIsNotAConcurrencyToken,
        NamedExceptionNotInTheModel,
        ForeignKeyDoesNotMatchOrganisationId,
        ForeignKeyToOrganisationIsNotOrganisationIdAlone,
    }

    // A breach by a foreign key has its dependent's table, and lists the foreign key's properties.
    private sealed record Breach(string Table, Fault Fault, string? ForeignKey = null);

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

    // A tenant-owned entity type that owns a collection with a table of its own, as an owned collection has unless it
    // is mapped to JSON, a value moved to a table of its own, and a value split between the owner's columns and a table
    // of its own; a value wholly in the owner's own columns, and a collection in a JSON column of the owner's table.
    private sealed class OwnedTypesContext : StandInContext
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Shift>(entity =>
            {
                entity.OwnsMany(shift => shift.Tasks);
                entity.OwnsOne(shift => shift.Handover).ToTable(nameof(ShiftHandover));
                entity.OwnsOne(shift => shift.Incident)
                    .SplitToTable(nameof(ShiftIncident), table => table.Property(incident => incident.Details));
                entity.OwnsOne(shift => shift.Times);
                entity.OwnsMany(shift => shift.Tags).ToJson();
            });
            TenantQueryFilter.Apply(modelBuilder, () => TenantId);
        }
    }

    // Linked as design.md §5.10 links entities: to a tenant-owned entity's, or a user's, alternate key
    // (OrganisationId, Id), and to Organisation by OrganisationId.
    private sealed class LinkedContext : StandInContext
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Visit>().HasOne<Organisation>().WithMany().HasForeignKey(visit => visit.OrganisationId);
            modelBuilder.Entity<VisitNote>(entity =>
            {
                entity.HasOne<Visit>().WithMany()
                    .HasForeignKey(note => new { note.OrganisationId, note.VisitId })
                    .HasPrincipalKey(visit => new { visit.OrganisationId, visit.Id });
                entity.HasOne<ApplicationUser>().WithMany()
                    .HasForeignKey(note => new { note.OrganisationId, note.AuthorUserId })
                    .HasPrincipalKey(user => new { user.OrganisationId, user.Id });
            });
            TenantQueryFilter.Apply(modelBuilder, () => TenantId);
        }
    }

    // By the ID alone, the database would keep a note of one organisation that points at another's visit or user.
    private sealed class SingleColumnLinksContext : StandInContext
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<VisitNote>(entity =>
            {
                entity.HasOne<Visit>().WithMany().HasForeignKey(note => note.VisitId);
                entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(note => note.AuthorUserId);
            });
            TenantQueryFilter.Apply(modelBuilder, () => TenantId);
        }
    }

    // Both columns are in the key, but the note's OrganisationId is paired with the visit's Id, so the organisations of
    // the two rows are never compared.
    private sealed class MismatchedLinkContext : StandInContext
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<VisitNote>().HasOne<Visit>().WithMany()
                .HasForeignKey(note => new { note.OrganisationId, note.VisitId })
                .HasPrincipalKey(visit => new { visit.Id, visit.OrganisationId });
            TenantQueryFilter.Apply(modelBuilder, () => TenantId);
        }
    }

    // A second column on a link to Organisation, and a link to it by another column, which names another organisation.
    private sealed class OrganisationLinksContext : StandInContext
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Referral>(entity =>
            {
                entity.HasOne<Organisation>().WithMany()
                    .HasForeignKey(referral => new { referral.OrganisationId, referral.OrganisationName })
                    .HasPrincipalKey(organisation => new { organisation.Id, organisation.Name });
                entity.HasOne<Organisation>().WithMany().HasForeignKey(referral => referral.FromOrganisationId);
            });
            TenantQueryFilter.Apply(modelBuilder, () => TenantId);
        }
    }

    private sealed class Visit : ITenantOwned
    {
        public Guid Id { get; set; }

        public Guid OrganisationId { get; set; }
    }

    private sealed class VisitNote : ITenantOwned
    {
        public Guid Id { get; set; }

        public Guid OrganisationId { get; set; }

        public Guid VisitId { get; set; }

        public Guid AuthorUserId { get; set; }
    }

    private sealed class Referral : ITenantOwned
    {
        public Guid Id { get; set; }

        public Guid OrganisationId { get; set; }

        public required string OrganisationName { get; set; }

        public Guid FromOrganisationId { get; set; }
    }

    private sealed class Shift : ITenantOwned
    {
        public Guid Id { get; set; }

        public Guid OrganisationId { get; set; }

        public List<ShiftTask> Tasks { get; } = [];

        public ShiftHandover? Handover { get; set; }

        public ShiftIncident? Incident { get; set; }

        public required ShiftTimes Times { get; set; }

        public List<ShiftTag> Tags { get; } = [];
    }

    private sealed class ShiftTask
    {
        public required string Text { get; set; }
    }

    private sealed class ShiftHandover
    {
        public required string Text { get; set; }
    }

    private sealed class ShiftIncident
    {
        public required string Summary { get; set; }

        public required string Details { get; set; }
    }

    private sealed class ShiftTimes
    {
        public DateTime StartUtc { get; set; }
    }

    private sealed class ShiftTag
    {
        public required string Name { get; set; }
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
