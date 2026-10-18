using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests.Data;

/// <summary>
/// The <c>Note</c> table in the app's model, as design.md §5.3 sets it out: the composite foreign keys that keep a
/// note's participant, author and discarder within its organisation, the alternate key that its working copies point
/// at (§5.1 <i>Tenancy</i>), the check constraint, and the one-live-note index (§5.5). The migrations create what the
/// model holds: CI fails on a model change without a migration, and so does migrating the test database. Building a
/// model opens no connection, so no test here needs a database. <see cref="ModelTenancyTests"/> checks that the table
/// is tenant-owned and its foreign keys composite, <see cref="RuntimeRoleTests"/> its deny, and
/// <see cref="NoteTableTests"/> the constraint and the index on SQL Server.
/// </summary>
public sealed class NoteModelTests
{
    // Stored in Note.Status and Note.FlagStatus, and named in the check constraint and the index filters, so changing
    // one would change what existing rows mean.
    [Fact]
    public void NoteStatus_and_FlagStatus_keep_their_stored_values()
    {
        Assert.Equal(
            new Dictionary<string, byte> { ["Draft"] = 0, ["Submitted"] = 1, ["Discarded"] = 2 },
            Enum.GetValues<NoteStatus>().ToDictionary(s => s.ToString(), s => (byte)s));
        Assert.Equal(
            new Dictionary<string, byte> { ["None"] = 0, ["ToReview"] = 1, ["Reviewed"] = 2 },
            Enum.GetValues<FlagStatus>().ToDictionary(s => s.ToString(), s => (byte)s));
    }

    [Fact]
    public async Task Its_columns_have_the_types_and_nullability_of_design_md()
    {
        var table = TableOf(await NoteInTheAppModelAsync());

        (string Name, string Type, bool IsNullable)[] expected =
        [
            ("AuthorUserId", "uniqueidentifier", false),
            ("CreatedAtUtc", "datetime2(3)", false),
            ("CurrentVersionNumber", "int", false),
            ("DiscardedAtUtc", "datetime2(3)", true),
            ("DiscardedByUserId", "uniqueidentifier", true),
            ("FirstSubmittedAtUtc", "datetime2(3)", true),
            ("FlagStatus", "tinyint", false),
            ("FlaggedVersionNumber", "int", true),
            ("Id", "uniqueidentifier", false),
            ("NoteDate", "date", false),
            ("OrganisationId", "uniqueidentifier", false),
            ("ParticipantId", "uniqueidentifier", false),
            ("Status", "tinyint", false),
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
        var note = await NoteInTheAppModelAsync();

        Assert.Equal(
            [
                (true, nameof(Note.Id)),
                (false, $"{nameof(Note.OrganisationId)}, {nameof(Note.Id)}"),
            ],
            note.GetKeys()
                .Select(key => (key.IsPrimaryKey(), string.Join(", ", key.Properties.Select(p => p.Name))))
                .OrderByDescending(key => key.Item1));
    }

    [Fact]
    public async Task Its_check_constraint_ties_each_status_to_its_version_submitted_and_discarded_columns()
    {
        var table = TableOf(await NoteInTheAppModelAsync());

        var constraint = Assert.Single(table.CheckConstraints);
        Assert.Equal("CK_Note_Status", constraint.Name);
        Assert.Equal(
            "([Status] = 0 AND [CurrentVersionNumber] = 0 AND [FirstSubmittedAtUtc] IS NULL"
            + " AND [DiscardedAtUtc] IS NULL)"
            + " OR ([Status] = 1 AND [CurrentVersionNumber] >= 1 AND [FirstSubmittedAtUtc] IS NOT NULL"
            + " AND [DiscardedAtUtc] IS NULL)"
            + " OR ([Status] = 2 AND [CurrentVersionNumber] = 0 AND [DiscardedAtUtc] IS NOT NULL"
            + " AND [DiscardedByUserId] IS NOT NULL)",
            constraint.Sql);
    }

    // The unique one-live-note index starts with the participant's foreign key, so that needs no index of its own. EF
    // Core gives each user foreign key one, as no other index starts with its columns.
    [Fact]
    public async Task It_has_the_three_indexes_of_design_md_and_one_for_each_user_foreign_key()
    {
        var table = TableOf(await NoteInTheAppModelAsync());

        (string Columns, bool IsUnique, string? Filter, string Included)[] expected =
        [
            ("OrganisationId, AuthorUserId", false, null, ""),
            ("OrganisationId, DiscardedByUserId", false, null, ""),
            ("OrganisationId, FlagStatus", false, "[FlagStatus] = 1", ""),
            ("OrganisationId, NoteDate", false, null, "Status, ParticipantId, AuthorUserId"),
            ("OrganisationId, ParticipantId, NoteDate", true, "[Status] IN (0, 1)", ""),
        ];
        Assert.Equal(
            expected,
            table.Indexes
                .Select(index => (
                    Columns: string.Join(", ", index.Columns.Select(column => column.Name)),
                    index.IsUnique,
                    index.Filter,
                    Included: string.Join(", ", Assert.Single(index.MappedIndexes).GetIncludeProperties() ?? [])))
                .OrderBy(index => index.Columns, StringComparer.Ordinal));
    }

    // Through the participant's and the users' alternate keys, so a note cannot point at another organisation's
    // participant or user; and to no other table, as the participant's own foreign key gives the organisation. Only the
    // discarder is optional, as a draft or submitted note has none.
    [Fact]
    public async Task It_belongs_to_a_participant_an_author_and_any_discarder_of_its_own_organisation()
    {
        var note = await NoteInTheAppModelAsync();

        (string Principal, string Columns, string PrincipalKey, bool IsPrimaryKey, bool IsRequired)[] expected =
        [
            (nameof(ApplicationUser), "OrganisationId, AuthorUserId", "OrganisationId, Id", false, true),
            (nameof(ApplicationUser), "OrganisationId, DiscardedByUserId", "OrganisationId, Id", false, false),
            (nameof(Participant), "OrganisationId, ParticipantId", "OrganisationId, Id", false, true),
        ];
        Assert.Equal(
            expected,
            note.GetForeignKeys()
                .Select(foreignKey => (
                    Principal: foreignKey.PrincipalEntityType.ClrType.Name,
                    Columns: string.Join(", ", foreignKey.Properties.Select(p => p.Name)),
                    PrincipalKey: string.Join(", ", foreignKey.PrincipalKey.Properties.Select(p => p.Name)),
                    foreignKey.PrincipalKey.IsPrimaryKey(),
                    foreignKey.IsRequired))
                .OrderBy(foreignKey => foreignKey.Columns, StringComparer.Ordinal));
    }

    /// <summary>
    /// <c>Note</c> in the model the app runs with, from a context that the app's own services make, as
    /// <see cref="GoalModelTests"/> takes <c>Goal</c>: the design-time copy, which the migrations are made from, as the
    /// copy that queries use leaves out the check constraint and the included columns.
    /// </summary>
    private static async Task<IEntityType> NoteInTheAppModelAsync()
    {
        await using var app = new AppWithoutDatabase("Production");
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();

        var entityType = db.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(Note));
        Assert.NotNull(entityType);
        return entityType;
    }

    private static ITable TableOf(IEntityType entityType) => Assert.Single(entityType.GetTableMappings()).Table;
}
