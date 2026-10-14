using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests.Platform.Audit;

/// <summary>
/// The shared audit-events test (design.md §5.3, §5.4, §14 M5; S05.02.01): one table-driven test with a row for each
/// event type of the catalogue, which triggers the event through the real endpoint, as the app's callers do, and finds
/// exactly one new entry, with its <c>EventType</c>, <c>OrganisationId</c>, <c>ActorUserId</c>, <c>EntityType</c>,
/// <c>EntityId</c>, <c>ParticipantId</c> and the values in its <c>Details</c>. Each task that writes an event adds its
/// row here, and T24 completes the table. Rows act in seeded organisation A and add rows of their own to it, changing
/// no seeded row (tests/README.md).
/// </summary>
[Collection<SqlServerCollection>]
public sealed class AuditEventsTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    : IClassFixture<Grow2NotesFactory>
{
    /// <summary>
    /// Each event type's row: what triggers it, through the real endpoint, which gives the entry that it must write.
    /// </summary>
    private static readonly Dictionary<string, Func<AuditEventsTests, Task<WrittenEvent>>> Rows = new()
    {
        [AuditEventTypes.Participant.Created] = test => test.AddParticipantAsync(),
    };

    private readonly SeededOrganisation a = sqlServer.Seeded.A;

    public static TheoryData<string> EventTypes { get; } = new(Rows.Keys);

    [Theory]
    [MemberData(nameof(EventTypes))]
    public async Task Its_trigger_writes_exactly_one_entry_with_its_fields_and_values(string eventType)
    {
        var lastEventId = await LastEventIdAsync();

        var expected = await Rows[eventType](this);

        var written = Assert.Single(await EventsAfterAsync(lastEventId));
        Assert.Equal(eventType, expected.EventType);
        Assert.Equal(expected with { Details = null }, written with { Details = null });
        Assert.True(
            JsonNode.DeepEquals(Parse(expected.Details), Parse(written.Details)),
            $"Details: expected {expected.Details}, written {written.Details}.");
    }

    private static JsonNode? Parse(string? details) => details is null ? null : JsonNode.Parse(details);

    /// <summary>
    /// <c>participant.created</c>: A's manager adds a participant with <c>POST /api/admin/participants</c>, which
    /// records the new participant as the entity and the participant, and their values as <c>new</c> alone, each named
    /// as design.md §6.6's list names it, the names as stored once their ends are trimmed.
    /// </summary>
    private async Task<WrittenEvent> AddParticipantAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = factory.CreateHttpsClient().SignInAs(a.ManagerId);
        using var response = await client.PostWithTokenAsync(
            "/api/admin/participants",
            await client.FetchTokenAsync(),
            new { givenName = " Jane", familyName = "Citizen ", dateOfBirth = "1987-03-07" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var id = (await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("id").GetGuid();

        var values = new JsonObject
        {
            ["givenName"] = "Jane",
            ["familyName"] = "Citizen",
            ["dateOfBirth"] = "1987-03-07",
            ["status"] = "Active",
        };
        return new(
            AuditEventTypes.Participant.Created,
            a.OrganisationId,
            a.ManagerId,
            AuditEntityTypes.Participant,
            id,
            id,
            new JsonObject { ["new"] = values }.ToJsonString());
    }

    // The ID of the last event written, by any test. The tests of the collection run one at a time, so the events after
    // it are the row's alone.
    private async Task<long> LastEventIdAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Database
            .SqlQuery<long>($"SELECT COALESCE(MAX([Id]), 0) AS [Value] FROM [AuditEvent]")
            .SingleAsync(TestContext.Current.CancellationToken);
    }

    // Read in SQL, so the query filter, which would need a tenant, does not decide what is seen.
    private async Task<List<WrittenEvent>> EventsAfterAsync(long eventId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        return await db.Database.SqlQuery<WrittenEvent>($"""
            SELECT [EventType], [OrganisationId], [ActorUserId], [EntityType], [EntityId], [ParticipantId], [Details]
            FROM [AuditEvent]
            WHERE [Id] > {eventId}
            ORDER BY [Id]
            """).ToListAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>An audit entry's fields that a row checks, its <c>Details</c> as the JSON stored.</summary>
    private sealed record WrittenEvent(
        string EventType,
        Guid OrganisationId,
        Guid? ActorUserId,
        string? EntityType,
        Guid? EntityId,
        Guid? ParticipantId,
        string? Details);
}
