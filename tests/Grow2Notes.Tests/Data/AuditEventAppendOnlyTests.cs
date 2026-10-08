using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests.Data;

/// <summary>
/// The app refuses to change or delete a saved <see cref="AuditEvent"/> (design.md §5.8 <i>In the app</i>). The tests
/// connect as <c>sa</c>, to which the database's deny does not apply, so only the app's check stands between each save
/// and the row; <see cref="RuntimeRoleTests"/> shows the database's half. Each test has an organisation of its own, and
/// the table has no foreign key, so no organisation row is needed.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class AuditEventAppendOnlyTests(Grow2NotesFactory factory) : IClassFixture<Grow2NotesFactory>
{
    private readonly Guid organisationId = Guid.NewGuid();

    [Fact]
    public async Task A_saved_audit_event_that_is_changed_is_refused_and_its_row_is_left_as_it_was()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var id = await AddAuditEventAsync();
        var before = await StoredRowAsync(id);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        var tenant = scope.ServiceProvider.GetRequiredService<ITenantContext>();

        using (tenant.Use(organisationId))
        {
            var auditEvent = await db.AuditEvents.SingleAsync(row => row.Id == id, cancellationToken);
            auditEvent.Details = """{"name":"Changed"}""";

            await AssertRefusedAsync(db, "changed");
        }

        Assert.Equal(before, await StoredRowAsync(id));
    }

    [Fact]
    public async Task A_saved_audit_event_that_is_removed_is_refused_and_its_row_is_left_as_it_was()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var id = await AddAuditEventAsync();
        var before = await StoredRowAsync(id);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        var tenant = scope.ServiceProvider.GetRequiredService<ITenantContext>();

        using (tenant.Use(organisationId))
        {
            db.AuditEvents.Remove(await db.AuditEvents.SingleAsync(row => row.Id == id, cancellationToken));

            await AssertRefusedAsync(db, "deleted");
        }

        Assert.Equal(before, await StoredRowAsync(id));
    }

    private static async Task AssertRefusedAsync(Grow2NotesDbContext db, string verb)
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            db.SaveChangesAsync(TestContext.Current.CancellationToken));

        Assert.StartsWith(
            $"Nothing was saved: {nameof(AuditEvent)} row to be {verb} is append-only.", ex.Message,
            StringComparison.Ordinal);
    }

    // Saved under the organisation's tenant, with every column set, so the check of the stored row covers them all.
    private async Task<long> AddAuditEventAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        var tenant = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        var auditEvent = new AuditEvent
        {
            OrganisationId = organisationId,
            OccurredAtUtc = new DateTime(2026, 10, 9, 1, 2, 3, 456, DateTimeKind.Utc),
            ActorUserId = Guid.NewGuid(),
            EventType = "participant.updated",
            EntityType = "Participant",
            EntityId = Guid.NewGuid(),
            ParticipantId = Guid.NewGuid(),
            Details = """{"name":"Added"}""",
            IpAddress = "203.0.113.7",
        };
        db.AuditEvents.Add(auditEvent);

        using (tenant.Use(organisationId))
        {
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        return auditEvent.Id;
    }

    // Read in SQL from a scope of its own, so that neither the query filter nor a context's tracked rows decide what is
    // seen.
    private async Task<StoredRow> StoredRowAsync(long id)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        var rows = await db.Database.SqlQuery<StoredRow>($"""
            SELECT [Id], [OrganisationId], [OccurredAtUtc], [ActorUserId], [EventType], [EntityType], [EntityId],
                   [ParticipantId], [Details], [IpAddress]
            FROM [AuditEvent]
            WHERE [Id] = {id}
            """).ToListAsync(TestContext.Current.CancellationToken);

        return Assert.Single(rows);
    }

    private sealed record StoredRow(
        long Id,
        Guid OrganisationId,
        DateTime OccurredAtUtc,
        Guid? ActorUserId,
        string EventType,
        string? EntityType,
        Guid? EntityId,
        Guid? ParticipantId,
        string? Details,
        string? IpAddress);
}
