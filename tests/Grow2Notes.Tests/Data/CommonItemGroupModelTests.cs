using System.Reflection;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests.Data;

/// <summary>
/// The <c>CommonItemGroup</c> table in the app's model, as design.md §5.3 sets it out with its <i>Constraints</i> and
/// §5.10 sketches it, with the alternate key that the group's items and a note version's group rows point at (§5.1
/// <i>Tenancy</i>); and the built-in Every note group, which only <see cref="CommonItemGroup.EveryNote"/> makes (A43).
/// The migrations create what the model holds: CI fails on a model change without a migration, and so does migrating
/// the test database. Building a model opens no connection, so no test here needs a database.
/// <see cref="ModelTenancyTests"/> checks that the table is tenant-owned, <see cref="ModelConventionsTests"/> its
/// <c>RowVersion</c>, <see cref="RuntimeRoleTests"/> its deny, and <see cref="EveryNoteGroupTests"/> its filtered index
/// and check constraint on SQL Server.
/// </summary>
public sealed class CommonItemGroupModelTests
{
    private static readonly DateTime CreatedAtUtc = new(2026, 10, 18, 1, 2, 3, 456, DateTimeKind.Utc);

    [Fact]
    public async Task Its_columns_have_the_types_and_nullability_of_design_md_and_the_name_size_of_Limits()
    {
        var table = TableOf(await GroupInTheAppModelAsync());

        (string Name, string Type, bool IsNullable)[] expected =
        [
            ("ArchivedAtUtc", "datetime2(3)", true),
            ("CreatedAtUtc", "datetime2(3)", false),
            ("Id", "uniqueidentifier", false),
            ("IsEveryNote", "bit", false),
            ("Name", $"nvarchar({Limits.GroupName})", false),
            ("OrganisationId", "uniqueidentifier", false),
            ("RowVersion", "rowversion", false),
            ("SortOrder", "int", false),
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
        var group = await GroupInTheAppModelAsync();

        Assert.Equal(
            [
                (true, nameof(CommonItemGroup.Id)),
                (false, $"{nameof(CommonItemGroup.OrganisationId)}, {nameof(CommonItemGroup.Id)}"),
            ],
            group.GetKeys()
                .Select(key => (key.IsPrimaryKey(), string.Join(", ", key.Properties.Select(p => p.Name))))
                .OrderByDescending(key => key.Item1));
    }

    // The foreign key to Organisation needs no index of its own: the alternate key starts with OrganisationId.
    [Fact]
    public async Task It_has_the_list_index_and_the_unique_index_that_allows_one_Every_note_group_per_organisation()
    {
        var table = TableOf(await GroupInTheAppModelAsync());

        Assert.Equal(
            [
                ("OrganisationId", true, "[IsEveryNote] = 1"),
                ("OrganisationId, ArchivedAtUtc, SortOrder", false, null),
            ],
            table.Indexes
                .Select(index => (
                    string.Join(", ", index.Columns.Select(column => column.Name)), index.IsUnique, index.Filter))
                .OrderBy(index => index.Item1, StringComparer.Ordinal));
    }

    [Fact]
    public async Task Its_check_constraint_keeps_Every_note_active()
    {
        var table = TableOf(await GroupInTheAppModelAsync(designTime: true));

        var constraint = Assert.Single(table.CheckConstraints);
        Assert.Equal(
            ("CK_CommonItemGroup_EveryNoteActive", "[IsEveryNote] = 0 OR [ArchivedAtUtc] IS NULL"),
            (constraint.Name, constraint.Sql));
    }

    [Fact]
    public async Task It_belongs_to_one_organisation_by_OrganisationId_alone()
    {
        var group = await GroupInTheAppModelAsync();

        var organisation = Assert.Single(group.GetForeignKeys());
        Assert.Equal(typeof(Organisation), organisation.PrincipalEntityType.ClrType);
        Assert.Equal([nameof(CommonItemGroup.OrganisationId)], organisation.Properties.Select(p => p.Name));
        Assert.True(organisation.PrincipalKey.IsPrimaryKey());
        Assert.True(organisation.IsRequired);
        Assert.False(organisation.IsUnique);
    }

    [Fact]
    public void EveryNote_makes_the_organisation_s_active_Every_note_group_with_no_ID_until_it_is_added()
    {
        var organisationId = Guid.NewGuid();

        var group = CommonItemGroup.EveryNote(organisationId, CreatedAtUtc);

        Assert.Equal(
            (Guid.Empty, organisationId, "Every note", 0, true, (DateTime?)null, CreatedAtUtc),
            (group.Id, group.OrganisationId, group.Name, group.SortOrder, group.IsEveryNote, group.ArchivedAtUtc,
                group.CreatedAtUtc));
    }

    [Fact]
    public void Any_other_group_made_in_code_is_not_Every_note()
    {
        var group = new CommonItemGroup
        {
            OrganisationId = Guid.NewGuid(),
            Name = "Community outing",
            CreatedAtUtc = CreatedAtUtc,
        };

        Assert.False(group.IsEveryNote);
    }

    // So EveryNote is the only way that code, an endpoint's included, can make an Every note group (S01.03.01's sixth
    // criterion). EF Core still sets the private setter as it reads a row.
    [Fact]
    public void IsEveryNote_has_no_public_setter()
    {
        var property = typeof(CommonItemGroup).GetProperty(
            nameof(CommonItemGroup.IsEveryNote), BindingFlags.Public | BindingFlags.Instance);

        Assert.NotNull(property);
        Assert.False(property.SetMethod?.IsPublic ?? false);
    }

    /// <summary>
    /// <c>CommonItemGroup</c> in the model the app runs with, from a context that the app's own services make, as
    /// <see cref="GoalModelTests"/> takes <c>Goal</c>; or, with <paramref name="designTime"/>, in the design-time model
    /// that migrations read, which alone holds what only they need, such as check constraints.
    /// </summary>
    private static async Task<IEntityType> GroupInTheAppModelAsync(bool designTime = false)
    {
        await using var app = new AppWithoutDatabase("Production");
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();

        var model = designTime ? db.GetService<IDesignTimeModel>().Model : db.Model;
        var entityType = model.FindEntityType(typeof(CommonItemGroup));
        Assert.NotNull(entityType);
        return entityType;
    }

    private static ITable TableOf(IEntityType entityType) => Assert.Single(entityType.GetTableMappings()).Table;
}
