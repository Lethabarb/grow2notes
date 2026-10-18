using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests.Data;

/// <summary>
/// The <c>NoteVersion</c> table in the app's model, as design.md §5.3 sets it out: the composite foreign keys that keep
/// a version within its note's and its writer's organisation, the alternate key that its item rows point at (§5.1
/// <i>Tenancy</i>), and the unique indexes of the version number and the save's key (§5.8). The migrations create what
/// the model holds: CI fails on a model change without a migration, and so does migrating the test database. Building
/// a model opens no connection, so no test here needs a database. <see cref="ModelTenancyTests"/> checks that the table
/// is tenant-owned and its foreign keys composite, <see cref="RuntimeRoleTests"/> its denies, and
/// <see cref="NoteVersionTableTests"/> the indexes on SQL Server.
/// </summary>
public sealed class NoteVersionModelTests
{
    [Fact]
    public async Task Its_columns_have_the_types_and_nullability_of_design_md_and_the_reason_size_of_Limits()
    {
        var table = TableOf(await NoteVersionInTheAppModelAsync());

        (string Name, string Type, bool IsNullable)[] expected =
        [
            ("CreatedAtUtc", "datetime2(3)", false),
            ("CreatedByUserId", "uniqueidentifier", false),
            ("FlagReason", $"nvarchar({Limits.FlagReason})", true),
            ("Id", "uniqueidentifier", false),
            ("IdempotencyKey", "uniqueidentifier", false),
            ("IsFlagged", "bit", false),
            ("Narrative", "nvarchar(max)", false),
            ("NoteId", "uniqueidentifier", false),
            ("OrganisationId", "uniqueidentifier", false),
            ("VersionNumber", "int", false),
        ];
        Assert.Equal(
            expected,
            table.Columns
                .Select(column => (column.Name, column.StoreType, column.IsNullable))
                .OrderBy(column => column.Name, StringComparer.Ordinal));
    }

    [Fact]
    public async Task Its_key_is_Id_with_OrganisationId_and_Id_an_alternate_key()
    {
        var version = await NoteVersionInTheAppModelAsync();

        Assert.Equal(
            [
                (true, nameof(NoteVersion.Id)),
                (false, $"{nameof(NoteVersion.OrganisationId)}, {nameof(NoteVersion.Id)}"),
            ],
            version.GetKeys()
                .Select(key => (key.IsPrimaryKey(), string.Join(", ", key.Properties.Select(p => p.Name))))
                .OrderByDescending(key => key.Item1));
    }

    // Neither of design.md's indexes starts with a foreign key's columns, so EF Core gives each foreign key one of its
    // own.
    [Fact]
    public async Task It_has_the_two_unique_indexes_of_design_md_and_one_for_each_foreign_key()
    {
        var table = TableOf(await NoteVersionInTheAppModelAsync());

        Assert.Equal(
            [
                ("NoteId, VersionNumber", true, null),
                ("OrganisationId, CreatedByUserId", false, null),
                ("OrganisationId, IdempotencyKey", true, null),
                ("OrganisationId, NoteId", false, (string?)null),
            ],
            table.Indexes
                .Select(index => (
                    Columns: string.Join(", ", index.Columns.Select(column => column.Name)),
                    index.IsUnique,
                    index.Filter))
                .OrderBy(index => index.Columns, StringComparer.Ordinal));
    }

    // Through the note's and the user's alternate keys, so a version cannot point at another organisation's note or
    // user.
    [Fact]
    public async Task It_belongs_to_one_note_and_one_writer_of_its_own_organisation()
    {
        var version = await NoteVersionInTheAppModelAsync();

        (string Principal, string Columns, string PrincipalKey, bool IsPrimaryKey, bool IsRequired)[] expected =
        [
            (nameof(ApplicationUser), "OrganisationId, CreatedByUserId", "OrganisationId, Id", false, true),
            (nameof(Note), "OrganisationId, NoteId", "OrganisationId, Id", false, true),
        ];
        Assert.Equal(
            expected,
            version.GetForeignKeys()
                .Select(foreignKey => (
                    Principal: foreignKey.PrincipalEntityType.ClrType.Name,
                    Columns: string.Join(", ", foreignKey.Properties.Select(p => p.Name)),
                    PrincipalKey: string.Join(", ", foreignKey.PrincipalKey.Properties.Select(p => p.Name)),
                    foreignKey.PrincipalKey.IsPrimaryKey(),
                    foreignKey.IsRequired))
                .OrderBy(foreignKey => foreignKey.Columns, StringComparer.Ordinal));
    }

    /// <summary>
    /// <c>NoteVersion</c> in the model the app runs with, from a context that the app's own services make, as
    /// <see cref="GoalModelTests"/> takes <c>Goal</c>.
    /// </summary>
    private static async Task<IEntityType> NoteVersionInTheAppModelAsync()
    {
        await using var app = new AppWithoutDatabase("Production");
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();

        var entityType = db.Model.FindEntityType(typeof(NoteVersion));
        Assert.NotNull(entityType);
        return entityType;
    }

    private static ITable TableOf(IEntityType entityType) => Assert.Single(entityType.GetTableMappings()).Table;
}
