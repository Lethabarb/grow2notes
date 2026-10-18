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
/// the tenant's rows (design.md §5.9), never changes or deletes an append-only one, and never changes a note's
/// participant, date or author (§5.8).
/// </summary>
internal sealed class Grow2NotesDbContext(DbContextOptions<Grow2NotesDbContext> options, ITenantContext tenant)
    : IdentityUserContext<ApplicationUser, Guid>(options)
{
    public DbSet<Organisation> Organisations => Set<Organisation>();

    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    public DbSet<Participant> Participants => Set<Participant>();

    public DbSet<Goal> Goals => Set<Goal>();

    public DbSet<CommonItemGroup> CommonItemGroups => Set<CommonItemGroup>();

    public DbSet<CommonItem> CommonItems => Set<CommonItem>();

    public DbSet<Note> Notes => Set<Note>();

    public DbSet<NoteDraft> NoteDrafts => Set<NoteDraft>();

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

        builder.Entity<Participant>(participant =>
        {
            participant.HasOne<Organisation>().WithMany().HasForeignKey(p => p.OrganisationId);

            // The principal of the composite foreign keys from a participant's goals and notes, so the database keeps
            // them within the participant's organisation (design.md §5.1, Tenancy).
            participant.HasAlternateKey(p => new { p.OrganisationId, p.Id });

            participant.Property(p => p.GivenName).HasMaxLength(Limits.ParticipantName);
            participant.Property(p => p.FamilyName).HasMaxLength(Limits.ParticipantName);
            participant.Property(p => p.RowVersion).IsRowVersion();

            // Every list of participants is one organisation's, of one status or both, by family name, then given
            // name (A5).
            participant.HasIndex(p => new { p.OrganisationId, p.Status, p.FamilyName, p.GivenName });
        });

        builder.Entity<Goal>(goal =>
        {
            // Composite, so the database keeps a goal within its participant's organisation (design.md §5.1, Tenancy).
            goal.HasOne<Participant>().WithMany()
                .HasForeignKey(g => new { g.OrganisationId, g.ParticipantId })
                .HasPrincipalKey(p => new { p.OrganisationId, p.Id });

            // The principal of the composite foreign key from a note version's goal rows (design.md §5.3).
            goal.HasAlternateKey(g => new { g.OrganisationId, g.Id });

            goal.Property(g => g.Text).HasMaxLength(Limits.ItemText);
            goal.Property(g => g.RowVersion).IsRowVersion();

            // Every list of goals is one participant's, the active ones or all, in their order. It starts with the
            // foreign key's columns, so the foreign key needs no index of its own.
            goal.HasIndex(g => new { g.OrganisationId, g.ParticipantId, g.ArchivedAtUtc, g.SortOrder });
        });

        builder.Entity<CommonItemGroup>(group =>
        {
            group.HasOne<Organisation>().WithMany().HasForeignKey(g => g.OrganisationId);

            // The principal of the composite foreign keys from the group's items and a note version's group rows, so
            // the database keeps them within the group's organisation (design.md §5.1, Tenancy).
            group.HasAlternateKey(g => new { g.OrganisationId, g.Id });

            group.Property(g => g.Name).HasMaxLength(Limits.GroupName);
            group.Property(g => g.RowVersion).IsRowVersion();

            // Every list of groups is one organisation's, the active ones or all, in their order.
            group.HasIndex(g => new { g.OrganisationId, g.ArchivedAtUtc, g.SortOrder });

            // At most one Every note group in an organisation (design.md §5.3, Constraints). Bootstrap and the
            // migration that added the table give each its one, and nothing deletes a group, so each has exactly one.
            group.HasIndex(g => g.OrganisationId).IsUnique().HasFilter("[IsEveryNote] = 1");

            // Every note is never archived, whatever the app's code does.
            group.ToTable(table => table.HasCheckConstraint(
                "CK_CommonItemGroup_EveryNoteActive", "[IsEveryNote] = 0 OR [ArchivedAtUtc] IS NULL"));
        });

        builder.Entity<CommonItem>(item =>
        {
            // Composite, so the database keeps an item within its group's organisation (design.md §5.1, Tenancy).
            item.HasOne<CommonItemGroup>().WithMany()
                .HasForeignKey(i => new { i.OrganisationId, i.GroupId })
                .HasPrincipalKey(g => new { g.OrganisationId, g.Id });

            // The principal of the composite foreign key from a note version's item rows (design.md §5.3).
            item.HasAlternateKey(i => new { i.OrganisationId, i.Id });

            item.Property(i => i.Text).HasMaxLength(Limits.ItemText);
            item.Property(i => i.RowVersion).IsRowVersion();

            // Every list of items is one group's, the active ones or all, in their order. It starts with the foreign
            // key's columns, so the foreign key needs no index of its own.
            item.HasIndex(i => new { i.OrganisationId, i.GroupId, i.ArchivedAtUtc, i.SortOrder });
        });

        builder.Entity<Note>(note =>
        {
            // Composite, so the database keeps a note's participant, author and discarder within the note's
            // organisation (design.md §5.1, Tenancy).
            note.HasOne<Participant>().WithMany()
                .HasForeignKey(n => new { n.OrganisationId, n.ParticipantId })
                .HasPrincipalKey(p => new { p.OrganisationId, p.Id });
            note.HasOne<ApplicationUser>().WithMany()
                .HasForeignKey(n => new { n.OrganisationId, n.AuthorUserId })
                .HasPrincipalKey(u => new { u.OrganisationId, u.Id });
            note.HasOne<ApplicationUser>().WithMany()
                .HasForeignKey(n => new { n.OrganisationId, n.DiscardedByUserId })
                .HasPrincipalKey(u => new { u.OrganisationId, u.Id });

            // The principal of the composite foreign keys from a note's working copies, versions and reviews.
            note.HasAlternateKey(n => new { n.OrganisationId, n.Id });

            // Each status with the columns that go with it, so no save can leave a submitted note without its first
            // submitted time, or a discarded one without who discarded it and when (design.md §5.3).
            note.ToTable(table => table.HasCheckConstraint(
                "CK_Note_Status",
                "([Status] = 0 AND [CurrentVersionNumber] = 0 AND [FirstSubmittedAtUtc] IS NULL"
                + " AND [DiscardedAtUtc] IS NULL)"
                + " OR ([Status] = 1 AND [CurrentVersionNumber] >= 1 AND [FirstSubmittedAtUtc] IS NOT NULL"
                + " AND [DiscardedAtUtc] IS NULL)"
                + " OR ([Status] = 2 AND [CurrentVersionNumber] = 0 AND [DiscardedAtUtc] IS NOT NULL"
                + " AND [DiscardedByUserId] IS NOT NULL)"));

            // One live note, a draft or submitted, per participant and day (D6). The app's check before a note is
            // created can pass for two first saves at once; this refuses the second (§5.5 item 3). Discarded drafts
            // stay beside the live note, so the day can be started again. Its columns start with the participant's
            // foreign key, so that needs no index of its own.
            note.HasIndex(n => new { n.OrganisationId, n.ParticipantId, n.NoteDate })
                .IsUnique()
                .HasFilter("[Status] IN (0, 1)");

            // For Today and the daily report, which read a day's notes.
            note.HasIndex(n => new { n.OrganisationId, n.NoteDate })
                .IncludeProperties(n => new { n.Status, n.ParticipantId, n.AuthorUserId });

            // For the Flagged list and its badge, which read the notes to review alone.
            note.HasIndex(n => new { n.OrganisationId, n.FlagStatus }).HasFilter("[FlagStatus] = 1");
        });

        builder.Entity<NoteDraft>(draft =>
        {
            // One working copy per note per editor (design.md §5.3).
            draft.HasKey(d => new { d.NoteId, d.EditorUserId });

            // Composite, so the database keeps a working copy within its note's and its editor's organisation
            // (design.md §5.1, Tenancy).
            draft.HasOne<Note>().WithMany()
                .HasForeignKey(d => new { d.OrganisationId, d.NoteId })
                .HasPrincipalKey(n => new { n.OrganisationId, n.Id });
            draft.HasOne<ApplicationUser>().WithMany()
                .HasForeignKey(d => new { d.OrganisationId, d.EditorUserId })
                .HasPrincipalKey(u => new { u.OrganisationId, u.Id });

            // Each in a JSON column, with the property names design.md §5.3 gives: every autosave rewrites a working
            // copy's snapshot whole, with its ticks and picks, so it needs no rows of its own, as a version's has.
            draft.ComplexCollection(d => d.Groups, groups =>
            {
                groups.ToJson();
                groups.Property(g => g.GroupId).HasJsonPropertyName("groupId");
                groups.Property(g => g.Name).HasJsonPropertyName("name");
                groups.Property(g => g.IsEveryNote).HasJsonPropertyName("isEveryNote");
                groups.Property(g => g.IsPicked).HasJsonPropertyName("isPicked");
            });
            draft.ComplexCollection(d => d.Items, items =>
            {
                items.ToJson();
                items.Property(i => i.Section).HasJsonPropertyName("section");
                items.Property(i => i.SourceId).HasJsonPropertyName("sourceId");
                items.Property(i => i.GroupId).HasJsonPropertyName("groupId");
                items.Property(i => i.Text).HasJsonPropertyName("text");
                items.Property(i => i.IsTicked).HasJsonPropertyName("isTicked");
            });

            // Longer than an nvarchar(n) can hold, so only validation enforces Limits.GuidedNotes (§5.1).
            draft.Property(d => d.Narrative).HasColumnType("nvarchar(max)");
            draft.Property(d => d.FlagReason).HasMaxLength(Limits.FlagReason);

            // For a user's unfinished drafts. It starts with the editor's foreign key, so that needs no index of its
            // own.
            draft.HasIndex(d => new { d.OrganisationId, d.EditorUserId });
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
