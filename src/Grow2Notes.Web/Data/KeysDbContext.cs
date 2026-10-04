using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Grow2Notes.Web.Data;

/// <summary>
/// The ASP.NET Core Data Protection key ring, in the <c>DataProtectionKeys</c> table of the application database
/// (design.md §5.3). It is a context of its own, with no tenant, so reading the keys that protect the sign-in cookie
/// never depends on who is signed in (§5.9). It leaves out <see cref="ModelConventions"/>, so the table keeps the plural
/// name that <see cref="IDataProtectionKeyContext"/> gives it.
/// </summary>
internal sealed class KeysDbContext(DbContextOptions<KeysDbContext> options)
    : DbContext(options), IDataProtectionKeyContext
{
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    // Grow2NotesDbContext maps the same table, so its migrations create it. The database then has one migration
    // history, and CI builds one migrations bundle and one script (design.md §10.4, §10.6).
    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.Entity<DataProtectionKey>().ToTable(table => table.ExcludeFromMigrations());
}
