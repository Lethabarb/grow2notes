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
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(user => user.HasIndex(u => new { u.OrganisationId, u.Status }));
    }
}
