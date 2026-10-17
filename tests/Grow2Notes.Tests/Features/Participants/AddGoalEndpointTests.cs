using System.Net;
using System.Text.Json;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Grow2Notes.Web.Platform.Audit;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Grow2Notes.Tests.Features.Participants;

/// <summary>
/// <c>POST /api/admin/participants/{participantId}/goals</c> (design.md §6.6, S01.02.05), called with the test-only
/// sign-in (<see cref="TestSignIn"/>) on a fake clock: a manager adds a goal at the end of the goals of a participant
/// of their own organisation, archived or not (D86), its text on one line with its ends trimmed, and gets <c>201</c>
/// with no body; a text that is empty or too long once cleaned gets <c>422 validation.failed</c> in the screen's words;
/// and a participant who is unknown, or another organisation's, <c>404</c>; each refusal adding nothing and writing no
/// event.
/// The tests add their own participants and goals to the seeded organisations and change no seeded row
/// (tests/README.md). The endpoint matrix covers the statuses of every caller, and
/// <see cref="Platform.Audit.AuditEventsTests"/> the <c>goal.created</c> that an add writes.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class AddGoalEndpointTests : IClassFixture<Grow2NotesFactory>, IAsyncDisposable
{
    private const string EnterTheGoal = "Enter the goal";

    private const string TooLong = "Goal must be 200 characters or less";

    private readonly WebApplicationFactory<Program> app;

    private readonly SeededOrganisations seeded;

    public AddGoalEndpointTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    {
        app = factory.WithWebHostBuilder(builder => builder.UseClock(new FakeTimeProvider(TestClock.Start)));
        seeded = sqlServer.Seeded;
    }

    public static TheoryData<string?> BlankTexts { get; } = new("", "   ", "\t \r\n \n", null);

    [Fact]
    public async Task A_manager_s_add_answers_201_with_no_body_and_stores_the_goal_first_in_an_empty_list()
    {
        var participant = await app.Services.AddParticipantAsync(seeded.A);

        using var response = await PostAsync(Caller.ManagerOfA, participant.Id, new { text = "Makes own breakfast" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Null(response.Headers.Location);
        Assert.Equal("", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var stored = Assert.Single(await StoredAsync(participant.Id));
        Assert.Equal(
            (seeded.A.OrganisationId, "Makes own breakfast", 0, (DateTime?)null, TestClock.Start.UtcDateTime),
            (stored.OrganisationId, stored.Text, stored.SortOrder, stored.ArchivedAtUtc, stored.CreatedAtUtc));
        Assert.Equal([AuditEventTypes.Goal.Created], await EventTypesAsync(stored.Id));
    }

    // Five, as a manager adds them on a phone (design.md §14 M1), each after the last, in the order added.
    [Fact]
    public async Task Goals_added_one_after_another_each_go_to_the_end()
    {
        var participant = await app.Services.AddParticipantAsync(seeded.A);
        string[] texts =
            ["Makes own breakfast", "Catches the bus", "Phones her sister", "Walks to the shop", "Waters the garden"];

        foreach (var text in texts)
        {
            using var response = await PostAsync(Caller.ManagerOfA, participant.Id, new { text });
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        var stored = await StoredAsync(participant.Id);
        Assert.Equal(texts.Select((text, index) => (text, index)), stored.Select(goal => (goal.Text, goal.SortOrder)));
    }

    // Archiving leaves gaps, and keeps an archived goal's SortOrder, which may be above every active one's (A2).
    [Fact]
    public async Task A_goal_added_after_archived_ones_goes_one_after_the_highest_active_SortOrder()
    {
        var participant = await app.Services.AddParticipantAsync(seeded.A);
        await app.Services.AddGoalsAsync(
            seeded.A,
            participant.Id,
            new("Makes own breakfast", 0),
            new("Walks to the shop", 1, ArchivedAtUtc: TestGoals.AddedAtUtc),
            new("Phones her sister", 2),
            new("Waters the garden", 7, ArchivedAtUtc: TestGoals.AddedAtUtc));

        using var response = await PostAsync(Caller.ManagerOfA, participant.Id, new { text = "Catches the bus" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(3, (await StoredAsync(participant.Id)).Single(goal => goal.Text == "Catches the bus").SortOrder);
    }

    // As the screen cleans it before it sends it (list-editor.md *Implementation notes*): a line break, CR, LF or
    // both, with the spaces and tabs on either side of it, becomes one space, and the ends are trimmed. Spaces
    // elsewhere stay.
    [Theory]
    [InlineData("  Makes own breakfast\t", "Makes own breakfast")]
    [InlineData("Catches the bus\nto day program", "Catches the bus to day program")]
    [InlineData("Catches the bus \r\n\r\n  to day program", "Catches the bus to day program")]
    [InlineData("\nPhones\rher\t\nsister\n", "Phones her sister")]
    [InlineData("Cooks phở  with Nguyễn", "Cooks phở  with Nguyễn")]
    public async Task Line_breaks_are_stored_as_one_space_and_the_ends_trimmed(string text, string storedText)
    {
        var participant = await app.Services.AddParticipantAsync(seeded.A);

        using var response = await PostAsync(Caller.ManagerOfA, participant.Id, new { text });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(storedText, Assert.Single(await StoredAsync(participant.Id)).Text);
    }

    // The limit counts the text as it is stored: 200 characters, the spaces at the ends left out and a line break
    // counted as one.
    [Fact]
    public async Task A_goal_of_200_characters_once_cleaned_is_stored()
    {
        var participant = await app.Services.AddParticipantAsync(seeded.A);
        var longest = new string('a', 100) + " " + new string('b', 99);

        using var response = await PostAsync(
            Caller.ManagerOfA, participant.Id, new { text = $"  {new string('a', 100)} \n\n {new string('b', 99)}  " });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(longest, Assert.Single(await StoredAsync(participant.Id)).Text);
    }

    [Fact]
    public async Task A_goal_of_201_characters_once_cleaned_is_refused_and_nothing_is_added()
    {
        await AssertRefusedAsync(new { text = new string('a', Limits.ItemText + 1) }, TooLong);
        await AssertRefusedAsync(new { text = $"{new string('a', 100)}\n{new string('b', 100)}" }, TooLong);
    }

    [Theory]
    [MemberData(nameof(BlankTexts))]
    public async Task A_blank_or_null_text_is_refused_and_nothing_is_added(string? text)
    {
        await AssertRefusedAsync(new { text }, EnterTheGoal);
    }

    [Fact]
    public async Task A_body_with_no_text_is_refused_and_nothing_is_added()
    {
        await AssertRefusedAsync(new { goal = "Makes own breakfast" }, EnterTheGoal);
    }

    // Only the screen is read-only for an archived participant: Release 1's server refuses nothing on one (D86).
    [Fact]
    public async Task An_archived_participant_s_goal_is_added()
    {
        var participant = await app.Services.AddParticipantAsync(seeded.A, archived: true);

        using var response = await PostAsync(Caller.ManagerOfA, participant.Id, new { text = "Makes own breakfast" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("Makes own breakfast", Assert.Single(await StoredAsync(participant.Id)).Text);
    }

    // B's manager does not find A's participant, nor A's B's (§9.2), so neither adds a goal to the other's.
    [Fact]
    public async Task Another_organisation_s_participant_is_a_404_problem_and_nothing_is_added()
    {
        var participant = await app.Services.AddParticipantAsync(seeded.A);
        var lastEventId = await LastEventIdAsync();

        foreach (var (caller, participantId) in new[]
        {
            (Caller.ManagerOfB, participant.Id),
            (Caller.ManagerOfB, seeded.A.ParticipantId),
            (Caller.ManagerOfA, seeded.B.ParticipantId),
        })
        {
            var goalsBefore = await GoalCountAsync(participantId);

            using var response = await PostAsync(caller, participantId, new { text = "Makes own breakfast" });

            await response.ReadProblemAsync(HttpStatusCode.NotFound);
            Assert.Equal(goalsBefore, await GoalCountAsync(participantId));
        }

        Assert.Equal(lastEventId, await LastEventIdAsync());
    }

    // The participant is looked for first, so an unknown one is a 404 even when the text would be refused too.
    [Theory]
    [InlineData("Makes own breakfast")]
    [InlineData("")]
    public async Task A_participant_of_no_organisation_is_a_404_problem(string text)
    {
        var lastEventId = await LastEventIdAsync();

        using var response = await PostAsync(Caller.ManagerOfA, Guid.NewGuid(), new { text });

        await response.ReadProblemAsync(HttpStatusCode.NotFound);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Equal(lastEventId, await LastEventIdAsync());
    }

    // The route takes a GUID alone, so another ID matches no endpoint, and gets the unknown API path's 404.
    [Fact]
    public async Task An_ID_that_is_not_a_GUID_is_a_404()
    {
        using var client = app.CreateHttpsClient().SignInAs(Caller.ManagerOfA, seeded);

        using var response = await client.PostWithTokenAsync(
            "/api/admin/participants/jane-citizen/goals",
            await client.FetchTokenAsync(),
            new { text = "Makes own breakfast" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // The /api/admin group's policy refuses the worker before the handler, which would otherwise add the goal.
    [Fact]
    public async Task A_worker_gets_403_and_nothing_is_added()
    {
        var participant = await app.Services.AddParticipantAsync(seeded.A);
        var lastEventId = await LastEventIdAsync();

        using var response = await PostAsync(Caller.WorkerOfA, participant.Id, new { text = "Makes own breakfast" });

        await response.ReadProblemAsync(HttpStatusCode.Forbidden);
        Assert.Equal(0, await GoalCountAsync(participant.Id));
        Assert.Equal(lastEventId, await LastEventIdAsync());
    }

    public ValueTask DisposeAsync() => app.DisposeAsync();

    /// <summary>
    /// Sends <paramref name="body"/> as A's manager to a participant added to A for the call, and asserts that it gets
    /// <c>422 validation.failed</c> with <paramref name="message"/> alone, under <c>text</c>, and that no goal was
    /// added and no event written.
    /// </summary>
    private async Task AssertRefusedAsync(object body, string message)
    {
        var participant = await app.Services.AddParticipantAsync(seeded.A);
        var lastEventId = await LastEventIdAsync();

        using var response = await PostAsync(Caller.ManagerOfA, participant.Id, body);

        using var problem = JsonDocument.Parse(await response.ReadProblemAsync(HttpStatusCode.UnprocessableEntity));
        Assert.Equal(
            ErrorCode.ValidationFailed.Code, problem.RootElement.GetProperty(ErrorCode.Extension).GetString());
        Assert.Equal(
            [("text", message)],
            problem.RootElement.GetProperty("errors").EnumerateObject()
                .Select(field => (field.Name, Assert.Single(field.Value.EnumerateArray()).GetString() ?? "")));
        Assert.Equal(0, await GoalCountAsync(participant.Id));
        Assert.Equal(lastEventId, await LastEventIdAsync());
    }

    // As the SPA adds one: over HTTPS, with a token fetched as the caller, and the body as JSON.
    private async Task<HttpResponseMessage> PostAsync(Caller caller, Guid participantId, object body)
    {
        using var client = app.CreateHttpsClient().SignInAs(caller, seeded);
        return await client.PostWithTokenAsync(
            $"/api/admin/participants/{participantId}/goals", await client.FetchTokenAsync(), body);
    }

    // The participant's goals as their rows hold them, in A, through the app's context under A's tenant, in SortOrder.
    private async Task<List<Goal>> StoredAsync(Guid participantId)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(seeded.A.OrganisationId))
        {
            return await db.Goals.AsNoTracking()
                .Where(goal => goal.ParticipantId == participantId)
                .OrderBy(goal => goal.SortOrder)
                .ToListAsync(TestContext.Current.CancellationToken);
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

    // Read in SQL, in whichever organisation, with no tenant needed. The tests of the collection run one at a time, so
    // none adds a goal or writes an event while a test compares these before and after its request.
    private async Task<int> GoalCountAsync(Guid participantId)
    {
        await using var scope = app.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Database
            .SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM [Goal] WHERE [ParticipantId] = {participantId}")
            .SingleAsync(TestContext.Current.CancellationToken);
    }

    private async Task<long?> LastEventIdAsync()
    {
        await using var scope = app.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Database
            .SqlQuery<long?>($"SELECT MAX([Id]) AS [Value] FROM [AuditEvent]")
            .SingleAsync(TestContext.Current.CancellationToken);
    }
}
