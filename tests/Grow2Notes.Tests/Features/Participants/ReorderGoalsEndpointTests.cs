using System.Linq.Expressions;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Features.Participants;
using Grow2Notes.Web.Platform;
using Grow2Notes.Web.Platform.Audit;
using Grow2Notes.Web.Platform.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests.Features.Participants;

/// <summary>
/// <c>PUT /api/admin/participants/{participantId}/goals/order</c> (design.md §6.6, S01.02.07), called with the
/// test-only sign-in (<see cref="TestSignIn"/>): a manager sends the IDs of a participant's active goals in a new
/// order, and gets <c>204</c> with the goals rewritten as 0 to n-1 and one <c>goal.reordered</c>; the order already
/// stored answers <c>204</c> and writes nothing (D84); IDs that are not the active goals, each once, and a goal changed
/// after the list was read, <c>422 config.order_mismatch</c>; a body with no IDs, <c>422 validation.failed</c>; and a
/// participant who is archived, <c>422 participant.archived</c> (D86, D93), whatever the IDs; and a participant who
/// is unknown, or another organisation's, <c>404</c>; each refusal changing nothing and writing no event. Each test
/// reorders the goals of a participant of its own in A (tests/README.md). The endpoint matrix covers the statuses of
/// every caller, and <see cref="Platform.Audit.AuditEventsTests"/> the fields of the
/// <c>goal.reordered</c> that it writes.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class ReorderGoalsEndpointTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    : IClassFixture<Grow2NotesFactory>
{
    private static readonly DateTime ArchivedAtUtc = TestGoals.AddedAtUtc.AddDays(1);

    private readonly SeededOrganisations seeded = sqlServer.Seeded;

    [Fact]
    public async Task A_new_order_answers_204_and_rewrites_the_active_goals_as_0_to_n_1_with_one_event()
    {
        // With a gap and a tie, as archiving and two adds at once leave them, which the list reads in CreatedAtUtc
        // order, so that the old order the event records is the list's; and an archived goal, which keeps its place.
        // The tied goals' IDs are the other way round in SQL Server's order, which compares a uniqueidentifier by its
        // last six bytes first, so only the CreatedAtUtc step reads the bus first.
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var bus = Guid.Parse("2f6b9d1e-4a7c-4b3e-8d5f-ffffffffffff");
        var sister = Guid.Parse("8b4d7f2a-1c6e-4d9b-a3f7-000000000001");
        var goals = await factory.Services.AddGoalsAsync(
            seeded.A,
            participant.Id,
            new("Makes own breakfast", 0),
            new("Uses the washing machine", 1, ArchivedAtUtc: ArchivedAtUtc),
            new("Catches the bus to day program", 3, Id: bus),
            new("Phones her sister", 3, TestGoals.AddedAtUtc.AddSeconds(1), sister));
        var (breakfast, washing) = (goals[0], goals[1]);
        var washingBefore = await StoredGoalAsync(washing);

        using var response = await PutAsync(
            Caller.ManagerOfA, participant.Id, new { goalIds = new[] { sister, breakfast, bus } });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Empty(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal([1, 1, 2, 0], await SortOrdersAsync(goals));
        Assert.Equal(washingBefore, await StoredGoalAsync(washing));
        var written = Assert.Single(await EventsAsync(participant.Id));
        Assert.Equal(AuditEventTypes.Goal.Reordered, written.EventType);
        Assert.Equal(AuditEntityTypes.Goal, written.EntityType);
        Assert.Null(written.EntityId);
        var details = Details(old: [breakfast, bus, sister], @new: [sister, breakfast, bus]);
        Assert.True(
            JsonNode.DeepEquals(details, JsonNode.Parse(written.Details ?? "null")),
            $"Details: expected {details?.ToJsonString()}, written {written.Details}.");
    }

    // D84: the list finds nothing to change, even with the gap and the tie, so the save writes no row and no event. The
    // tied goals' IDs are the other way round in SQL Server's order, as in the test above, so only the CreatedAtUtc
    // step reads the order sent as the one stored.
    [Fact]
    public async Task The_order_already_stored_answers_204_and_writes_nothing()
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var bus = Guid.Parse("6a1c4e8b-3d7f-4c2a-9b5e-ffffffffffff");
        var sister = Guid.Parse("d3f5a7c9-2b4e-4f6a-8c1d-000000000001");
        var goals = await factory.Services.AddGoalsAsync(
            seeded.A,
            participant.Id,
            new("Makes own breakfast", 0),
            new("Catches the bus to day program", 2, Id: bus),
            new("Phones her sister", 2, TestGoals.AddedAtUtc.AddSeconds(1), sister));
        var before = await StoredGoalsAsync(participant.Id);

        using var response = await PutAsync(Caller.ManagerOfA, participant.Id, new { goalIds = goals });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(before, await StoredGoalsAsync(participant.Id));
        Assert.Empty(await EventsAsync(participant.Id));
    }

    // ListOrder.Reorder is given the active goals alone, so an archived one is refused as one it was not given, and
    // another participant's goal as an unknown one (S01.02.07's Notes).
    [Theory]
    [InlineData("missing")]
    [InlineData("extra")]
    [InlineData("archived")]
    [InlineData("repeated")]
    public async Task An_order_that_is_not_the_active_goals_each_once_answers_422_and_changes_nothing(string mismatch)
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var goals = await factory.Services.AddGoalsAsync(
            seeded.A,
            participant.Id,
            new("Makes own breakfast", 0),
            new("Catches the bus to day program", 1),
            new("Uses the washing machine", 2, ArchivedAtUtc: ArchivedAtUtc));
        var (breakfast, bus, washing) = (goals[0], goals[1], goals[2]);
        var other = await factory.Services.AddParticipantAsync(seeded.A);
        var othersGoal = Assert.Single(
            await factory.Services.AddGoalsAsync(seeded.A, other.Id, new TestGoal("Phones her sister", 0)));
        Guid[] sent = mismatch switch
        {
            "missing" => [bus],
            "extra" => [bus, breakfast, othersGoal],
            "archived" => [bus, breakfast, washing],
            "repeated" => [bus, breakfast, bus],
            _ => throw new ArgumentOutOfRangeException(nameof(mismatch), mismatch, null),
        };
        var before = await StoredGoalsAsync(participant.Id);
        var othersBefore = await StoredGoalsAsync(other.Id);

        using var response = await PutAsync(Caller.ManagerOfA, participant.Id, new { goalIds = sent });

        await AssertProblemAsync(response, ErrorCode.OrderMismatch);
        Assert.Equal(before, await StoredGoalsAsync(participant.Id));
        Assert.Equal(othersBefore, await StoredGoalsAsync(other.Id));
        Assert.Empty(await EventsAsync(participant.Id));
    }

    // The race that the set of IDs cannot catch, as ConfigurationChangesOnSqlServerTests has it: the first scope's
    // context already holds the goals as they were before the second scope's reword, so the handler's read there gives
    // those rows, which EF Core does not overwrite with the stored ones, and the set of IDs passes as if read before
    // that reword. The save of the moved goal that was reworded then matches no row, so nothing of the reorder is
    // written: the screen reads the list again, as for any mismatch, so it is the same 422, never 412 or 500.
    [Fact]
    public async Task A_goal_another_scope_changed_after_this_one_read_the_list_gives_OrderMismatch_and_writes_nothing()
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var breakfast = await factory.Services.AddGoalAsync(seeded.A, participant.Id, new("Makes own breakfast", 0));
        var others = await factory.Services.AddGoalsAsync(
            seeded.A, participant.Id, new("Catches the bus to day program", 1), new("Phones her sister", 2));
        var (bus, sister) = (others[0], others[1]);
        await using var first = factory.Services.CreateAsyncScope();
        await using var second = factory.Services.CreateAsyncScope();
        using (first.ServiceProvider.GetRequiredService<ITenantContext>().Use(seeded.A.OrganisationId))
        {
            await first.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Goals
                .Where(goal => goal.ParticipantId == participant.Id)
                .ToListAsync(TestContext.Current.CancellationToken);
        }

        using (second.ServiceProvider.GetRequiredService<ITenantContext>().Use(seeded.A.OrganisationId))
        {
            var reworded = await second.ServiceProvider.GetRequiredService<GoalChanges>().UpdateAsync(
                Manager, breakfast.Id, breakfast.ETag, new("Makes own lunch"), TestContext.Current.CancellationToken);
            Assert.NotNull(reworded.ETag);
        }

        var afterSecond = await StoredGoalsAsync(participant.Id);

        GoalChangeResult result;
        using (first.ServiceProvider.GetRequiredService<ITenantContext>().Use(seeded.A.OrganisationId))
        {
            result = await first.ServiceProvider.GetRequiredService<GoalChanges>().ReorderAsync(
                Manager, participant.Id, [bus, breakfast.Id, sister], TestContext.Current.CancellationToken);
        }

        Assert.Same(ErrorCode.OrderMismatch, result.Error);
        Assert.Equal(afterSecond, await StoredGoalsAsync(participant.Id));
        Assert.Equal(
            [AuditEventTypes.Goal.Updated], (await EventsAsync(participant.Id)).Select(written => written.EventType));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"goalIds":null}""")]
    public async Task A_body_with_no_goalIds_or_a_null_one_answers_422_on_goalIds_and_changes_nothing(string body)
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        await factory.Services.AddGoalsAsync(
            seeded.A, participant.Id, new("Makes own breakfast", 0), new("Phones her sister", 1));
        var before = await StoredGoalsAsync(participant.Id);
        using var client = factory.CreateHttpsClient().SignInAs(Caller.ManagerOfA, seeded);
        using var request = new HttpRequestMessage(HttpMethod.Put, OrderPath(participant.Id))
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add(AntiforgeryTokens.Header, await client.FetchTokenAsync());

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        using var problem = JsonDocument.Parse(await response.ReadProblemAsync(HttpStatusCode.UnprocessableEntity));
        Assert.Equal(
            ErrorCode.ValidationFailed.Code, problem.RootElement.GetProperty(ErrorCode.Extension).GetString());
        Assert.Equal("goalIds", Assert.Single(problem.RootElement.GetProperty("errors").EnumerateObject()).Name);
        Assert.Equal(before, await StoredGoalsAsync(participant.Id));
        Assert.Empty(await EventsAsync(participant.Id));
    }

    // The archived participant is looked at before the IDs, so a wrong set gets the same 422 as the right one (D93).
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task An_archived_participant_s_goals_are_refused_and_not_reordered(bool rightIdSet)
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A, archived: true);
        var goals = await factory.Services.AddGoalsAsync(
            seeded.A, participant.Id, new("Makes own breakfast", 0), new("Phones her sister", 1));
        var before = await StoredGoalsAsync(participant.Id);

        Guid[] goalIds = rightIdSet ? [goals[1], goals[0]] : [goals[0]];

        using var response = await PutAsync(Caller.ManagerOfA, participant.Id, new { goalIds });

        await AssertProblemAsync(response, ErrorCode.ParticipantArchived);
        Assert.Equal(before, await StoredGoalsAsync(participant.Id));
        Assert.Empty(await EventsAsync(participant.Id));
    }

    // B's manager, sending the goals' own IDs, as one who had them would: the tenant filter finds no such participant
    // in B, so no goal is read (design.md §9.2).
    [Fact]
    public async Task Another_organisation_s_participant_is_a_404_problem_and_nothing_is_changed()
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var goals = await factory.Services.AddGoalsAsync(
            seeded.A, participant.Id, new("Makes own breakfast", 0), new("Phones her sister", 1));
        var before = await StoredGoalsAsync(participant.Id);

        using var response = await PutAsync(
            Caller.ManagerOfB, participant.Id, new { goalIds = new[] { goals[1], goals[0] } });

        await response.ReadProblemAsync(HttpStatusCode.NotFound);
        Assert.Equal(before, await StoredGoalsAsync(participant.Id));
        Assert.Empty(await EventsAsync(participant.Id));
    }

    [Fact]
    public async Task A_participant_of_no_organisation_is_a_404_problem()
    {
        var unknown = Guid.NewGuid();

        using var response = await PutAsync(Caller.ManagerOfA, unknown, new { goalIds = Array.Empty<Guid>() });

        await response.ReadProblemAsync(HttpStatusCode.NotFound);
        Assert.Empty(await EventsAsync(unknown));
    }

    // The route takes a GUID alone, so another ID matches no endpoint, and gets the unknown API path's 404.
    [Fact]
    public async Task An_ID_that_is_not_a_GUID_is_a_404()
    {
        using var client = factory.CreateHttpsClient().SignInAs(Caller.ManagerOfA, seeded);

        using var response = await client.PutWithTokenAsync(
            "/api/admin/participants/jane-citizen/goals/order",
            await client.FetchTokenAsync(),
            new { goalIds = Array.Empty<Guid>() },
            ifMatch: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // The /api/admin group's policy refuses the worker before the handler, which would otherwise reorder the goals.
    [Fact]
    public async Task A_worker_gets_403_and_nothing_is_changed()
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var goals = await factory.Services.AddGoalsAsync(
            seeded.A, participant.Id, new("Makes own breakfast", 0), new("Phones her sister", 1));
        var before = await StoredGoalsAsync(participant.Id);

        using var response = await PutAsync(
            Caller.WorkerOfA, participant.Id, new { goalIds = new[] { goals[1], goals[0] } });

        await response.ReadProblemAsync(HttpStatusCode.Forbidden);
        Assert.Equal(before, await StoredGoalsAsync(participant.Id));
    }

    /// <summary>A's manager, as the handler is given them in the app.</summary>
    private Actor Manager => new(seeded.A.ManagerId, Channel.App);

    private static async Task AssertProblemAsync(HttpResponseMessage response, ErrorCode code)
    {
        using var problem = JsonDocument.Parse(await response.ReadProblemAsync((HttpStatusCode)code.Status));
        Assert.Equal(code.Code, problem.RootElement.GetProperty(ErrorCode.Extension).GetString());
    }

    // The Details of a goal.reordered, the old and new order of the active goals' IDs (design.md §6.6), as the JSON
    // stored has them, so that each ID is compared as the text written.
    private static JsonNode? Details(Guid[] old, Guid[] @new) => JsonNode.Parse(new JsonObject
    {
        ["old"] = new JsonObject { ["order"] = new JsonArray([.. old.Select(id => JsonValue.Create(id))]) },
        ["new"] = new JsonObject { ["order"] = new JsonArray([.. @new.Select(id => JsonValue.Create(id))]) },
    }.ToJsonString());

    private static string OrderPath(Guid participantId) => $"/api/admin/participants/{participantId}/goals/order";

    // As the SPA reorders: over HTTPS, with a token fetched as the caller, and no If-Match.
    private async Task<HttpResponseMessage> PutAsync(Caller caller, Guid participantId, object body)
    {
        using var client = factory.CreateHttpsClient().SignInAs(caller, seeded);
        return await client.PutWithTokenAsync(
            OrderPath(participantId), await client.FetchTokenAsync(), body, ifMatch: null);
    }

    // The goal as its row holds it in A, its version as its ETag, through the app's context under A's tenant.
    private async Task<(int SortOrder, DateTime? ArchivedAtUtc, string ETag)> StoredGoalAsync(Guid goalId)
    {
        var goal = Assert.Single(await GoalsAsync(goal => goal.Id == goalId));
        return (goal.SortOrder, goal.ArchivedAtUtc, RowVersionToken.ETag(goal));
    }

    // Each of the participant's goals, by ID, with its place, text and version, so that a change to any shows.
    private async Task<List<(Guid Id, int SortOrder, string Text, string ETag)>> StoredGoalsAsync(Guid participantId) =>
    [
        .. (await GoalsAsync(goal => goal.ParticipantId == participantId))
            .OrderBy(goal => goal.Id)
            .Select(goal => (goal.Id, goal.SortOrder, goal.Text, RowVersionToken.ETag(goal))),
    ];

    // The SortOrder of each of the goals, in the order given.
    private async Task<List<int>> SortOrdersAsync(IReadOnlyList<Guid> goalIds)
    {
        var stored = (await GoalsAsync(goal => goalIds.Contains(goal.Id))).ToDictionary(goal => goal.Id);
        return [.. goalIds.Select(id => stored[id].SortOrder)];
    }

    private async Task<List<Goal>> GoalsAsync(Expression<Func<Goal, bool>> predicate)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(seeded.A.OrganisationId))
        {
            return await db.Goals.AsNoTracking().Where(predicate).ToListAsync(TestContext.Current.CancellationToken);
        }
    }

    // The events written about the participant's goals, oldest first, read in SQL, with no tenant needed.
    private async Task<List<StoredEvent>> EventsAsync(Guid participantId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Database.SqlQuery<StoredEvent>($"""
            SELECT [EventType], [EntityType], [EntityId], [Details]
            FROM [AuditEvent]
            WHERE [ParticipantId] = {participantId}
            ORDER BY [Id]
            """).ToListAsync(TestContext.Current.CancellationToken);
    }

    private sealed record StoredEvent(string EventType, string? EntityType, Guid? EntityId, string? Details);
}
