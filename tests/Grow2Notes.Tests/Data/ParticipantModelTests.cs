using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests.Data;

/// <summary>
/// The <c>Participant</c> table in the app's model, as design.md §5.3 sets it out, with the alternate key that a
/// participant's goals and notes point at (§5.1 <i>Tenancy</i>). The migrations create what the model holds: CI fails
/// on a model change without a migration, and so does migrating the test database. Building a model opens no
/// connection, so no test here needs a database. <see cref="ModelTenancyTests"/> checks that the table is tenant-owned,
/// <see cref="ModelConventionsTests"/> its <c>RowVersion</c>, and <see cref="RuntimeRoleTests"/> its deny.
/// </summary>
public sealed class ParticipantModelTests
{
    // Stored in Participant.Status, so changing one would change what existing rows mean.
    [Fact]
    public void ParticipantStatus_keeps_its_stored_values() =>
        Assert.Equal(
            new Dictionary<string, byte> { ["Active"] = 1, ["Archived"] = 2 },
            Enum.GetValues<ParticipantStatus>().ToDictionary(s => s.ToString(), s => (byte)s));

    // Nothing else: no NDIS number or other government identifier (A5, §12).
    [Fact]
    public async Task Its_columns_have_the_types_and_nullability_of_design_md_and_the_name_sizes_of_Limits()
    {
        var table = TableOf(await ParticipantInTheAppModelAsync());

        (string Name, string Type, bool IsNullable)[] expected =
        [
            ("ArchivedAtUtc", "datetime2(3)", true),
            ("CreatedAtUtc", "datetime2(3)", false),
            ("DateOfBirth", "date", false),
            ("FamilyName", $"nvarchar({Limits.ParticipantName})", false),
            ("GivenName", $"nvarchar({Limits.ParticipantName})", false),
            ("Id", "uniqueidentifier", false),
            ("OrganisationId", "uniqueidentifier", false),
            ("RowVersion", "rowversion", false),
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
        var participant = await ParticipantInTheAppModelAsync();

        Assert.Equal(
            [
                (true, nameof(Participant.Id)),
                (false, $"{nameof(Participant.OrganisationId)}, {nameof(Participant.Id)}"),
            ],
            participant.GetKeys()
                .Select(key => (key.IsPrimaryKey(), string.Join(", ", key.Properties.Select(p => p.Name))))
                .OrderByDescending(key => key.Item1));
    }

    // The foreign key to Organisation needs no index of its own: the alternate key starts with OrganisationId.
    [Fact]
    public async Task It_has_the_one_index_of_design_md()
    {
        var table = TableOf(await ParticipantInTheAppModelAsync());

        var index = Assert.Single(table.Indexes);
        Assert.Equal(
            ["OrganisationId", "Status", "FamilyName", "GivenName"], index.Columns.Select(column => column.Name));
        Assert.False(index.IsUnique);
        Assert.Null(index.Filter);
    }

    [Fact]
    public async Task It_belongs_to_one_organisation_by_OrganisationId_alone()
    {
        var participant = await ParticipantInTheAppModelAsync();

        var organisation = Assert.Single(participant.GetForeignKeys());
        Assert.Equal(typeof(Organisation), organisation.PrincipalEntityType.ClrType);
        Assert.Equal([nameof(Participant.OrganisationId)], organisation.Properties.Select(p => p.Name));
        Assert.True(organisation.PrincipalKey.IsPrimaryKey());
        Assert.True(organisation.IsRequired);
    }

    /// <summary>
    /// <c>Participant</c> in the model the app runs with, from a context that the app's own services make, as
    /// <see cref="ModelTenancyTests"/> takes it. EF Core builds that model once and shares it, so it outlives the app.
    /// </summary>
    private static async Task<IEntityType> ParticipantInTheAppModelAsync()
    {
        await using var app = new AppWithoutDatabase("Production");
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();

        var entityType = db.Model.FindEntityType(typeof(Participant));
        Assert.NotNull(entityType);
        return entityType;
    }

    private static ITable TableOf(IEntityType entityType) => Assert.Single(entityType.GetTableMappings()).Table;
}
