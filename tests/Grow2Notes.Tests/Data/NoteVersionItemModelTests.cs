using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests.Data;

/// <summary>
/// The <c>NoteVersionItem</c> table in the app's model, as design.md §5.3 sets it out: one row per place in each
/// section of a version, with composite foreign keys that keep it within its version's, its goal's and its common
/// item's organisation (§5.1 <i>Tenancy</i>), and the check constraint that ties each section to its columns. Its
/// foreign key from <c>GroupPosition</c> points at the version's <c>NoteVersionGroup</c> rows. The migrations create
/// what the model holds: CI fails on a model change without a migration, and so does migrating the test database.
/// Building a model opens no connection, so no test here needs a database. <see cref="ModelTenancyTests"/> checks
/// that the table is tenant-owned and its foreign keys composite, <see cref="RuntimeRoleTests"/> its denies, and
/// <see cref="NoteVersionTableTests"/> the check constraint on SQL Server.
/// </summary>
public sealed class NoteVersionItemModelTests
{
    [Fact]
    public async Task Its_columns_have_the_types_and_nullability_of_design_md_and_the_text_size_of_Limits()
    {
        var table = TableOf(await NoteVersionItemInTheAppModelAsync());

        (string Name, string Type, bool IsNullable)[] expected =
        [
            ("CommonItemId", "uniqueidentifier", true),
            ("GoalId", "uniqueidentifier", true),
            ("GroupPosition", "int", true),
            ("IsTicked", "bit", false),
            ("NoteVersionId", "uniqueidentifier", false),
            ("OrganisationId", "uniqueidentifier", false),
            ("Position", "int", false),
            ("Section", "tinyint", false),
            ("Text", $"nvarchar({Limits.ItemText})", false),
        ];
        Assert.Equal(
            expected,
            table.Columns
                .Select(column => (column.Name, column.StoreType, column.IsNullable))
                .OrderBy(column => column.Name, StringComparer.Ordinal));
    }

    // The code that saves a version gives all three, so EF Core generates none.
    [Fact]
    public async Task Its_key_is_NoteVersionId_Section_and_Position_and_it_has_no_other()
    {
        var item = await NoteVersionItemInTheAppModelAsync();

        var key = Assert.Single(item.GetKeys());
        Assert.True(key.IsPrimaryKey());
        Assert.Equal(
            [nameof(NoteVersionItem.NoteVersionId), nameof(NoteVersionItem.Section), nameof(NoteVersionItem.Position)],
            key.Properties.Select(p => p.Name));
        Assert.All(key.Properties, property => Assert.Equal(ValueGenerated.Never, property.ValueGenerated));
    }

    [Fact]
    public async Task Its_check_constraint_ties_each_section_to_its_source_and_group_columns()
    {
        var table = TableOf(await NoteVersionItemInTheAppModelAsync());

        var constraint = Assert.Single(table.CheckConstraints);
        Assert.Equal("CK_NoteVersionItem_Section", constraint.Name);
        Assert.Equal(
            "([Section] = 1 AND [GoalId] IS NOT NULL AND [CommonItemId] IS NULL AND [GroupPosition] IS NULL)"
            + " OR ([Section] = 2 AND [CommonItemId] IS NOT NULL AND [GoalId] IS NULL AND [GroupPosition] IS NOT NULL)",
            constraint.Sql);
    }

    // design.md gives the table no index beyond its key, which starts with NoteVersionId alone, so EF Core gives each
    // foreign key one of its own.
    [Fact]
    public async Task It_has_one_index_for_each_foreign_key()
    {
        var table = TableOf(await NoteVersionItemInTheAppModelAsync());

        Assert.Equal(
            [
                ("OrganisationId, CommonItemId", false, null),
                ("OrganisationId, GoalId", false, null),
                ("OrganisationId, NoteVersionId, GroupPosition", false, (string?)null),
            ],
            table.Indexes
                .Select(index => (
                    Columns: string.Join(", ", index.Columns.Select(column => column.Name)),
                    index.IsUnique,
                    index.Filter))
                .OrderBy(index => index.Columns, StringComparer.Ordinal));
    }

    // Through the version's, the goal's, the common item's and the version's group's alternate keys, so a row cannot
    // point at another organisation's. Only the version is required, as a row names a goal or a common item, never
    // both, and a goal row has no group.
    [Fact]
    public async Task It_belongs_to_one_version_and_any_goal_common_item_or_group_of_its_own_organisation()
    {
        var item = await NoteVersionItemInTheAppModelAsync();

        (string Principal, string Columns, string PrincipalKey, bool IsPrimaryKey, bool IsRequired)[] expected =
        [
            (nameof(CommonItem), "OrganisationId, CommonItemId", "OrganisationId, Id", false, false),
            (nameof(Goal), "OrganisationId, GoalId", "OrganisationId, Id", false, false),
            (nameof(NoteVersion), "OrganisationId, NoteVersionId", "OrganisationId, Id", false, true),
            (
                nameof(NoteVersionGroup),
                "OrganisationId, NoteVersionId, GroupPosition",
                "OrganisationId, NoteVersionId, Position",
                false,
                false),
        ];
        Assert.Equal(
            expected,
            item.GetForeignKeys()
                .Select(foreignKey => (
                    Principal: foreignKey.PrincipalEntityType.ClrType.Name,
                    Columns: string.Join(", ", foreignKey.Properties.Select(p => p.Name)),
                    PrincipalKey: string.Join(", ", foreignKey.PrincipalKey.Properties.Select(p => p.Name)),
                    foreignKey.PrincipalKey.IsPrimaryKey(),
                    foreignKey.IsRequired))
                .OrderBy(foreignKey => foreignKey.Columns, StringComparer.Ordinal));
    }

    /// <summary>
    /// <c>NoteVersionItem</c> in the model the app runs with, from a context that the app's own services make, as
    /// <see cref="NoteModelTests"/> takes <c>Note</c>: the design-time copy, which the migrations are made from, as the
    /// copy that queries use leaves out the check constraint.
    /// </summary>
    private static async Task<IEntityType> NoteVersionItemInTheAppModelAsync()
    {
        await using var app = new AppWithoutDatabase("Production");
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();

        var entityType = db.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(NoteVersionItem));
        Assert.NotNull(entityType);
        return entityType;
    }

    private static ITable TableOf(IEntityType entityType) => Assert.Single(entityType.GetTableMappings()).Table;
}
