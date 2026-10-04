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
    }
}
