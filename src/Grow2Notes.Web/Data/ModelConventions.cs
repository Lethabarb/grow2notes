using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Grow2Notes.Web.Data;

/// <summary>
/// The design.md §5.1 conventions that EF Core does not apply by default, set once so that every entity gets them.
/// The other rows are EF Core's own defaults on SQL Server, which the model tests pin: a <c>Guid</c> key gets a
/// sequential GUID generated on the client, a <c>DateOnly</c> is a <c>date</c>, and an enum with <c>byte</c> as its
/// underlying type is a <c>tinyint</c>.
/// </summary>
internal static class ModelConventions
{
    public static void Apply(ModelConfigurationBuilder configuration)
    {
        // Without the DbSet's plural name, a table takes its entity's name: Note, NoteVersion. Identity names its own
        // tables explicitly, so they keep their AspNet… names.
        configuration.Conventions.Remove(typeof(TableNameFromDbSetConvention));

        configuration.Conventions.Add(_ => new NoCascadeDeleteConvention());

        // DateTime? columns included.
        configuration.Properties<DateTime>().HaveColumnType("datetime2(3)").HaveConversion<UtcDateTimeConverter>();
    }

    /// <summary>
    /// SQL Server keeps no kind with a <c>datetime2</c>, so EF Core would read every timestamp back as
    /// <see cref="DateTimeKind.Unspecified"/>, which time zone conversions then treat as local time.
    /// </summary>
    private sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
        value => value,
        value => DateTime.SpecifyKind(value, DateTimeKind.Utc));

    /// <summary>
    /// Makes every foreign key <c>ON DELETE NO ACTION</c>: rows are kept, not deleted with their parent (A29). EF Core
    /// otherwise cascades a required relationship, Identity's child tables included. Restrict is what SQL Server
    /// creates as NO ACTION, and it also stops EF Core deleting a parent whose children it is tracking. It runs as the
    /// model is finalised, after Identity's configuration and EF Core's cascade convention have had their say.
    /// </summary>
    private sealed class NoCascadeDeleteConvention : IModelFinalizingConvention
    {
        public void ProcessModelFinalizing(
            IConventionModelBuilder modelBuilder, IConventionContext<IConventionModelBuilder> context)
        {
            // An ownership is part of its owner's row, such as the passkey data that Identity stores as JSON, so it
            // has no foreign key in the database.
            foreach (var foreignKey in modelBuilder.Metadata.GetEntityTypes()
                         .SelectMany(entityType => entityType.GetDeclaredForeignKeys())
                         .Where(foreignKey => !foreignKey.IsOwnership))
            {
                foreignKey.Builder.OnDelete(DeleteBehavior.Restrict);
            }
        }
    }
}
