using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Grow2Notes.Web.Data;

/// <summary>
/// The application database (design.md §5). It derives from <see cref="IdentityUserContext{TUser, TKey}"/>, not
/// <c>IdentityDbContext</c>, so Identity creates no role tables: a user's role is the <c>Role</c> column. The Identity
/// schema version comes from <c>IdentityOptions.Stores.SchemaVersion</c>, which Program.cs sets to <c>Version3</c> to
/// add <c>AspNetUserPasskeys</c>.
/// </summary>
internal sealed class Grow2NotesDbContext(DbContextOptions<Grow2NotesDbContext> options)
    : IdentityUserContext<ApplicationUser, Guid>(options)
{
    public DbSet<Organisation> Organisations => Set<Organisation>();

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

        // KeysDbContext reads and writes the Data Protection key ring. The table is in this model as well, so that this
        // context's migrations create it, under the plural name that KeysDbContext expects (design.md §5.3).
        builder.Entity<DataProtectionKey>().ToTable(nameof(KeysDbContext.DataProtectionKeys));
    }
}
