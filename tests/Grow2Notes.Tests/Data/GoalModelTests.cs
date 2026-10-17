using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests.Data;

/// <summary>
/// The <c>Goal</c> table in the app's model, as design.md §5.3 sets it out, with the composite foreign key that keeps a
/// goal within its participant's organisation, and the alternate key that a note version's goal rows will point at
/// (§5.1 <i>Tenancy</i>). The migrations create what the model holds: CI fails on a model change without a migration,
/// and so does migrating the test database. Building a model opens no connection, so no test here needs a database.
/// <see cref="ModelTenancyTests"/> checks that the table is tenant-owned and its foreign key composite,
/// <see cref="ModelConventionsTests"/> its <c>RowVersion</c>, and <see cref="RuntimeRoleTests"/> its deny.
/// </summary>
public sealed class GoalModelTests
{
    [Fact]
    public async Task Its_columns_have_the_types_and_nullability_of_design_md_and_the_text_size_of_Limits()
    {
        var table = TableOf(await GoalInTheAppModelAsync());

        (string Name, string Type, bool IsNullable)[] expected =
        [
            ("ArchivedAtUtc", "datetime2(3)", true),
            ("CreatedAtUtc", "datetime2(3)", false),
            ("Id", "uniqueidentifier", false),
            ("OrganisationId", "uniqueidentifier", false),
            ("ParticipantId", "uniqueidentifier", false),
            ("RowVersion", "rowversion", false),
            ("SortOrder", "int", false),
            ("Text", $"nvarchar({Limits.ItemText})", false),
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
        var goal = await GoalInTheAppModelAsync();

        Assert.Equal(
            [
                (true, nameof(Goal.Id)),
                (false, $"{nameof(Goal.OrganisationId)}, {nameof(Goal.Id)}"),
            ],
            goal.GetKeys()
                .Select(key => (key.IsPrimaryKey(), string.Join(", ", key.Properties.Select(p => p.Name))))
                .OrderByDescending(key => key.Item1));
    }

    // The foreign key needs no index of its own: the one index starts with its columns.
    [Fact]
    public async Task It_has_the_one_index_of_design_md()
    {
        var table = TableOf(await GoalInTheAppModelAsync());

        var index = Assert.Single(table.Indexes);
        Assert.Equal(
            ["OrganisationId", "ParticipantId", "ArchivedAtUtc", "SortOrder"],
            index.Columns.Select(column => column.Name));
        Assert.False(index.IsUnique);
        Assert.Null(index.Filter);
    }

    // Through the participant's alternate key, so a goal cannot point at another organisation's participant; and to no
    // other table, as the participant's own foreign key gives the organisation.
    [Fact]
    public async Task It_belongs_to_one_participant_of_its_own_organisation()
    {
        var goal = await GoalInTheAppModelAsync();

        var participant = Assert.Single(goal.GetForeignKeys());
        Assert.Equal(typeof(Participant), participant.PrincipalEntityType.ClrType);
        Assert.Equal(
            [nameof(Goal.OrganisationId), nameof(Goal.ParticipantId)], participant.Properties.Select(p => p.Name));
        Assert.Equal(
            [nameof(Participant.OrganisationId), nameof(Participant.Id)],
            participant.PrincipalKey.Properties.Select(p => p.Name));
        Assert.False(participant.PrincipalKey.IsPrimaryKey());
        Assert.True(participant.IsRequired);
    }

    /// <summary>
    /// <c>Goal</c> in the model the app runs with, from a context that the app's own services make, as
    /// <see cref="ParticipantModelTests"/> takes <c>Participant</c>.
    /// </summary>
    private static async Task<IEntityType> GoalInTheAppModelAsync()
    {
        await using var app = new AppWithoutDatabase("Production");
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();

        var entityType = db.Model.FindEntityType(typeof(Goal));
        Assert.NotNull(entityType);
        return entityType;
    }

    private static ITable TableOf(IEntityType entityType) => Assert.Single(entityType.GetTableMappings()).Table;
}
