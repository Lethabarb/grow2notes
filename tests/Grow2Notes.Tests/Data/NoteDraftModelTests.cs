using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests.Data;

/// <summary>
/// The <c>NoteDraft</c> table in the app's model, as design.md §5.3 sets it out: one working copy per note per editor,
/// with composite foreign keys that keep it within its note's and its editor's organisation (§5.1 <i>Tenancy</i>), and
/// its snapshot of the groups and items in two JSON columns. The migrations create what the model holds: CI fails on a
/// model change without a migration, and so does migrating the test database. Building a model opens no connection,
/// so no test here needs a database. <see cref="ModelTenancyTests"/> checks that the table is tenant-owned and its
/// foreign keys composite, <see cref="RuntimeRoleTests"/> that the app may delete its rows, and
/// <see cref="NoteTableTests"/> its JSON columns on SQL Server.
/// </summary>
public sealed class NoteDraftModelTests
{
    // Stored in the Section of each of NoteDraft.Items, so changing one would change what existing rows mean.
    [Fact]
    public void NoteSection_keeps_its_stored_values() =>
        Assert.Equal(
            new Dictionary<string, byte> { ["Goal"] = 1, ["CommonItem"] = 2 },
            Enum.GetValues<NoteSection>().ToDictionary(s => s.ToString(), s => (byte)s));

    // Nothing else: no PicksCopiedFrom (D83).
    [Fact]
    public async Task Its_columns_have_the_types_and_nullability_of_design_md_and_the_reason_size_of_Limits()
    {
        var table = TableOf(await NoteDraftInTheAppModelAsync());

        (string Name, string Type, bool IsNullable)[] expected =
        [
            ("BaseVersionNumber", "int", false),
            ("CreatedAtUtc", "datetime2(3)", false),
            ("EditorUserId", "uniqueidentifier", false),
            ("FlagReason", $"nvarchar({Limits.FlagReason})", true),
            ("Groups", "nvarchar(max)", false),
            ("IsFlagged", "bit", false),
            ("Items", "nvarchar(max)", false),
            ("LastClientId", "uniqueidentifier", false),
            ("LastSeq", "int", false),
            ("Narrative", "nvarchar(max)", false),
            ("NoteId", "uniqueidentifier", false),
            ("OrganisationId", "uniqueidentifier", false),
            ("UpdatedAtUtc", "datetime2(3)", false),
        ];
        Assert.Equal(
            expected,
            table.Columns
                .Select(column => (column.Name, column.StoreType, column.IsNullable))
                .OrderBy(column => column.Name, StringComparer.Ordinal));
    }

    // The code that creates a working copy gives both, so EF Core generates neither.
    [Fact]
    public async Task Its_key_is_NoteId_and_EditorUserId_and_it_has_no_other()
    {
        var draft = await NoteDraftInTheAppModelAsync();

        var key = Assert.Single(draft.GetKeys());
        Assert.True(key.IsPrimaryKey());
        Assert.Equal([nameof(NoteDraft.NoteId), nameof(NoteDraft.EditorUserId)], key.Properties.Select(p => p.Name));
        Assert.All(key.Properties, property => Assert.Equal(ValueGenerated.Never, property.ValueGenerated));
    }

    // The editor's foreign key needs no index of its own: the design's index starts with its columns. EF Core gives the
    // note's one, as no other index starts with its columns; the key starts with NoteId alone.
    [Fact]
    public async Task It_has_the_index_of_design_md_and_one_for_the_note_foreign_key()
    {
        var table = TableOf(await NoteDraftInTheAppModelAsync());

        Assert.Equal(
            [("OrganisationId, EditorUserId", false, null), ("OrganisationId, NoteId", false, (string?)null)],
            table.Indexes
                .Select(index => (
                    Columns: string.Join(", ", index.Columns.Select(column => column.Name)),
                    index.IsUnique,
                    index.Filter))
                .OrderBy(index => index.Columns, StringComparer.Ordinal));
    }

    // Through the note's and the user's alternate keys, so a working copy cannot point at another organisation's note
    // or user.
    [Fact]
    public async Task It_belongs_to_one_note_and_one_editor_of_its_own_organisation()
    {
        var draft = await NoteDraftInTheAppModelAsync();

        (string Principal, string Columns, string PrincipalKey, bool IsPrimaryKey, bool IsRequired)[] expected =
        [
            (nameof(ApplicationUser), "OrganisationId, EditorUserId", "OrganisationId, Id", false, true),
            (nameof(Note), "OrganisationId, NoteId", "OrganisationId, Id", false, true),
        ];
        Assert.Equal(
            expected,
            draft.GetForeignKeys()
                .Select(foreignKey => (
                    Principal: foreignKey.PrincipalEntityType.ClrType.Name,
                    Columns: string.Join(", ", foreignKey.Properties.Select(p => p.Name)),
                    PrincipalKey: string.Join(", ", foreignKey.PrincipalKey.Properties.Select(p => p.Name)),
                    foreignKey.PrincipalKey.IsPrimaryKey(),
                    foreignKey.IsRequired))
                .OrderBy(foreignKey => foreignKey.Columns, StringComparer.Ordinal));
    }

    [Fact]
    public async Task Groups_and_Items_are_complex_collections_each_in_its_own_JSON_column_named_as_in_design_md()
    {
        var draft = await NoteDraftInTheAppModelAsync();

        Assert.Equal(
            [
                ("groupId", nameof(DraftGroup.GroupId)),
                ("isEveryNote", nameof(DraftGroup.IsEveryNote)),
                ("isPicked", nameof(DraftGroup.IsPicked)),
                ("name", nameof(DraftGroup.Name)),
            ],
            JsonPropertiesOf(draft, nameof(NoteDraft.Groups)));
        Assert.Equal(
            [
                ("groupId", nameof(DraftItem.GroupId)),
                ("isTicked", nameof(DraftItem.IsTicked)),
                ("section", nameof(DraftItem.Section)),
                ("sourceId", nameof(DraftItem.SourceId)),
                ("text", nameof(DraftItem.Text)),
            ],
            JsonPropertiesOf(draft, nameof(NoteDraft.Items)));

        // The JSON property name and the property each holds, of a complex collection that is required and alone in
        // its column, which takes the collection's name.
        static IEnumerable<(string? Json, string Property)> JsonPropertiesOf(IEntityType draft, string name)
        {
            var collection = draft.FindComplexProperty(name);
            Assert.NotNull(collection);
            Assert.True(collection.IsCollection);
            Assert.False(collection.IsNullable);
            Assert.Equal(name, collection.ComplexType.GetContainerColumnName());

            return collection.ComplexType.GetProperties()
                .Select(property => (Json: property.GetJsonPropertyName(), property.Name))
                .OrderBy(property => property.Json, StringComparer.Ordinal);
        }
    }

    /// <summary>
    /// <c>NoteDraft</c> in the model the app runs with, from a context that the app's own services make, as
    /// <see cref="GoalModelTests"/> takes <c>Goal</c>.
    /// </summary>
    private static async Task<IEntityType> NoteDraftInTheAppModelAsync()
    {
        await using var app = new AppWithoutDatabase("Production");
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();

        var entityType = db.Model.FindEntityType(typeof(NoteDraft));
        Assert.NotNull(entityType);
        return entityType;
    }

    private static ITable TableOf(IEntityType entityType) => Assert.Single(entityType.GetTableMappings()).Table;
}
