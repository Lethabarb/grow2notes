using System.Net;
using System.Text.Json;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Grow2Notes.Web.Platform.Audit;
using Grow2Notes.Web.Platform.Configuration;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Grow2Notes.Tests.Features.Participants;

/// <summary>
/// <c>PUT /api/admin/goals/{goalId}</c> (design.md §6.6, S01.02.06), called with the test-only sign-in
/// (<see cref="TestSignIn"/>) on a fake clock: a manager rewords a goal of their own organisation with the goal's
/// <c>ETag</c> as <c>If-Match</c>, its text cleaned and checked by the add's rules, and gets <c>200</c> with no body
/// and the goal's new <c>ETag</c>; the text already stored answers <c>200</c> with the same <c>ETag</c> and writes
/// nothing (D84); with no <c>If-Match</c>, <c>428</c>; with a stale one, or a goal changed after the handler loaded
/// it, <c>412</c>; a text that the add refuses, <c>422 validation.failed</c> in the screen's words; and a goal that is
/// unknown, or another organisation's, <c>404</c>; each refusal changing nothing and writing no event. Each test
/// rewords a goal that it adds to a participant of its own in A (tests/README.md). The endpoint matrix covers the
/// statuses of every caller, and <see cref="Platform.Audit.AuditEventsTests"/> the <c>goal.updated</c> that a reword
/// writes.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class UpdateGoalEndpointTests : IClassFixture<Grow2NotesFactory>, IAsyncDisposable
{
    private const string StoredText = "Makes own breakfast";

    private const string EnterTheGoal = "Enter the goal";

    private const string TooLong = "Goal must be 200 characters or less";

    private readonly WebApplicationFactory<Program> app;

    private readonly SeededOrganisations seeded;

    public UpdateGoalEndpointTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    {
        app = factory.WithWebHostBuilder(builder => builder.UseClock(new FakeTimeProvider(TestClock.Start)));
        seeded = sqlServer.Seeded;
    }

    public static TheoryData<string?> BlankTexts { get; } = new("", "   ", "\t \r\n \n", null);

    [Fact]
    public async Task A_reword_answers_200_with_no_body_and_the_goal_s_new_ETag_and_stores_the_text_alone()
    {
        var (participantId, goal) = await AddGoalAsync(new(StoredText, 2));

        using var response = await PutAsync(Caller.ManagerOfA, goal.Id, new { text = "Makes own lunch" }, goal.ETag);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Empty(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var stored = await StoredAsync(goal.Id);
        Assert.Equal(
            ("Makes own lunch", 2, (DateTime?)null, TestGoals.AddedAtUtc, participantId),
            (stored.Text, stored.SortOrder, stored.ArchivedAtUtc, stored.CreatedAtUtc, stored.ParticipantId));
        Assert.Equal(RowVersionToken.ETag(stored), ETagOf(response));
        Assert.NotEqual(goal.ETag, ETagOf(response));
        Assert.Equal([AuditEventTypes.Goal.Updated], await EventTypesAsync(goal.Id));
    }

    // As the add cleans it (list-editor.md *Implementation notes*): a line break, with the spaces on either side of
    // it, becomes one space, and the ends are trimmed; 200 characters remain, so the text is stored.
    [Fact]
    public async Task Line_breaks_are_stored_as_one_space_and_200_characters_once_cleaned_are_stored()
    {
        var (_, goal) = await AddGoalAsync(new(StoredText, 0));

        using var response = await PutAsync(
            Caller.ManagerOfA,
            goal.Id,
            new { text = $"  {new string('a', 100)} \r\n\n {new string('b', 99)}\t" },
            goal.ETag);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new string('a', 100) + " " + new string('b', 99), (await StoredAsync(goal.Id)).Text);
    }

    // D84: the save finds the text unchanged, so it saves nothing, and the version stays the one the If-Match matched.
    [Theory]
    [InlineData(StoredText)]
    [InlineData(" Makes own\n  breakfast ")]
    public async Task A_reword_to_the_stored_text_answers_200_with_the_same_ETag_and_writes_nothing(string text)
    {
        var (_, goal) = await AddGoalAsync(new(StoredText, 0));

        using var response = await PutAsync(Caller.ManagerOfA, goal.Id, new { text }, goal.ETag);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(goal.ETag, ETagOf(response));
        await AssertUnchangedAsync(goal);
    }

    [Fact]
    public async Task With_no_If_Match_it_answers_428_and_changes_nothing()
    {
        var (_, goal) = await AddGoalAsync(new(StoredText, 0));

        using var response = await PutAsync(Caller.ManagerOfA, goal.Id, new { text = "Makes own lunch" }, null);

        await AssertProblemAsync(response, ErrorCode.PreconditionRequired);
        await AssertUnchangedAsync(goal);
    }

    // As when another manager saved first: the second reword, sent with the version both loaded, changes nothing.
    [Fact]
    public async Task With_a_stale_If_Match_it_answers_412_and_writes_nothing()
    {
        var (_, goal) = await AddGoalAsync(new(StoredText, 0));
        using (var first = await PutAsync(Caller.ManagerOfA, goal.Id, new { text = "Makes own lunch" }, goal.ETag))
        {
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        }

        var afterFirst = await StoredAsync(goal.Id);

        using var response = await PutAsync(Caller.ManagerOfA, goal.Id, new { text = "Makes own dinner" }, goal.ETag);

        await AssertProblemAsync(response, ErrorCode.PreconditionFailed);
        Assert.Null(response.Headers.ETag);
        var stored = await StoredAsync(goal.Id);
        Assert.Equal(
            ("Makes own lunch", RowVersionToken.ETag(afterFirst)), (stored.Text, RowVersionToken.ETag(stored)));
        Assert.Equal([AuditEventTypes.Goal.Updated], await EventTypesAsync(goal.Id));
    }

    // The race that If-Match cannot catch: two managers reword at once, both with the ETag that their pages loaded. The
    // first's handler has loaded the goal and checked it, and the second's reword commits before the first's save,
    // whose concurrency check then matches no row. That is a goal changed since it was loaded, as a stale If-Match is,
    // so it gets 412, never 500, and the second's wording stands.
    [Fact]
    public async Task A_reword_whose_save_finds_the_goal_changed_since_it_was_loaded_gets_412_and_writes_nothing()
    {
        var (_, goal) = await AddGoalAsync(new(StoredText, 0));
        var gate = new HoldsTheFirstSaveOfTheGoal(goal.Id);
        await using var gated = app.WithWebHostBuilder(builder => builder.ConfigureTestServices(
            services => services.ConfigureDbContext<Grow2NotesDbContext>(options => options.AddInterceptors(gate))));

        var first = PutAsync(gated, Caller.ManagerOfA, goal.Id, new { text = "Makes own dinner" }, goal.ETag);
        try
        {
            await gate.Reached.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            using var second = await PutAsync(
                gated, Caller.ManagerOfA, goal.Id, new { text = "Makes own lunch" }, goal.ETag);
            Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        }
        finally
        {
            gate.Release();
        }

        using var response = await first;
        await AssertProblemAsync(response, ErrorCode.PreconditionFailed);
        Assert.Null(response.Headers.ETag);
        Assert.Equal("Makes own lunch", (await StoredAsync(goal.Id)).Text);
        Assert.Equal([AuditEventTypes.Goal.Updated], await EventTypesAsync(goal.Id));
    }

    [Fact]
    public async Task A_goal_of_201_characters_once_cleaned_is_refused_and_nothing_is_changed()
    {
        await AssertRefusedAsync(new { text = new string('a', Limits.ItemText + 1) }, TooLong);
        await AssertRefusedAsync(new { text = $"{new string('a', 100)}\n{new string('b', 100)}" }, TooLong);
    }

    [Theory]
    [MemberData(nameof(BlankTexts))]
    public async Task A_blank_or_null_text_is_refused_and_nothing_is_changed(string? text)
    {
        await AssertRefusedAsync(new { text }, EnterTheGoal);
    }

    [Fact]
    public async Task A_body_with_no_text_is_refused_and_nothing_is_changed()
    {
        await AssertRefusedAsync(new { goal = "Makes own lunch" }, EnterTheGoal);
    }

    // The screen offers Edit on active goals alone, but the server refuses nothing on an archived one (T05's Notes).
    [Fact]
    public async Task An_archived_goal_is_reworded_and_stays_archived_in_its_place()
    {
        var (_, goal) = await AddGoalAsync(new(StoredText, 4, ArchivedAtUtc: TestGoals.AddedAtUtc));

        using var response = await PutAsync(Caller.ManagerOfA, goal.Id, new { text = "Makes own lunch" }, goal.ETag);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var stored = await StoredAsync(goal.Id);
        Assert.Equal(
            ("Makes own lunch", 4, (DateTime?)TestGoals.AddedAtUtc),
            (stored.Text, stored.SortOrder, stored.ArchivedAtUtc));
    }

    // B's manager, sending the goal's current ETag, as one who had it would: the tenant filter finds no such goal in B,
    // so the version is never checked (design.md §9.2).
    [Fact]
    public async Task Another_organisation_s_goal_is_a_404_problem_and_not_changed()
    {
        var (_, goal) = await AddGoalAsync(new(StoredText, 0));

        using var response = await PutAsync(Caller.ManagerOfB, goal.Id, new { text = "Makes own lunch" }, goal.ETag);

        await response.ReadProblemAsync(HttpStatusCode.NotFound);
        await AssertUnchangedAsync(goal);
    }

    // The goal is looked for first, so an unknown one is a 404 even when its If-Match or text would be refused too.
    [Theory]
    [InlineData("Makes own lunch", true)]
    [InlineData("", false)]
    public async Task A_goal_of_no_organisation_is_a_404_problem(string text, bool withIfMatch)
    {
        var lastEventId = await LastEventIdAsync();

        using var response = await PutAsync(
            Caller.ManagerOfA, Guid.NewGuid(), new { text }, withIfMatch ? IfMatch.ETag("AAAAAAAAB9E=") : null);

        await response.ReadProblemAsync(HttpStatusCode.NotFound);
        Assert.Equal(lastEventId, await LastEventIdAsync());
    }

    // The route takes a GUID alone, so another ID matches no endpoint, and gets the unknown API path's 404.
    [Fact]
    public async Task An_ID_that_is_not_a_GUID_is_a_404()
    {
        using var client = app.CreateHttpsClient().SignInAs(Caller.ManagerOfA, seeded);

        using var response = await client.PutWithTokenAsync(
            "/api/admin/goals/makes-own-breakfast",
            await client.FetchTokenAsync(),
            new { text = "Makes own lunch" },
            IfMatch.ETag("AAAAAAAAB9E="));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // The /api/admin group's policy refuses the worker before the handler, which would otherwise reword the goal.
    [Fact]
    public async Task A_worker_gets_403_and_nothing_is_changed()
    {
        var (_, goal) = await AddGoalAsync(new(StoredText, 0));

        using var response = await PutAsync(Caller.WorkerOfA, goal.Id, new { text = "Makes own lunch" }, goal.ETag);

        await response.ReadProblemAsync(HttpStatusCode.Forbidden);
        await AssertUnchangedAsync(goal);
    }

    public ValueTask DisposeAsync() => app.DisposeAsync();

    private static async Task AssertProblemAsync(HttpResponseMessage response, ErrorCode code)
    {
        using var problem = JsonDocument.Parse(await response.ReadProblemAsync((HttpStatusCode)code.Status));
        Assert.Equal(code.Code, problem.RootElement.GetProperty(ErrorCode.Extension).GetString());
    }

    // The header as sent, W/ and all, so a weak ETag never equals the row's strong one.
    private static string ETagOf(HttpResponseMessage response)
    {
        var etag = response.Headers.ETag;
        Assert.NotNull(etag);
        return etag.ToString();
    }

    /// <summary>
    /// Sends <paramref name="body"/> as A's manager, with the ETag, to a goal added to A for the call, and asserts that
    /// it gets <c>422 validation.failed</c> with <paramref name="message"/> alone, under <c>text</c>, and that the goal
    /// was not changed and no event written.
    /// </summary>
    private async Task AssertRefusedAsync(object body, string message)
    {
        var (_, goal) = await AddGoalAsync(new(StoredText, 0));

        using var response = await PutAsync(Caller.ManagerOfA, goal.Id, body, goal.ETag);

        using var problem = JsonDocument.Parse(await response.ReadProblemAsync(HttpStatusCode.UnprocessableEntity));
        Assert.Equal(
            ErrorCode.ValidationFailed.Code, problem.RootElement.GetProperty(ErrorCode.Extension).GetString());
        Assert.Equal(
            [("text", message)],
            problem.RootElement.GetProperty("errors").EnumerateObject()
                .Select(field => (field.Name, Assert.Single(field.Value.EnumerateArray()).GetString() ?? "")));
        await AssertUnchangedAsync(goal);
    }

    // The goal as AddGoalAsync left it, at the same version, with no event written about it.
    private async Task AssertUnchangedAsync(AddedGoal goal)
    {
        var stored = await StoredAsync(goal.Id);
        Assert.Equal((StoredText, goal.ETag), (stored.Text, RowVersionToken.ETag(stored)));
        Assert.Empty(await EventTypesAsync(goal.Id));
    }

    // A goal, as given, of a participant added to A for the test.
    private async Task<(Guid ParticipantId, AddedGoal Goal)> AddGoalAsync(TestGoal goal)
    {
        var participant = await app.Services.AddParticipantAsync(seeded.A);
        return (participant.Id, await app.Services.AddGoalAsync(seeded.A, participant.Id, goal));
    }

    private Task<HttpResponseMessage> PutAsync(Caller caller, Guid goalId, object body, string? ifMatch) =>
        PutAsync(app, caller, goalId, body, ifMatch);

    // As the SPA rewords one: over HTTPS, with a token fetched as the caller, and, unless null, the ETag as If-Match.
    private async Task<HttpResponseMessage> PutAsync(
        WebApplicationFactory<Program> host, Caller caller, Guid goalId, object body, string? ifMatch)
    {
        using var client = host.CreateHttpsClient().SignInAs(caller, seeded);
        return await client.PutWithTokenAsync(
            $"/api/admin/goals/{goalId}", await client.FetchTokenAsync(), body, ifMatch);
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

    // The tests of the collection run one at a time, so none writes an event while a test compares this before and
    // after its request.
    private async Task<long?> LastEventIdAsync()
    {
        await using var scope = app.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Database
            .SqlQuery<long?>($"SELECT MAX([Id]) AS [Value] FROM [AuditEvent]")
            .SingleAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Holds back the first save that changes the goal whose ID it is given, until the test lets it go: in a reword,
    /// the one save of the text and its event, made once the handler has loaded the goal and checked its ETag. Every
    /// other save passes.
    /// </summary>
    private sealed class HoldsTheFirstSaveOfTheGoal(Guid goalId) : SaveChangesInterceptor
    {
        private readonly TaskCompletionSource reached = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource released = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int held;

        public Task Reached => reached.Task;

        public void Release() => released.TrySetResult();

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context is { } context
                && context.ChangeTracker.Entries<Goal>().Any(
                    entry => entry.State == EntityState.Modified && entry.Entity.Id == goalId)
                && Interlocked.Exchange(ref held, 1) == 0)
            {
                reached.SetResult();
                await released.Task.WaitAsync(cancellationToken);
            }

            return result;
        }
    }
}
