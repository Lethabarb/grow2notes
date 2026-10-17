using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Grow2Notes.Web.Platform.Audit;
using Grow2Notes.Web.Platform.Configuration;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Grow2Notes.Tests.Features.Participants;

/// <summary>
/// <c>POST /api/admin/goals/{goalId}/archive</c> and <c>/restore</c> (design.md §3.7, §6.6, S01.02.08), called with the
/// test-only sign-in (<see cref="TestSignIn"/>) on a fake clock: a manager archives a goal of their own organisation,
/// which keeps its <c>SortOrder</c>, or restores one, which goes to the end of its participant's active goals, with
/// the goal's <c>ETag</c> as <c>If-Match</c>, and gets <c>204</c>; with no <c>If-Match</c>, <c>428</c>; with a stale
/// one, <c>412</c>; and for a goal that is unknown, or another organisation's, <c>404</c>; each of those changing
/// nothing and writing no event. Archiving an archived goal, or restoring an active one, answers <c>204</c> too, and
/// saves nothing (D84). Each test acts on a goal that it adds to a participant of its own in A (tests/README.md). The
/// endpoint matrix covers the statuses of every caller, and <see cref="Platform.Audit.AuditEventsTests"/> the events
/// that they write.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class ArchiveGoalEndpointsTests : IClassFixture<Grow2NotesFactory>, IAsyncDisposable
{
    private const string Archive = "archive";
    private const string Restore = "restore";

    private readonly WebApplicationFactory<Program> app;

    private readonly SeededOrganisations seeded;

    public ArchiveGoalEndpointsTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    {
        app = factory.WithWebHostBuilder(builder => builder.UseClock(new FakeTimeProvider(TestClock.Start)));
        seeded = sqlServer.Seeded;
    }

    // Each action, on a goal it changes: an active one to archive, an archived one to restore.
    public static TheoryData<string> Actions { get; } = new(Archive, Restore);

    [Fact]
    public async Task Archive_answers_204_and_archives_it_at_the_clocks_time_in_its_place_with_a_new_version()
    {
        var (participantId, goal) = await AddGoalAsync(new("Makes own breakfast", 2));

        using var response = await PostAsync(Caller.ManagerOfA, goal.Id, Archive, goal.ETag);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        var stored = await StoredAsync(goal.Id);
        Assert.Equal(
            ("Makes own breakfast", 2, (DateTime?)TestClock.Start.UtcDateTime, participantId),
            (stored.Text, stored.SortOrder, stored.ArchivedAtUtc, stored.ParticipantId));
        Assert.NotEqual(goal.ETag, RowVersionToken.ETag(stored));
        Assert.Equal([AuditEventTypes.Goal.Archived], await EventTypesAsync(goal.Id));
    }

    // A2: one more than the highest active SortOrder, past the gap that an archive left, and whatever SortOrder the
    // archived goals hold, its own included, as only a reorder closes gaps.
    [Fact]
    public async Task Restore_answers_204_and_puts_it_one_after_the_highest_active_SortOrder_with_a_new_version()
    {
        var participant = await app.Services.AddParticipantAsync(seeded.A);
        await app.Services.AddGoalsAsync(
            seeded.A,
            participant.Id,
            new("Phones her sister", 0),
            new("Catches the bus", 3),
            new("Walks to the shop", 7, ArchivedAtUtc: TestGoals.AddedAtUtc));
        var goal = await app.Services.AddGoalAsync(
            seeded.A, participant.Id, new("Makes own breakfast", 1, ArchivedAtUtc: TestGoals.AddedAtUtc));

        using var response = await PostAsync(Caller.ManagerOfA, goal.Id, Restore, goal.ETag);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        var stored = await StoredAsync(goal.Id);
        Assert.Equal(
            ("Makes own breakfast", 4, (DateTime?)null), (stored.Text, stored.SortOrder, stored.ArchivedAtUtc));
        Assert.NotEqual(goal.ETag, RowVersionToken.ETag(stored));
        Assert.Equal([AuditEventTypes.Goal.Restored], await EventTypesAsync(goal.Id));
    }

    [Fact]
    public async Task A_goal_restored_to_a_participant_with_no_active_goal_is_first()
    {
        var (_, goal) = await AddGoalAsync(new("Makes own breakfast", 5, ArchivedAtUtc: TestGoals.AddedAtUtc));

        using var response = await PostAsync(Caller.ManagerOfA, goal.Id, Restore, goal.ETag);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(0, (await StoredAsync(goal.Id)).SortOrder);
    }

    // D84: a repeat finds nothing to change, so an archived goal keeps the time it was archived at, and an active one
    // its place.
    [Theory]
    [InlineData(Archive, true)]
    [InlineData(Restore, false)]
    public async Task A_goal_already_so_answers_204_and_nothing_is_saved(string action, bool archived)
    {
        var (_, goal) = await AddGoalAsync(ToChange(archived));

        using var response = await PostAsync(Caller.ManagerOfA, goal.Id, action, goal.ETag);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await AssertUnchangedAsync(goal, archived);
    }

    [Theory]
    [MemberData(nameof(Actions))]
    public async Task With_no_If_Match_it_answers_428_and_changes_nothing(string action)
    {
        var (_, goal) = await AddGoalAsync(ToChange(archived: action == Restore));

        using var response = await PostAsync(Caller.ManagerOfA, goal.Id, action, ifMatch: null);

        await AssertProblemAsync(response, ErrorCode.PreconditionRequired);
        await AssertUnchangedAsync(goal, archived: action == Restore);
    }

    // As when another manager reworded the goal first: the action, sent with the version both loaded, changes nothing.
    [Theory]
    [MemberData(nameof(Actions))]
    public async Task With_a_stale_If_Match_it_answers_412_and_writes_nothing(string action)
    {
        var archived = action == Restore;
        var (_, goal) = await AddGoalAsync(ToChange(archived));
        using (var client = app.CreateHttpsClient().SignInAs(Caller.ManagerOfA, seeded))
        using (var reword = await client.PutWithTokenAsync(
                   $"/api/admin/goals/{goal.Id}", await client.FetchTokenAsync(), new { text = "Makes own lunch" },
                   goal.ETag))
        {
            Assert.Equal(HttpStatusCode.OK, reword.StatusCode);
        }

        var reworded = RowVersionToken.ETag(await StoredAsync(goal.Id));

        using var response = await PostAsync(Caller.ManagerOfA, goal.Id, action, goal.ETag);

        await AssertProblemAsync(response, ErrorCode.PreconditionFailed);
        var stored = await StoredAsync(goal.Id);
        Assert.Equal(
            (2, archived ? TestGoals.AddedAtUtc : (DateTime?)null, reworded),
            (stored.SortOrder, stored.ArchivedAtUtc, RowVersionToken.ETag(stored)));
        Assert.Equal([AuditEventTypes.Goal.Updated], await EventTypesAsync(goal.Id));
    }

    // B's manager, sending the goal's current ETag, as one who had it would: the tenant filter finds no such goal in B,
    // so the version is never checked (design.md §9.2).
    [Theory]
    [MemberData(nameof(Actions))]
    public async Task Another_organisation_s_goal_is_not_found_and_not_changed(string action)
    {
        var (_, goal) = await AddGoalAsync(ToChange(archived: action == Restore));

        using var response = await PostAsync(Caller.ManagerOfB, goal.Id, action, goal.ETag);

        await response.ReadProblemAsync(HttpStatusCode.NotFound);
        await AssertUnchangedAsync(goal, archived: action == Restore);
    }

    [Theory]
    [MemberData(nameof(Actions))]
    public async Task An_ID_that_no_goal_has_is_not_found(string action)
    {
        using var response = await PostAsync(Caller.ManagerOfA, Guid.NewGuid(), action, IfMatch.ETag("AAAAAAAAB9E="));

        await response.ReadProblemAsync(HttpStatusCode.NotFound);
    }

    // The routes take a GUID alone, so another ID matches no endpoint, and gets the unknown API path's 404.
    [Theory]
    [MemberData(nameof(Actions))]
    public async Task An_ID_that_is_not_a_GUID_is_not_found(string action)
    {
        using var response = await PostAsync(
            Caller.ManagerOfA, $"/api/admin/goals/makes-own-breakfast/{action}", IfMatch.ETag("AAAAAAAAB9E="));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // The screen reads the goals with the archived ones, in the list's order, and sends the version that the list gives
    // each. An archived goal keeps its place in that order, so the screen shows the archived ones apart.
    [Fact]
    public async Task After_Archive_it_is_listed_as_archived_in_its_place_and_after_Restore_last()
    {
        var participant = await app.Services.AddParticipantAsync(seeded.A);
        var ids = await app.Services.AddGoalsAsync(
            seeded.A, participant.Id, new("Makes own breakfast", 0), new("Phones her sister", 1));
        var (breakfast, sister) = (ids[0], ids[1]);

        var listed = await ListAsync(participant.Id);
        using (var archived = await PostAsync(Caller.ManagerOfA, breakfast, Archive, VersionOf(listed, breakfast)))
        {
            Assert.Equal(HttpStatusCode.NoContent, archived.StatusCode);
        }

        listed = await ListAsync(participant.Id);
        Assert.Equal([(breakfast, "2026-10-09T13:30:00Z"), (sister, null)], Places(listed));

        using (var restored = await PostAsync(Caller.ManagerOfA, breakfast, Restore, VersionOf(listed, breakfast)))
        {
            Assert.Equal(HttpStatusCode.NoContent, restored.StatusCode);
        }

        Assert.Equal([(sister, null), (breakfast, null)], Places(await ListAsync(participant.Id)));
    }

    public ValueTask DisposeAsync() => app.DisposeAsync();

    private static async Task AssertProblemAsync(HttpResponseMessage response, ErrorCode code)
    {
        using var problem = JsonDocument.Parse(await response.ReadProblemAsync((HttpStatusCode)code.Status));
        Assert.Equal(code.Code, problem.RootElement.GetProperty(ErrorCode.Extension).GetString());
    }

    // Each listed goal's ID and the time it was archived at, as the list gives it, in the list's order.
    private static List<(Guid Id, string? ArchivedAtUtc)> Places(List<JsonElement> listed) =>
        [.. listed.Select(goal => (goal.GetProperty("id").GetGuid(), goal.GetProperty("archivedAtUtc").GetString()))];

    // The If-Match of the listed goal of ID `goalId`, from the version that the list gives it.
    private static string VersionOf(List<JsonElement> listed, Guid goalId) =>
        IfMatch.ETag(listed.Single(goal => goal.GetProperty("id").GetGuid() == goalId)
            .GetProperty("rowVersion").GetString()!);

    // A goal that the action changes: an active one to archive, an archived one to restore, each in a place of its own.
    private static TestGoal ToChange(bool archived) =>
        archived
            ? new("Makes own breakfast", 2, ArchivedAtUtc: TestGoals.AddedAtUtc)
            : new("Makes own breakfast", 2);

    // The goal as AddGoalAsync left it, in its place and at the same version, with no event written about it.
    private async Task AssertUnchangedAsync(AddedGoal goal, bool archived)
    {
        var stored = await StoredAsync(goal.Id);
        Assert.Equal(
            (2, archived ? TestGoals.AddedAtUtc : (DateTime?)null, goal.ETag),
            (stored.SortOrder, stored.ArchivedAtUtc, RowVersionToken.ETag(stored)));
        Assert.Empty(await EventTypesAsync(goal.Id));
    }

    // A goal, as given, of a participant added to A for the test.
    private async Task<(Guid ParticipantId, AddedGoal Goal)> AddGoalAsync(TestGoal goal)
    {
        var participant = await app.Services.AddParticipantAsync(seeded.A);
        return (participant.Id, await app.Services.AddGoalAsync(seeded.A, participant.Id, goal));
    }

    private Task<HttpResponseMessage> PostAsync(Caller caller, Guid goalId, string action, string? ifMatch) =>
        PostAsync(caller, $"/api/admin/goals/{goalId}/{action}", ifMatch);

    // As the SPA archives or restores one: over HTTPS, with a token fetched as the caller, and no body.
    private async Task<HttpResponseMessage> PostAsync(Caller caller, string path, string? ifMatch)
    {
        using var client = app.CreateHttpsClient().SignInAs(caller, seeded);
        return await client.PostWithTokenAsync(path, await client.FetchTokenAsync(), ifMatch: ifMatch);
    }

    // A's manager's list of the participant's goals, archived ones included, as the screen reads it.
    private async Task<List<JsonElement>> ListAsync(Guid participantId)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = app.CreateHttpsClient().SignInAs(Caller.ManagerOfA, seeded);
        using var response = await client.GetAsync(
            $"/api/admin/participants/{participantId}/goals?includeArchived=true", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return [.. (await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).EnumerateArray()];
    }

    // The goal as its row holds it in A, through the app's context under A's tenant.
    private async Task<Goal> StoredAsync(Guid goalId)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(seeded.A.OrganisationId))
        {
            return await db.Goals.AsNoTracking()
                .SingleAsync(g => g.Id == goalId, TestContext.Current.CancellationToken);
        }
    }

    // The types of the events written about the goal, oldest first, read in SQL, with no tenant needed.
    private async Task<List<string>> EventTypesAsync(Guid goalId)
    {
        await using var scope = app.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Database
            .SqlQuery<string>(
                $"SELECT [EventType] AS [Value] FROM [AuditEvent] WHERE [EntityId] = {goalId} ORDER BY [Id]")
            .ToListAsync(TestContext.Current.CancellationToken);
    }
}
