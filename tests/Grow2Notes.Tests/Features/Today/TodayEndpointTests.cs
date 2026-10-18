using System.Data.SqlTypes;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Grow2Notes.Tests.Features.Today;

/// <summary>
/// <c>GET /api/today</c> (design.md §6.3), called with the test-only sign-in (<see cref="TestSignIn"/>): every
/// signed-in user gets the date in Melbourne and their own organisation's active participants, and none of the other's
/// (§5.9 item 7), sorted by family name, then given name (A5), then ID, as JSON that is never stored (§6.1), and no
/// call writes an audit event (A28). The tests add their own participants to the seeded organisations, and change no
/// seeded row (tests/README.md). The endpoint matrix covers who may call it, and <see cref="SignedOutTests"/> what a
/// signed-out caller gets.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class TodayEndpointTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    : IClassFixture<Grow2NotesFactory>
{
    private const string Path = "/api/today";

    // Made up (A35), with milliseconds, which datetime2(3) keeps.
    private static readonly DateTime CreatedAtUtc = new(2026, 10, 2, 3, 4, 5, 678, DateTimeKind.Utc);

    // SQL Server's order of uniqueidentifier values, which compares their last six bytes first, unlike Guid's.
    private static readonly Comparer<Guid> SqlServerGuidOrder =
        Comparer<Guid>.Create((x, y) => new SqlGuid(x).CompareTo(new SqlGuid(y)));

    private readonly SeededOrganisations seeded = sqlServer.Seeded;

    [Fact]
    public async Task A_s_worker_and_manager_get_the_same_list_of_A_s_active_participants_and_none_of_B_s()
    {
        var added = await AddAsync(seeded.A, ("Jane", "Citizen", ParticipantStatus.Active));

        var worker = await ListAsync(Caller.WorkerOfA);
        var manager = await ListAsync(Caller.ManagerOfA);

        Assert.Equal(worker.GetRawText(), manager.GetRawText());
        var ids = ParticipantIds(worker);
        Assert.Equal((await StoredActiveIdsAsync(seeded.A)).Order(), ids.Order());
        Assert.Contains(seeded.A.ParticipantId, ids);
        Assert.Contains(added[0], ids);
        Assert.DoesNotContain(seeded.B.ParticipantId, ids);
    }

    [Fact]
    public async Task B_s_manager_gets_B_s_active_participants_and_none_of_A_s()
    {
        var addedToA = await AddAsync(seeded.A, ("Jane", "Citizen", ParticipantStatus.Active));
        var addedToB = await AddAsync(seeded.B, ("Priya", "Kumar", ParticipantStatus.Active));

        var ids = ParticipantIds(await ListAsync(Caller.ManagerOfB));

        Assert.Equal((await StoredActiveIdsAsync(seeded.B)).Order(), ids.Order());
        Assert.Contains(seeded.B.ParticipantId, ids);
        Assert.Contains(addedToB[0], ids);
        Assert.DoesNotContain(seeded.A.ParticipantId, ids);
        Assert.DoesNotContain(addedToA[0], ids);
    }

    [Fact]
    public async Task An_archived_participant_is_left_out()
    {
        var added = await AddAsync(
            seeded.A, ("Sam", "Active", ParticipantStatus.Active), ("Sam", "Archived", ParticipantStatus.Archived));

        var ids = ParticipantIds(await ListAsync(Caller.WorkerOfA));

        Assert.Contains(added[0], ids);
        Assert.DoesNotContain(added[1], ids);
    }

    // By family name, then given name, in SQL Server's collation, which ignores case, so "de Silva" sorts between
    // "Citizen" and "Ng", where an ordinal sort would put it last; then two people of one name by ID, in SQL Server's
    // order of IDs. Neither the order they were added in nor given names first gives the same order.
    [Fact]
    public async Task The_list_is_sorted_by_family_name_then_given_name_then_ID()
    {
        var added = await AddAsync(
            seeded.A,
            ("Adam", "Ng", ParticipantStatus.Active),
            ("Zoë", "de Silva", ParticipantStatus.Active),
            ("Jane", "Citizen", ParticipantStatus.Active),
            ("Bea", "Citizen", ParticipantStatus.Active),
            ("Sam", "Twin", ParticipantStatus.Active),
            ("Sam", "Twin", ParticipantStatus.Active));

        var ids = ParticipantIds(await ListAsync(Caller.WorkerOfA));

        Assert.Equal(
            [added[3], added[2], added[1], added[0], .. added[4..].Order(SqlServerGuidOrder)],
            ids.Where(added.Contains));
    }

    // design.md §6.3's shape, until T09 adds each row's note: the names as typed, accents and all, and never the date
    // of birth or the status.
    [Fact]
    public async Task The_answer_has_the_date_and_each_participant_s_ID_and_names_as_stored()
    {
        var added = await AddAsync(seeded.A, ("Nguyễn", "O’Brien-Smith", ParticipantStatus.Active));

        var list = await ListAsync(Caller.WorkerOfA);

        Assert.Equal(["date", "participants"], list.EnumerateObject().Select(property => property.Name));
        var participant = list.GetProperty("participants").EnumerateArray().Single(row => Id(row) == added[0]);
        Assert.Equal(
            ["id", "givenName", "familyName"], participant.EnumerateObject().Select(property => property.Name));
        Assert.Equal("Nguyễn", participant.GetProperty("givenName").GetString());
        Assert.Equal("O’Brien-Smith", participant.GetProperty("familyName").GetString());
    }

    [Fact]
    public async Task The_answer_is_JSON_that_is_never_stored()
    {
        using var client = ClientFor(Caller.WorkerOfA);

        using var response = await client.GetAsync(Path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.False(response.Headers.Contains("ETag"));
    }

    // design.md §5.4 lists no event for reading (A28).
    [Fact]
    public async Task The_calls_write_no_audit_event()
    {
        var lastEventId = await LastEventIdAsync();

        foreach (var caller in new[] { Caller.WorkerOfA, Caller.ManagerOfA, Caller.ManagerOfB })
        {
            await ListAsync(caller);
        }

        Assert.Equal(lastEventId, await LastEventIdAsync());
    }

    // 14:30 UTC on 3 October is half past midnight on 4 October in Melbourne, still in standard time (UTC+10), as
    // daylight saving starts at 2 am that day.
    [Fact]
    public async Task The_date_is_today_in_Melbourne_where_it_differs_from_the_date_in_UTC()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 3, 14, 30, 0, TimeSpan.Zero));
        await using var app = factory.WithWebHostBuilder(builder => builder.UseClock(clock));
        using var client = app.CreateHttpsClient().SignInAs(Caller.WorkerOfA, seeded);

        var list = await ReadListAsync(client);

        Assert.Equal("2026-10-04", list.GetProperty("date").GetString());
    }

    private static Guid Id(JsonElement participant) => participant.GetProperty("id").GetGuid();

    // The list's participants' IDs, in its order.
    private static List<Guid> ParticipantIds(JsonElement list) =>
        [.. list.GetProperty("participants").EnumerateArray().Select(Id)];

    private static async Task<JsonElement> ReadListAsync(HttpClient client)
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        using var response = await client.GetAsync(Path, cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
    }

    // Over HTTPS, as the SPA calls the /api group (tests/README.md).
    private HttpClient ClientFor(Caller caller) => factory.CreateHttpsClient().SignInAs(caller, seeded);

    private async Task<JsonElement> ListAsync(Caller caller)
    {
        using var client = ClientFor(caller);
        return await ReadListAsync(client);
    }

    /// <summary>
    /// Adds the participants to <paramref name="organisation"/>, through the app's context under its tenant, as the app
    /// adds them, each born on 7 March 1987, and an archived one archived as Archive leaves them, and gives their IDs
    /// in the order given.
    /// </summary>
    private async Task<List<Guid>> AddAsync(
        SeededOrganisation organisation,
        params (string GivenName, string FamilyName, ParticipantStatus Status)[] people)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        var participants = people.Select(person => new Participant
        {
            OrganisationId = organisation.OrganisationId,
            GivenName = person.GivenName,
            FamilyName = person.FamilyName,
            DateOfBirth = new DateOnly(1987, 3, 7),
            Status = person.Status,
            ArchivedAtUtc = person.Status == ParticipantStatus.Archived ? CreatedAtUtc.AddDays(1) : null,
            CreatedAtUtc = CreatedAtUtc,
        }).ToList();
        db.Participants.AddRange(participants);
        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(organisation.OrganisationId))
        {
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        return [.. participants.Select(participant => participant.Id)];
    }

    // The organisation's active participants. The tests of the collection run one at a time, so no participant is added
    // between this and the call it is compared with.
    private async Task<List<Guid>> StoredActiveIdsAsync(SeededOrganisation organisation)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(organisation.OrganisationId))
        {
            return await db.Participants
                .Where(p => p.Status == ParticipantStatus.Active)
                .Select(p => p.Id)
                .ToListAsync(TestContext.Current.CancellationToken);
        }
    }

    // The ID of the last event written, by any test: the tests of the collection run one at a time, so none writes
    // one while a test reads it before and after its requests.
    private async Task<long?> LastEventIdAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Database
            .SqlQuery<long?>($"SELECT MAX([Id]) AS [Value] FROM [AuditEvent]")
            .SingleAsync(TestContext.Current.CancellationToken);
    }
}
