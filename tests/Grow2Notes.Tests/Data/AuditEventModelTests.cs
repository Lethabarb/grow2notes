using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests.Data;

/// <summary>
/// The <c>AuditEvent</c> table in the app's model, as design.md §5.3 sets it out. The migrations create what the model
/// holds: CI fails on a model change without a migration, and so does migrating the test database. Building a model
/// opens no connection, so no test here needs a database. <see cref="ModelTenancyTests"/> checks that the table is
/// tenant-owned.
/// </summary>
public sealed class AuditEventModelTests
{
    [Fact]
    public async Task Its_columns_have_the_types_and_nullability_of_design_md_and_the_varchar_sizes_of_Limits()
    {
        var table = TableOf(await AuditEventInTheAppModelAsync());

        (string Name, string Type, bool IsNullable)[] expected =
        [
            ("ActorUserId", "uniqueidentifier", true),
            ("Details", "nvarchar(max)", true),
            ("EntityId", "uniqueidentifier", true),
            ("EntityType", $"varchar({Limits.AuditEntityType})", true),
            ("EventType", $"varchar({Limits.AuditEventType})", false),
            ("Id", "bigint", false),
            ("IpAddress", $"varchar({Limits.IpAddress})", true),
            ("OccurredAtUtc", "datetime2(3)", false),
            ("OrganisationId", "uniqueidentifier", false),
            ("ParticipantId", "uniqueidentifier", true),
        ];
        Assert.Equal(
            expected,
            table.Columns
                .Select(column => (column.Name, column.StoreType, column.IsNullable))
                .OrderBy(column => column.Name, StringComparer.Ordinal));
    }

    [Fact]
    public async Task Its_key_is_Id_alone_a_bigint_identity_that_SQL_Server_sets()
    {
        var auditEvent = await AuditEventInTheAppModelAsync();

        var key = Assert.Single(auditEvent.GetKeys());
        var id = Assert.Single(key.Properties);
        Assert.True(key.IsPrimaryKey());
        Assert.Equal(nameof(AuditEvent.Id), id.Name);
        Assert.Equal("bigint", id.GetColumnType());
        Assert.Equal(SqlServerValueGenerationStrategy.IdentityColumn, id.GetValueGenerationStrategy());
        Assert.Equal(ValueGenerated.OnAdd, id.ValueGenerated);
    }

    [Fact]
    public async Task It_has_the_three_indexes_of_design_md_and_only_the_participant_one_is_filtered()
    {
        var table = TableOf(await AuditEventInTheAppModelAsync());

        (string Columns, bool IsUnique, string? Filter)[] expected =
        [
            ("OrganisationId, ActorUserId, OccurredAtUtc", false, null),
            ("OrganisationId, OccurredAtUtc", false, null),
            ("OrganisationId, ParticipantId, OccurredAtUtc", false, "[ParticipantId] IS NOT NULL"),
        ];
        Assert.Equal(
            expected,
            table.Indexes
                .Select(index => (
                    Columns: string.Join(", ", index.Columns.Select(column => column.Name)),
                    index.IsUnique,
                    index.Filter))
                .OrderBy(index => index.Columns, StringComparer.Ordinal));
    }

    [Fact]
    public async Task It_has_no_foreign_key_to_or_from_another_table()
    {
        var auditEvent = await AuditEventInTheAppModelAsync();
        var table = TableOf(auditEvent);

        Assert.Empty(auditEvent.GetForeignKeys());
        Assert.Empty(auditEvent.GetReferencingForeignKeys());
        Assert.Empty(table.ForeignKeyConstraints);
        Assert.Empty(table.ReferencingForeignKeyConstraints);
    }

    /// <summary>
    /// <c>AuditEvent</c> in the model the app runs with, from a context that the app's own services make, as
    /// <see cref="ModelTenancyTests"/> takes it. EF Core builds that model once and shares it, so it outlives the app.
    /// </summary>
    private static async Task<IEntityType> AuditEventInTheAppModelAsync()
    {
        await using var app = new AppWithoutDatabase("Production");
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();

        var entityType = db.Model.FindEntityType(typeof(AuditEvent));
        Assert.NotNull(entityType);
        return entityType;
    }

    private static ITable TableOf(IEntityType entityType) => Assert.Single(entityType.GetTableMappings()).Table;
}
