using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests.Data;

/// <summary>
/// The <c>NoteReview</c> table in the app's model, as design.md §5.3 sets it out: the composite foreign keys that keep
/// a review within its note's and its reviewer's organisation (§5.1 <i>Tenancy</i>), none to the reviewed version, and
/// the indexes that the read view and the Reviewed list use. The migrations create what the model holds: CI fails on a
/// model change without a migration, and so does migrating the test database. Building a model opens no connection, so
/// no test here needs a database. <see cref="ModelTenancyTests"/> checks that the table is tenant-owned and its foreign
/// keys composite, <see cref="RuntimeRoleTests"/> its denies, and <see cref="NoteReviewTableTests"/> the foreign keys
/// on SQL Server.
/// </summary>
public sealed class NoteReviewModelTests
{
    [Fact]
    public async Task Its_columns_have_the_types_and_nullability_of_design_md_and_the_comment_size_of_Limits()
    {
        var table = TableOf(await NoteReviewInTheAppModelAsync());

        (string Name, string Type, bool IsNullable)[] expected =
        [
            ("Comment", $"nvarchar({Limits.ReviewComment})", true),
            ("Id", "uniqueidentifier", false),
            ("NoteId", "uniqueidentifier", false),
            ("OrganisationId", "uniqueidentifier", false),
            ("ReviewedAtUtc", "datetime2(3)", false),
            ("ReviewedByUserId", "uniqueidentifier", false),
            ("VersionNumber", "int", false),
        ];
        Assert.Equal(500, Limits.ReviewComment);
        Assert.Equal(
            expected,
            table.Columns
                .Select(column => (column.Name, column.StoreType, column.IsNullable))
                .OrderBy(column => column.Name, StringComparer.Ordinal));
    }

    [Fact]
    public async Task It_is_the_NoteReview_table_with_Id_as_its_only_key_and_is_tenant_owned_and_append_only()
    {
        var review = await NoteReviewInTheAppModelAsync();

        Assert.Equal("NoteReview", review.GetTableName());
        Assert.Equal(
            [(true, nameof(NoteReview.Id))],
            review.GetKeys()
                .Select(key => (key.IsPrimaryKey(), string.Join(", ", key.Properties.Select(p => p.Name)))));
        Assert.True(typeof(ITenantOwned).IsAssignableFrom(review.ClrType));
        Assert.True(typeof(IAppendOnly).IsAssignableFrom(review.ClrType));
    }

    // The two indexes of design.md §5.3; EF Core gives each foreign key one more, as its columns start neither.
    [Fact]
    public async Task It_has_the_two_indexes_of_design_md_and_one_for_each_foreign_key()
    {
        var table = TableOf(await NoteReviewInTheAppModelAsync());

        Assert.Equal(
            [
                ("NoteId, ReviewedAtUtc", false, null),
                ("OrganisationId, NoteId", false, null),
                ("OrganisationId, ReviewedAtUtc", false, null),
                ("OrganisationId, ReviewedByUserId", false, (string?)null),
            ],
            table.Indexes
                .Select(index => (
                    Columns: string.Join(", ", index.Columns.Select(column => column.Name)),
                    index.IsUnique,
                    index.Filter))
                .OrderBy(index => index.Columns, StringComparer.Ordinal));
    }

    // Through the note's and the user's alternate keys, so a review cannot point at another organisation's note or
    // user; Restrict, so that nothing is removed with them.
    [Fact]
    public async Task It_belongs_to_one_note_and_one_reviewer_of_its_own_organisation_and_removes_neither()
    {
        var review = await NoteReviewInTheAppModelAsync();

        (string Principal, string Columns, string PrincipalKey, bool IsRequired, DeleteBehavior OnDelete)[] expected =
        [
            (nameof(Note), "OrganisationId, NoteId", "OrganisationId, Id", true, DeleteBehavior.Restrict),
            (nameof(ApplicationUser), "OrganisationId, ReviewedByUserId", "OrganisationId, Id", true,
                DeleteBehavior.Restrict),
        ];
        Assert.Equal(
            expected,
            review.GetForeignKeys()
                .Select(foreignKey => (
                    Principal: foreignKey.PrincipalEntityType.ClrType.Name,
                    Columns: string.Join(", ", foreignKey.Properties.Select(p => p.Name)),
                    PrincipalKey: string.Join(", ", foreignKey.PrincipalKey.Properties.Select(p => p.Name)),
                    foreignKey.IsRequired,
                    foreignKey.DeleteBehavior))
                .OrderBy(foreignKey => foreignKey.Columns, StringComparer.Ordinal));
    }

    /// <summary>
    /// <c>NoteReview</c> in the model the app runs with, from a context that the app's own services make, as
    /// <see cref="NoteVersionModelTests"/> takes <c>NoteVersion</c>.
    /// </summary>
    private static async Task<IEntityType> NoteReviewInTheAppModelAsync()
    {
        await using var app = new AppWithoutDatabase("Production");
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();

        var entityType = db.Model.FindEntityType(typeof(NoteReview));
        Assert.NotNull(entityType);
        return entityType;
    }

    private static ITable TableOf(IEntityType entityType) => Assert.Single(entityType.GetTableMappings()).Table;
}
