using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Grow2Notes.Web.Data;

/// <summary>
/// The application database (design.md §5). It derives from <see cref="IdentityUserContext{TUser, TKey}"/>, not
/// <c>IdentityDbContext</c>, so Identity creates no role tables: a user's role is the <c>Role</c> column. The Identity
/// schema version comes from <c>IdentityOptions.Stores.SchemaVersion</c>, which Program.cs sets to <c>Version3</c> to
/// add <c>AspNetUserPasskeys</c>. A query on a tenant-owned table sees only the tenant's rows, and a save writes only
/// the tenant's rows (design.md §5.9) and never changes or deletes an append-only one (§5.8).
/// </summary>
internal sealed class Grow2NotesDbContext(DbContextOptions<Grow2NotesDbContext> options, ITenantContext tenant)
    : IdentityUserContext<ApplicationUser, Guid>(options)
{
    public DbSet<Organisation> Organisations => Set<Organisation>();

    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    // The tenant query filter reads this as each query on a tenant-owned table runs, and nothing else does. So making
    // the context and building its model, as dotnet ef does, need no tenant, and nor do Identity's queries.
    private Guid TenantId => tenant.OrganisationId;

    /// <summary>
    /// Runs <paramref name="work"/> in one transaction and commits it (design.md §5.9). The retrying execution strategy
    /// refuses a transaction begun outside it. Inside it, a transient failure rolls the transaction back and the
    /// strategy runs the whole of <paramref name="work"/> again in a new one. So <paramref name="work"/> loads what it
    /// changes: every attempt starts with an empty change tracker, which also discards anything tracked before the
    /// call. A connection lost after the commit reached the server is retried too, so work that would do harm if it ran
    /// twice needs a key that finds its first run, as the <c>Idempotency-Key</c> does for a version save.
    /// </summary>
    public Task InTransactionAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken = default) =>
        Database.CreateExecutionStrategy().ExecuteAsync(async ct =>
        {
            // Entities tracked by a failed attempt describe rows that its rollback removed.
            ChangeTracker.Clear();
            await using var transaction = await Database.BeginTransactionAsync(ct);
            await work(ct);
            await transaction.CommitAsync(ct);
        }, cancellationToken);

    // Here rather than where the app registers the context, so that every instance has them, however it is made, and
    // the tenant one checks each save against the same tenant as this context's queries. EF Core runs them in the order
    // added. The append-only one goes first because it reads no tenant and changes nothing: a change to an append-only
    // row is refused for what it is even with no tenant, and a save it refuses leaves its added rows unstamped.
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) =>
        optionsBuilder.AddInterceptors(
            new AppendOnlySaveChangesInterceptor(), new TenantSaveChangesInterceptor(tenant));

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder) =>
        ModelConventions.Apply(configurationBuilder);

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Organisation>(organisation =>
        {
            organisation.Property(o => o.Name).HasMaxLength(Limits.OrganisationName);
            organisation.Property(o => o.GuidePrompts).HasMaxLength(Limits.GuidePrompts).HasDefaultValue("");
            organisation.Property(o => o.RowVersion).IsRowVersion();
        });

        builder.Entity<ApplicationUser>(user =>
        {
            user.HasOne<Organisation>().WithMany().HasForeignKey(u => u.OrganisationId);

            // The principal of every composite user foreign key, so the database keeps the authors, editors and
            // reviewers of an organisation's rows within that organisation (design.md §5.1, Tenancy).
            user.HasAlternateKey(u => new { u.OrganisationId, u.Id });

            user.Property(u => u.DisplayName).HasMaxLength(Limits.DisplayName);

            // Identity already sizes these, at the same length. Taking the size from Limits keeps the request
            // validation of an email address in step with the columns that store it.
            user.Property(u => u.Email).HasMaxLength(Limits.Email);
            user.Property(u => u.NormalizedEmail).HasMaxLength(Limits.Email);
            user.Property(u => u.UserName).HasMaxLength(Limits.Email);
            user.Property(u => u.NormalizedUserName).HasMaxLength(Limits.Email);

            // Identity's EmailIndex, made unique: one account per email address across the whole app (A27). Identity's
            // RequireUniqueEmail option only checks in the app, where two requests at once can both pass, and leaves
            // the index non-unique. NormalizedEmail is upper-cased, so the index also refuses the same address in
            // another letter case.
            user.HasIndex(u => u.NormalizedEmail).IsUnique();

            user.HasIndex(u => new { u.OrganisationId, u.Status });
        });

        // Deliberately no relationships, so the table has no foreign keys (design.md §5.3).
        builder.Entity<AuditEvent>(auditEvent =>
        {
            // varchar, as design.md §5.3 has them: event and entity types are ASCII names, and so is an IP address.
            auditEvent.Property(e => e.EventType).HasMaxLength(Limits.AuditEventType).IsUnicode(false);
            auditEvent.Property(e => e.EntityType).HasMaxLength(Limits.AuditEntityType).IsUnicode(false);
            auditEvent.Property(e => e.IpAddress).HasMaxLength(Limits.IpAddress).IsUnicode(false);
            auditEvent.Property(e => e.Details).HasColumnType("nvarchar(max)");

            auditEvent.HasIndex(e => new { e.OrganisationId, e.OccurredAtUtc });
            auditEvent.HasIndex(e => new { e.OrganisationId, e.ParticipantId, e.OccurredAtUtc })
                .HasFilter("[ParticipantId] IS NOT NULL");
            auditEvent.HasIndex(e => new { e.OrganisationId, e.ActorUserId, e.OccurredAtUtc });
        });

        // KeysDbContext reads and writes the Data Protection key ring. The table is in this model as well, so that this
        // context's migrations create it, under the plural name that KeysDbContext expects (design.md §5.3).
        builder.Entity<DataProtectionKey>().ToTable(nameof(KeysDbContext.DataProtectionKeys));

        // Last, so it finds every entity type configured above. With the filter, it makes OrganisationId a concurrency
        // token, so updates and deletes keep to the tenant's rows too.
        TenantQueryFilter.Apply(builder, () => TenantId);
    }
}
