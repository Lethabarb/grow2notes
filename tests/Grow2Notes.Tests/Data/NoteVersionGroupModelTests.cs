using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests.Data;

/// <summary>
/// The <c>NoteVersionGroup</c> table in the app's model, as design.md §5.3 sets it out: one row per common item group
/// in a version's snapshot, with composite foreign keys that keep it within its version's and its group's
/// organisation (§5.1 <i>Tenancy</i>), the alternate key that the version's common item rows point at, and the check
/// constraint that keeps Every note from being picked. The migrations create what the model holds: CI fails on a model
/// change without a migration, and so does migrating the test database. Building a model opens no connection, so no
/// test here needs a database. <see cref="ModelTenancyTests"/> checks that the table is tenant-owned and its foreign
/// keys composite, <see cref="RuntimeRoleTests"/> its denies, and <see cref="NoteVersionTableTests"/> the constraints
/// on SQL Server.
/// </summary>
public sealed class NoteVersionGroupModelTests
{
    [Fact]
    public async Task Its_columns_have_the_types_and_nullability_of_design_md_and_the_name_size_of_Limits()
    {
        var table = TableOf(await NoteVersionGroupInTheAppModelAsync());

        (string Name, string Type, bool IsNullable)[] expected =
        [
            ("CommonItemGroupId", "uniqueidentifier", false),
            ("IsEveryNote", "bit", false),
            ("IsPicked", "bit", false),
            ("Name", $"nvarchar({Limits.GroupName})", false),
            ("NoteVersionId", "uniqueidentifier", false),
            ("OrganisationId", "uniqueidentifier", false),
            ("Position", "int", false),
        ];
        Assert.Equal(
            expected,
            table.Columns
                .Select(column => (column.Name, column.StoreType, column.IsNullable))
                .OrderBy(column => column.Name, StringComparer.Ordinal));
    }

    // The code that saves a version gives both, so EF Core generates neither. OrganisationId, NoteVersionId and
    // Position are the alternate key that the version's common item rows point at.
    [Fact]
    public async Task Its_key_is_NoteVersionId_and_Position_with_OrganisationId_too_an_alternate_key()
    {
        var group = await NoteVersionGroupInTheAppModelAsync();

        Assert.Equal(
            [
                (true, "NoteVersionId, Position"),
                (false, "OrganisationId, NoteVersionId, Position"),
            ],
            group.GetKeys()
                .Select(key => (key.IsPrimaryKey(), string.Join(", ", key.Properties.Select(p => p.Name))))
                .OrderByDescending(key => key.Item1));
        var primaryKey = Assert.IsAssignableFrom<IKey>(group.FindPrimaryKey());
        Assert.All(primaryKey.Properties, property => Assert.Equal(ValueGenerated.Never, property.ValueGenerated));
    }

    [Fact]
    public async Task Its_check_constraint_keeps_Every_note_from_being_picked()
    {
        var table = TableOf(await NoteVersionGroupInTheAppModelAsync());

        var constraint = Assert.Single(table.CheckConstraints);
        Assert.Equal("CK_NoteVersionGroup_EveryNoteNotPicked", constraint.Name);
        Assert.Equal("[IsEveryNote] = 0 OR [IsPicked] = 0", constraint.Sql);
    }

    // The alternate key starts with the version's foreign key's columns, so only the group's foreign key needs an
    // index of its own.
    [Fact]
    public async Task It_has_one_index_for_the_group_foreign_key_and_none_for_the_version_foreign_key()
    {
        var table = TableOf(await NoteVersionGroupInTheAppModelAsync());

        var index = Assert.Single(table.Indexes);
        Assert.Equal(
            ("OrganisationId, CommonItemGroupId", false, null),
            (string.Join(", ", index.Columns.Select(column => column.Name)), index.IsUnique, index.Filter));
    }

    // Through the version's and the group's alternate keys, so a row cannot point at another organisation's.
    [Fact]
    public async Task It_belongs_to_one_version_and_one_group_of_its_own_organisation()
    {
        var group = await NoteVersionGroupInTheAppModelAsync();

        (string Principal, string Columns, string PrincipalKey, bool IsPrimaryKey, bool IsRequired)[] expected =
        [
            (nameof(CommonItemGroup), "OrganisationId, CommonItemGroupId", "OrganisationId, Id", false, true),
            (nameof(NoteVersion), "OrganisationId, NoteVersionId", "OrganisationId, Id", false, true),
        ];
        Assert.Equal(
            expected,
            group.GetForeignKeys()
                .Select(foreignKey => (
                    Principal: foreignKey.PrincipalEntityType.ClrType.Name,
                    Columns: string.Join(", ", foreignKey.Properties.Select(p => p.Name)),
                    PrincipalKey: string.Join(", ", foreignKey.PrincipalKey.Properties.Select(p => p.Name)),
                    foreignKey.PrincipalKey.IsPrimaryKey(),
                    foreignKey.IsRequired))
                .OrderBy(foreignKey => foreignKey.Columns, StringComparer.Ordinal));
        Assert.All(
            group.GetForeignKeys(),
            foreignKey => Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior));
    }

    [Fact]
    public void It_is_only_ever_added_and_belongs_to_one_organisation()
    {
        Assert.True(typeof(IAppendOnly).IsAssignableFrom(typeof(NoteVersionGroup)));
        Assert.True(typeof(ITenantOwned).IsAssignableFrom(typeof(NoteVersionGroup)));
    }

    /// <summary>
    /// <c>NoteVersionGroup</c> in the model the app runs with, from a context that the app's own services make, as
    /// <see cref="NoteVersionItemModelTests"/> takes <c>NoteVersionItem</c>: the design-time copy, which the migrations
    /// are made from, as the copy that queries use leaves out the check constraint.
    /// </summary>
    private static async Task<IEntityType> NoteVersionGroupInTheAppModelAsync()
    {
        await using var app = new AppWithoutDatabase("Production");
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();

        var entityType = db.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(NoteVersionGroup));
        Assert.NotNull(entityType);
        return entityType;
    }

    private static ITable TableOf(IEntityType entityType) => Assert.Single(entityType.GetTableMappings()).Table;
}
