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
/// <c>GET /api/admin/participants/{participantId}/goals?includeArchived=</c> (design.md §6.6), called with the
/// test-only sign-in (<see cref="TestSignIn"/>): a manager gets a participant's active goals, or with
/// <c>includeArchived=true</c> the archived ones too, in <c>SortOrder</c>, then <c>CreatedAtUtc</c>, then <c>Id</c>
/// order, each in §6.6's shape with its stored <c>RowVersion</c>, as JSON that is never stored (§6.1); an unknown
/// participant, or another organisation's, is <c>404</c> (§9.2); and no call writes an audit event (A28). The tests
/// add their own participants and goals to seeded organisation A, and change no seeded row (tests/README.md). The
/// endpoint matrix covers who may call it, and <see cref="SignedOutTests"/> what a signed-out caller gets.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class GoalListEndpointTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    : IClassFixture<Grow2NotesFactory>
{
    // Made up (A35), with milliseconds, which datetime2(3) keeps.
    private static readonly DateTime AddedAtUtc = TestGoals.AddedAtUtc;

    private readonly SeededOrganisations seeded = sqlServer.Seeded;

    [Fact]
    public async Task A_manager_gets_the_participant_s_active_goals_as_JSON_that_is_never_stored()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var added = await AddGoalsAsync(participant.Id, new("Makes own breakfast", 0), new("Phones her sister", 1));
        using var client = ClientFor(Caller.ManagerOfA);

        using var response = await client.GetAsync(PathOf(participant.Id), cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.False(response.Headers.Contains("ETag"));
        var goals = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        Assert.Equal(added, goals.EnumerateArray().Select(Id));
    }

    // SortOrder first, whatever the order they were added in; then, for two of one SortOrder, as two adds at once can
    // leave them, CreatedAtUtc; then Id, in SQL Server's order, which compares a uniqueidentifier by its last six bytes
    // first, so ...0001 comes before ...0002 there, where .NET's Guid order, and the order added, have them the other
    // way round.
    [Fact]
    public async Task The_goals_are_in_SortOrder_then_CreatedAtUtc_then_Id_order()
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var tiedSecond = Guid.Parse("1a2b3c4d-5e6f-4a7b-8c9d-a1b2c3000002");
        var tiedFirst = Guid.Parse("9f1c2b3a-4d5e-4f60-8a71-a1b2c3000001");
        var added = await AddGoalsAsync(
            participant.Id,
            new("Walks to the shop", 2),
            new("Phones her sister", 1, AddedAtUtc.AddSeconds(1), tiedSecond),
            new("Catches the bus", 1, AddedAtUtc.AddSeconds(1), tiedFirst),
            new("Makes own breakfast", 1, AddedAtUtc),
            new("Waters the garden", 0, AddedAtUtc.AddMinutes(5)));

        var goals = await ListAsync(Caller.ManagerOfA, PathOf(participant.Id));

        Assert.Equal([added[4], added[3], tiedFirst, tiedSecond, added[0]], goals.Select(Id));
    }

    [Theory]
    [InlineData("")]
    [InlineData("?includeArchived=false")]
    public async Task Without_includeArchived_true_the_list_holds_the_active_goals_only(string query)
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var added = await AddGoalsAsync(
            participant.Id, new("Makes own breakfast", 0), new("Walks to the shop", 1, ArchivedAtUtc: AddedAtUtc));

        var goals = await ListAsync(Caller.ManagerOfA, PathOf(participant.Id) + query);

        Assert.Equal([added[0]], goals.Select(Id));
    }

    // An archived goal keeps its SortOrder (Archiving.Archive), so it takes its place among the active ones here.
    [Fact]
    public async Task With_includeArchived_true_the_list_holds_the_archived_goals_too_in_the_same_order()
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var added = await AddGoalsAsync(
            participant.Id,
            new("Makes own breakfast", 0),
            new("Walks to the shop", 1, ArchivedAtUtc: AddedAtUtc),
            new("Phones her sister", 2));

        var goals = await ListAsync(Caller.ManagerOfA, $"{PathOf(participant.Id)}?includeArchived=true");

        Assert.Equal(added, goals.Select(Id));
    }

    // An archived participant's goals are listed as an active one's are: only the screen is read-only (D86).
    [Fact]
    public async Task An_archived_participant_s_goals_are_listed()
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A, archived: true);
        var added = await AddGoalsAsync(participant.Id, new TestGoal("Makes own breakfast", 0));

        var goals = await ListAsync(Caller.ManagerOfA, $"{PathOf(participant.Id)}?includeArchived=true");

        Assert.Equal(added, goals.Select(Id));
    }

    // The text as stored, accents and all; the time it was archived at in UTC, to the millisecond, or null; and the
    // stored RowVersion as base64, which If-Match sends back.
    [Fact]
    public async Task A_goal_has_section_6_6_s_shape_with_its_stored_values_and_RowVersion()
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var added = await AddGoalsAsync(
            participant.Id,
            new("Cooks phở with Nguyễn, then tidies up", 3),
            new("Walks to the shop", 4, ArchivedAtUtc: AddedAtUtc));

        var goals = await ListAsync(Caller.ManagerOfA, $"{PathOf(participant.Id)}?includeArchived=true");

        var active = goals[0];
        Assert.Equal(
            ["id", "text", "sortOrder", "archivedAtUtc", "rowVersion"],
            active.EnumerateObject().Select(property => property.Name));
        Assert.Equal("Cooks phở with Nguyễn, then tidies up", active.GetProperty("text").GetString());
        Assert.Equal(3, active.GetProperty("sortOrder").GetInt32());
        Assert.Equal(JsonValueKind.Null, active.GetProperty("archivedAtUtc").ValueKind);
        Assert.Equal("2026-10-02T03:04:05.678Z", goals[1].GetProperty("archivedAtUtc").GetString());
        foreach (var (goal, id) in goals.Zip(added))
        {
            Assert.Equal(
                Convert.ToBase64String(await StoredRowVersionAsync(id)), goal.GetProperty("rowVersion").GetString());
        }
    }

    // SQL Server gives the row a new RowVersion on every update, so the If-Match that the old one makes is stale.
    [Fact]
    public async Task Once_a_goal_changes_the_list_gives_its_new_RowVersion()
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var added = await AddGoalsAsync(participant.Id, new TestGoal("Makes own breakfast", 0));
        var before = (await ListAsync(Caller.ManagerOfA, PathOf(participant.Id)))[0].GetProperty("rowVersion");

        await RewordAsync(added[0], "Makes own breakfast and lunch");

        var after = (await ListAsync(Caller.ManagerOfA, PathOf(participant.Id)))[0].GetProperty("rowVersion");
        Assert.Equal(Convert.ToBase64String(await StoredRowVersionAsync(added[0])), after.GetString());
        Assert.NotEqual(before.GetString(), after.GetString());
    }

    [Fact]
    public async Task A_participant_of_no_organisation_is_a_404_problem()
    {
        using var client = ClientFor(Caller.ManagerOfA);

        using var response = await client.GetAsync(
            PathOf(Guid.NewGuid()), TestContext.Current.CancellationToken);

        await response.ReadProblemAsync(HttpStatusCode.NotFound);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
    }

    // B's manager does not find A's participant, nor A's B's, so neither gets the other's goals (§9.2, §5.9 item 7).
    [Fact]
    public async Task Another_organisation_s_participant_is_a_404_problem_with_or_without_the_archived_goals()
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        await AddGoalsAsync(participant.Id, new TestGoal("Makes own breakfast", 0));

        foreach (var (caller, participantId) in new[]
        {
            (Caller.ManagerOfB, participant.Id),
            (Caller.ManagerOfB, seeded.A.ParticipantId),
            (Caller.ManagerOfA, seeded.B.ParticipantId),
        })
        {
            foreach (var query in new[] { "", "?includeArchived=true" })
            {
                using var client = ClientFor(caller);
                using var response = await client.GetAsync(
                    PathOf(participantId) + query, TestContext.Current.CancellationToken);

                await response.ReadProblemAsync(HttpStatusCode.NotFound);
            }
        }
    }

    // The route takes a GUID alone, so another ID matches no endpoint, and gets the unknown API path's 404.
    [Fact]
    public async Task An_ID_that_is_not_a_GUID_is_a_404()
    {
        using var client = ClientFor(Caller.ManagerOfA);

        using var response = await client.GetAsync(
            "/api/admin/participants/jane-citizen/goals", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // design.md §5.4 lists no event for reading, whether the list is given or refused (A28).
    [Fact]
    public async Task The_calls_write_no_audit_event()
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        await AddGoalsAsync(participant.Id, new TestGoal("Makes own breakfast", 0));
        var lastEventId = await LastEventIdAsync();

        foreach (var (caller, path, status) in new[]
        {
            (Caller.ManagerOfA, PathOf(participant.Id), HttpStatusCode.OK),
            (Caller.ManagerOfA, $"{PathOf(participant.Id)}?includeArchived=true", HttpStatusCode.OK),
            (Caller.ManagerOfB, PathOf(participant.Id), HttpStatusCode.NotFound),
            (Caller.ManagerOfA, PathOf(Guid.NewGuid()), HttpStatusCode.NotFound),
            (Caller.WorkerOfA, PathOf(participant.Id), HttpStatusCode.Forbidden),
        })
        {
            using var client = ClientFor(caller);
            using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

            Assert.Equal(status, response.StatusCode);
        }

        Assert.Equal(lastEventId, await LastEventIdAsync());
    }

    private static string PathOf(Guid participantId) => $"/api/admin/participants/{participantId}/goals";

    private static Guid Id(JsonElement goal) => goal.GetProperty("id").GetGuid();

    // Over HTTPS, as the SPA calls the /api group (tests/README.md).
    private HttpClient ClientFor(Caller caller) => factory.CreateHttpsClient().SignInAs(caller, seeded);

    // The answer's goals, in its order.
    private async Task<List<JsonElement>> ListAsync(Caller caller, string path)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = ClientFor(caller);

        using var response = await client.GetAsync(path, cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var goals = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return [.. goals.EnumerateArray()];
    }

    // Adds the goals to the participant, who is in organisation A, and gives their IDs in the order given.
    private Task<List<Guid>> AddGoalsAsync(Guid participantId, params TestGoal[] goals) =>
        factory.Services.AddGoalsAsync(seeded.A, participantId, goals);

    private async Task RewordAsync(Guid goalId, string text)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(seeded.A.OrganisationId))
        {
            var goal = await db.Goals.SingleAsync(g => g.Id == goalId, cancellationToken);
            goal.Text = text;
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    // As the row holds it, read in a scope of its own, as a request reads it.
    private async Task<byte[]> StoredRowVersionAsync(Guid goalId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(seeded.A.OrganisationId))
        {
            return await db.Goals
                .Where(g => g.Id == goalId)
                .Select(g => g.RowVersion)
                .SingleAsync(TestContext.Current.CancellationToken);
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
