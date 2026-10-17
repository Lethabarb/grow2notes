using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests.Features.Participants;

/// <summary>
/// <c>GET /api/admin/participants?includeArchived=</c> (design.md §6.6), called with the test-only sign-in
/// (<see cref="TestSignIn"/>): a manager gets their own organisation's participants and none of the other's (§5.9
/// item 7), the active ones, or with <c>includeArchived=true</c> the archived ones too, sorted by family name, then
/// given name (A5), each in §6.6's shape, as JSON that is never stored (§6.1), and no call writes an audit event
/// (A28). The tests add their own participants to seeded organisation A, and change no seeded row (tests/README.md).
/// The endpoint matrix covers who may call it, and <see cref="SignedOutTests"/> what a signed-out caller gets.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class ParticipantListEndpointTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    : IClassFixture<Grow2NotesFactory>
{
    private const string Path = "/api/admin/participants";

    // Made up (A35), with milliseconds, which datetime2(3) keeps.
    private static readonly DateTime CreatedAtUtc = new(2026, 10, 2, 3, 4, 5, 678, DateTimeKind.Utc);

    private readonly SeededOrganisations seeded = sqlServer.Seeded;

    [Fact]
    public async Task A_s_manager_gets_A_s_active_participants_and_none_of_B_s_as_JSON_that_is_never_stored()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var added = await AddAsync(seeded.A, ("Jane", "Citizen", ParticipantStatus.Active));
        using var client = ClientFor(Caller.ManagerOfA);

        using var response = await client.GetAsync(Path, cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.False(response.Headers.Contains("ETag"));
        var ids = ParticipantIds(await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken));
        Assert.Equal((await StoredIdsAsync(seeded.A, ParticipantStatus.Active)).Order(), ids.Order());
        Assert.Contains(seeded.A.ParticipantId, ids);
        Assert.Contains(added[0], ids);
        Assert.DoesNotContain(seeded.B.ParticipantId, ids);
    }

    [Fact]
    public async Task B_s_manager_gets_B_s_participants_and_none_of_A_s_even_with_the_archived_ones()
    {
        var addedToA = await AddAsync(
            seeded.A, ("Jane", "Citizen", ParticipantStatus.Active), ("Tom", "Archer", ParticipantStatus.Archived));

        foreach (var path in new[] { Path, $"{Path}?includeArchived=true" })
        {
            var ids = (await ListAsync(Caller.ManagerOfB, path)).Select(Id).ToList();

            Assert.Contains(seeded.B.ParticipantId, ids);
            Assert.DoesNotContain(seeded.A.ParticipantId, ids);
            Assert.DoesNotContain(addedToA[0], ids);
            Assert.DoesNotContain(addedToA[1], ids);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("?includeArchived=false")]
    public async Task Without_includeArchived_true_the_list_holds_the_active_participants_only(string query)
    {
        var added = await AddAsync(
            seeded.A, ("Sam", "Active", ParticipantStatus.Active), ("Sam", "Archived", ParticipantStatus.Archived));

        var ids = (await ListAsync(Caller.ManagerOfA, Path + query)).Select(Id).ToList();

        Assert.Contains(added[0], ids);
        Assert.DoesNotContain(added[1], ids);
        Assert.Equal((await StoredIdsAsync(seeded.A, ParticipantStatus.Active)).Order(), ids.Order());
    }

    [Fact]
    public async Task With_includeArchived_true_the_list_holds_the_archived_participants_too()
    {
        var added = await AddAsync(
            seeded.A, ("Sam", "Active", ParticipantStatus.Active), ("Sam", "Archived", ParticipantStatus.Archived));

        var participants = await ListAsync(Caller.ManagerOfA, $"{Path}?includeArchived=true");

        var byId = participants.ToDictionary(Id);
        Assert.Equal("Active", byId[added[0]].GetProperty("status").GetString());
        Assert.Equal("Archived", byId[added[1]].GetProperty("status").GetString());
        Assert.Equal((await StoredIdsAsync(seeded.A, status: null)).Order(), byId.Keys.Order());
    }

    // By family name, then given name, in SQL Server's collation, which ignores case, so "de Silva" sorts between
    // "Citizen" and "Ng", where an ordinal sort would put it last; and active and archived together. Neither the order
    // they were added in, nor given names first, nor the active ones first gives the same order.
    [Fact]
    public async Task The_list_is_sorted_by_family_name_then_given_name_whatever_the_status()
    {
        var added = await AddAsync(
            seeded.A,
            ("Adam", "Ng", ParticipantStatus.Active),
            ("Zoë", "de Silva", ParticipantStatus.Archived),
            ("Jane", "Citizen", ParticipantStatus.Active),
            ("Bea", "Citizen", ParticipantStatus.Archived));

        var participants = await ListAsync(Caller.ManagerOfA, $"{Path}?includeArchived=true");

        Assert.Equal(
            [added[3], added[2], added[1], added[0]],
            participants.Select(Id).Where(added.Contains));
    }

    // Names as typed, accents and all; the date of birth as a calendar date; the status by its name; and the stored
    // RowVersion as base64.
    [Fact]
    public async Task A_participant_has_section_6_6_s_shape_with_their_stored_values_and_RowVersion()
    {
        var added = await AddAsync(seeded.A, ("Nguyễn", "O'Brien-Smith", ParticipantStatus.Archived));

        var participant = (await ListAsync(Caller.ManagerOfA, $"{Path}?includeArchived=true"))
            .Single(row => Id(row) == added[0]);

        Assert.Equal(
            ["id", "givenName", "familyName", "dateOfBirth", "status", "rowVersion"],
            participant.EnumerateObject().Select(property => property.Name));
        Assert.Equal("Nguyễn", participant.GetProperty("givenName").GetString());
        Assert.Equal("O'Brien-Smith", participant.GetProperty("familyName").GetString());
        Assert.Equal("1987-03-07", participant.GetProperty("dateOfBirth").GetString());
        Assert.Equal("Archived", participant.GetProperty("status").GetString());
        Assert.Equal(
            Convert.ToBase64String(await StoredRowVersionAsync(seeded.A, added[0])),
            participant.GetProperty("rowVersion").GetString());
    }

    // SQL Server gives the row a new RowVersion on every update, so the If-Match that the old one makes is stale.
    [Fact]
    public async Task Once_a_participant_changes_the_list_gives_their_new_RowVersion()
    {
        var added = await AddAsync(seeded.A, ("Jane", "Citizen", ParticipantStatus.Active));
        var before = await RowVersionInTheListAsync(added[0]);

        await RenameAsync(seeded.A, added[0], "Janet");

        var after = await RowVersionInTheListAsync(added[0]);
        Assert.Equal(Convert.ToBase64String(await StoredRowVersionAsync(seeded.A, added[0])), after);
        Assert.NotEqual(before, after);
    }

    // design.md §5.4 lists no event for reading, whether the list is given or refused (A28).
    [Fact]
    public async Task The_calls_write_no_audit_event()
    {
        var lastEventId = await LastEventIdAsync();

        foreach (var (caller, path, status) in new[]
        {
            (Caller.ManagerOfA, Path, HttpStatusCode.OK),
            (Caller.ManagerOfA, $"{Path}?includeArchived=true", HttpStatusCode.OK),
            (Caller.ManagerOfB, Path, HttpStatusCode.OK),
            (Caller.WorkerOfA, Path, HttpStatusCode.Forbidden),
        })
        {
            using var client = ClientFor(caller);
            using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

            Assert.Equal(status, response.StatusCode);
        }

        Assert.Equal(lastEventId, await LastEventIdAsync());
    }

    private static Guid Id(JsonElement participant) => participant.GetProperty("id").GetGuid();

    private static List<Guid> ParticipantIds(JsonElement participants) => [.. participants.EnumerateArray().Select(Id)];

    // Over HTTPS, as the SPA calls the /api group (tests/README.md).
    private HttpClient ClientFor(Caller caller) => factory.CreateHttpsClient().SignInAs(caller, seeded);

    // The answer's participants, in its order.
    private async Task<List<JsonElement>> ListAsync(Caller caller, string path)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = ClientFor(caller);

        using var response = await client.GetAsync(path, cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var participants = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return [.. participants.EnumerateArray()];
    }

    private async Task<string?> RowVersionInTheListAsync(Guid participantId) =>
        (await ListAsync(Caller.ManagerOfA, Path)).Single(row => Id(row) == participantId)
            .GetProperty("rowVersion").GetString();

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

    private async Task RenameAsync(SeededOrganisation organisation, Guid participantId, string givenName)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(organisation.OrganisationId))
        {
            var participant = await db.Participants.SingleAsync(p => p.Id == participantId, cancellationToken);
            participant.GivenName = givenName;
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    // As the row holds it, read in a scope of its own, as a request reads it.
    private async Task<byte[]> StoredRowVersionAsync(SeededOrganisation organisation, Guid participantId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(organisation.OrganisationId))
        {
            return await db.Participants
                .Where(p => p.Id == participantId)
                .Select(p => p.RowVersion)
                .SingleAsync(TestContext.Current.CancellationToken);
        }
    }

    // The organisation's participants of one status, or of both for null. The tests of the collection run one at a
    // time, so no participant is added between this and the call it is compared with.
    private async Task<List<Guid>> StoredIdsAsync(SeededOrganisation organisation, ParticipantStatus? status)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(organisation.OrganisationId))
        {
            return await db.Participants
                .Where(p => status == null || p.Status == status)
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
