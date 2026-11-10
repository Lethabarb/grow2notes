using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Grow2Notes.Web.Platform.Audit;
using Grow2Notes.Web.Platform.Configuration;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Grow2Notes.Tests.Features.Participants;

/// <summary>
/// A goal's term on <c>POST /api/admin/participants/{participantId}/goals</c> and <c>PUT /api/admin/goals/{goalId}</c>
/// (design.md §6.6, D104, A150), called with the test-only sign-in (<see cref="TestSignIn"/>): a goal is added Short
/// term unless the body says <c>isMediumOrLongTerm</c>; a reword may set the term, a request that changes the term
/// alone writes <c>goal.updated</c> with its old and new value, a request that leaves the term out keeps the stored
/// one, and one that sends the stored text and term writes nothing and answers the same <c>ETag</c> (D84); the
/// <c>If-Match</c> rules and the archived participant's refusal before them (D93) are the reword's; and the admin list
/// gives the term.
/// Each test adds a participant of its own in A (tests/README.md). <see cref="AddGoalEndpointTests"/> and
/// <see cref="UpdateGoalEndpointTests"/> cover the text.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class GoalTermEndpointTests : IClassFixture<Grow2NotesFactory>, IAsyncDisposable
{
    private const string StoredText = "Makes own breakfast";

    private readonly WebApplicationFactory<Program> app;

    private readonly SeededOrganisations seeded;

    public GoalTermEndpointTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    {
        app = factory.WithWebHostBuilder(builder => builder.UseClock(new FakeTimeProvider(TestClock.Start)));
        seeded = sqlServer.Seeded;
    }

    [Fact]
    public async Task A_goal_added_with_no_term_is_Short_term_and_the_event_records_it()
    {
        var participant = await app.Services.AddParticipantAsync(seeded.A);

        using var response = await PostAsync(participant.Id, new { text = StoredText });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var goal = Assert.Single(await StoredForAsync(participant.Id));
        Assert.False(goal.IsMediumOrLongTerm);
        var details = Assert.Single(await DetailsAsync(goal.Id));
        Assert.False(details["new"]!["isMediumOrLongTerm"]!.GetValue<bool>());
    }

    [Fact]
    public async Task A_goal_added_as_Medium_or_long_term_is_stored_so_and_the_event_records_it()
    {
        var participant = await app.Services.AddParticipantAsync(seeded.A);

        using var response = await PostAsync(participant.Id, new { text = StoredText, isMediumOrLongTerm = true });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var goal = Assert.Single(await StoredForAsync(participant.Id));
        Assert.True(goal.IsMediumOrLongTerm);
        var details = Assert.Single(await DetailsAsync(goal.Id));
        Assert.True(details["new"]!["isMediumOrLongTerm"]!.GetValue<bool>());
    }

    [Fact]
    public async Task A_request_that_changes_the_term_alone_writes_goal_updated_with_the_old_and_new_term()
    {
        var (_, goal) = await AddGoalAsync(new(StoredText, 0));

        using var response = await PutAsync(goal.Id, new { text = StoredText, isMediumOrLongTerm = true }, goal.ETag);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var stored = await StoredAsync(goal.Id);
        Assert.True(stored.IsMediumOrLongTerm);
        Assert.Equal(RowVersionToken.ETag(stored), response.Headers.ETag?.ToString());
        Assert.NotEqual(goal.ETag, response.Headers.ETag?.ToString());
        var details = Assert.Single(await DetailsAsync(goal.Id));
        Assert.True(JsonNode.DeepEquals(new JsonObject { ["isMediumOrLongTerm"] = false }, details["old"]));
        Assert.True(JsonNode.DeepEquals(new JsonObject { ["isMediumOrLongTerm"] = true }, details["new"]));
    }

    // A reword that sends no term keeps the stored one, as a stale page or a tool that knows nothing of terms would.
    [Fact]
    public async Task A_request_that_leaves_the_term_out_keeps_the_stored_term()
    {
        var (_, goal) = await AddGoalAsync(new(StoredText, 0, IsMediumOrLongTerm: true));

        using var response = await PutAsync(goal.Id, new { text = "Makes own lunch" }, goal.ETag);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var stored = await StoredAsync(goal.Id);
        Assert.Equal(("Makes own lunch", true), (stored.Text, stored.IsMediumOrLongTerm));
        var details = Assert.Single(await DetailsAsync(goal.Id));
        Assert.True(JsonNode.DeepEquals(new JsonObject { ["text"] = "Makes own lunch" }, details["new"]));
    }

    // D84: the same text and term saves nothing and writes no event, and the ETag stays the one the If-Match matched.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task The_stored_text_and_term_sent_again_answer_200_with_the_same_ETag_and_write_nothing(
        bool isMediumOrLongTerm)
    {
        var (_, goal) = await AddGoalAsync(new(StoredText, 0, IsMediumOrLongTerm: isMediumOrLongTerm));

        using var response = await PutAsync(goal.Id, new { text = StoredText, isMediumOrLongTerm }, goal.ETag);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(goal.ETag, response.Headers.ETag?.ToString());
        Assert.Empty(await DetailsAsync(goal.Id));
    }

    [Fact]
    public async Task With_no_If_Match_a_term_change_answers_428_and_changes_nothing()
    {
        var (_, goal) = await AddGoalAsync(new(StoredText, 0));

        using var response = await PutAsync(goal.Id, new { text = StoredText, isMediumOrLongTerm = true }, null);

        await AssertProblemAsync(response, ErrorCode.PreconditionRequired);
        Assert.False((await StoredAsync(goal.Id)).IsMediumOrLongTerm);
        Assert.Empty(await DetailsAsync(goal.Id));
    }

    [Fact]
    public async Task With_a_stale_If_Match_a_term_change_answers_412_and_changes_nothing()
    {
        var (_, goal) = await AddGoalAsync(new(StoredText, 0));
        using (var first = await PutAsync(goal.Id, new { text = "Makes own lunch" }, goal.ETag))
        {
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        }

        using var response = await PutAsync(goal.Id, new { text = StoredText, isMediumOrLongTerm = true }, goal.ETag);

        await AssertProblemAsync(response, ErrorCode.PreconditionFailed);
        var stored = await StoredAsync(goal.Id);
        Assert.Equal(("Makes own lunch", false), (stored.Text, stored.IsMediumOrLongTerm));
    }

    // D93: the participant is archived, so the change is refused as such, whatever the If-Match says or lacks.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_term_change_to_an_archived_participant_s_goal_answers_422_before_the_If_Match(bool sendsETag)
    {
        var (_, goal) = await AddGoalAsync(new(StoredText, 0), archivedParticipant: true);

        using var response = await PutAsync(
            goal.Id, new { text = StoredText, isMediumOrLongTerm = true }, sendsETag ? goal.ETag : null);

        await AssertProblemAsync(response, ErrorCode.ParticipantArchived);
        Assert.False((await StoredAsync(goal.Id)).IsMediumOrLongTerm);
        Assert.Empty(await DetailsAsync(goal.Id));
    }

    [Fact]
    public async Task A_goal_is_not_added_to_an_archived_participant_with_a_term_either()
    {
        var participant = await app.Services.AddParticipantAsync(seeded.A, archived: true);

        using var response = await PostAsync(participant.Id, new { text = StoredText, isMediumOrLongTerm = true });

        await AssertProblemAsync(response, ErrorCode.ParticipantArchived);
        Assert.Empty(await StoredForAsync(participant.Id));
    }

    [Fact]
    public async Task The_admin_list_gives_each_goal_s_term()
    {
        var participant = await app.Services.AddParticipantAsync(seeded.A);
        await app.Services.AddGoalsAsync(
            seeded.A,
            participant.Id,
            new TestGoal("Short one", 0),
            new TestGoal("Long one", 1, IsMediumOrLongTerm: true));
        using var client = app.CreateHttpsClient().SignInAs(Caller.ManagerOfA, seeded);

        using var response = await client.GetAsync(
            $"/api/admin/participants/{participant.Id}/goals", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var goals = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(
            [("Short one", false), ("Long one", true)],
            goals.EnumerateArray().Select(goal =>
                (goal.GetProperty("text").GetString(), goal.GetProperty("isMediumOrLongTerm").GetBoolean())));
    }

    public ValueTask DisposeAsync() => app.DisposeAsync();

    private static async Task AssertProblemAsync(HttpResponseMessage response, ErrorCode code)
    {
        using var problem = JsonDocument.Parse(await response.ReadProblemAsync((HttpStatusCode)code.Status));
        Assert.Equal(code.Code, problem.RootElement.GetProperty(ErrorCode.Extension).GetString());
    }

    // A goal, as given, of a participant added to A for the test, active unless archivedParticipant.
    private async Task<(Guid ParticipantId, AddedGoal Goal)> AddGoalAsync(
        TestGoal goal, bool archivedParticipant = false)
    {
        var participant = await app.Services.AddParticipantAsync(seeded.A, archivedParticipant);
        return (participant.Id, await app.Services.AddGoalAsync(seeded.A, participant.Id, goal));
    }

    private async Task<HttpResponseMessage> PostAsync(Guid participantId, object body)
    {
        using var client = app.CreateHttpsClient().SignInAs(Caller.ManagerOfA, seeded);
        return await client.PostWithTokenAsync(
            $"/api/admin/participants/{participantId}/goals", await client.FetchTokenAsync(), body);
    }

    private async Task<HttpResponseMessage> PutAsync(Guid goalId, object body, string? ifMatch)
    {
        using var client = app.CreateHttpsClient().SignInAs(Caller.ManagerOfA, seeded);
        return await client.PutWithTokenAsync(
            $"/api/admin/goals/{goalId}", await client.FetchTokenAsync(), body, ifMatch);
    }

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

    private async Task<List<Goal>> StoredForAsync(Guid participantId)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(seeded.A.OrganisationId))
        {
            return await db.Goals.AsNoTracking()
                .Where(goal => goal.ParticipantId == participantId)
                .ToListAsync(TestContext.Current.CancellationToken);
        }
    }

    // The Details of the events written about the goal, oldest first, read in SQL, with no tenant needed.
    private async Task<List<JsonNode>> DetailsAsync(Guid goalId)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var details = await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Database
            .SqlQuery<string>(
                $"SELECT [Details] AS [Value] FROM [AuditEvent] WHERE [EntityId] = {goalId} ORDER BY [Id]")
            .ToListAsync(TestContext.Current.CancellationToken);
        return [.. details.Select(text => JsonNode.Parse(text)!)];
    }
}
