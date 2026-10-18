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

namespace Grow2Notes.Tests.Features.CommonItems;

/// <summary>
/// <c>PUT /api/admin/common-item-groups/{groupId}</c> (design.md §6.6, S01.03.05), called with the test-only sign-in
/// (<see cref="TestSignIn"/>) on a fake clock: a manager renames a group of their own organisation with the group's
/// <c>ETag</c> as <c>If-Match</c>, its name cleaned and checked by the add's rules, and gets <c>200</c> with no body
/// and no <c>ETag</c>; the name already stored answers the same and writes nothing (D84); a group that is unknown, or
/// another organisation's, gets <c>404</c>, then, with no <c>If-Match</c>, <c>428</c>, with a stale one, or a group
/// changed after the handler loaded it, <c>412</c>, then Every note <c>422 config.every_note_fixed</c> (A43), then a
/// name that the add refuses <c>422 validation.failed</c> in the screen's words; each refusal changing nothing and
/// writing no event. Each test renames a group that it adds to A, but for Every note, which a refusal leaves as it was,
/// and changes no seeded row (tests/README.md). The endpoint matrix covers the statuses of every caller, and
/// <see cref="Platform.Audit.AuditEventsTests"/> the <c>common_item_group.updated</c> that a rename writes.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class RenameCommonItemGroupEndpointTests : IClassFixture<Grow2NotesFactory>, IAsyncDisposable
{
    private const string StoredName = "Community outing";

    private const string EnterTheGroupName = "Enter the group name";

    private const string TooLong = "Group name must be 200 characters or less";

    private static readonly DateTime ArchivedAtUtc = TestCommonItems.AddedAtUtc;

    private readonly WebApplicationFactory<Program> app;

    private readonly SeededOrganisations seeded;

    public RenameCommonItemGroupEndpointTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    {
        app = factory.WithWebHostBuilder(builder => builder.UseClock(new FakeTimeProvider(TestClock.Start)));
        seeded = sqlServer.Seeded;
    }

    public static TheoryData<string?> BlankNames { get; } = new("", "   ", "\t \r\n \n", null);

    [Fact]
    public async Task A_rename_answers_200_with_no_body_and_no_ETag_and_stores_the_name_alone()
    {
        var group = await AddGroupAsync(new(StoredName, 2));

        using var response = await PutAsync(Caller.ManagerOfA, group.Id, new { name = "Outings" }, group.ETag);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Null(response.Headers.ETag);
        Assert.Empty(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var stored = await StoredAsync(group.Id);
        Assert.Equal(
            ("Outings", 2, (DateTime?)null, TestCommonItems.AddedAtUtc, false),
            (stored.Name, stored.SortOrder, stored.ArchivedAtUtc, stored.CreatedAtUtc, stored.IsEveryNote));
        Assert.NotEqual(group.ETag, RowVersionToken.ETag(stored));
        Assert.Equal([AuditEventTypes.CommonItemGroup.Updated], await EventTypesAsync(group.Id));
    }

    // As the add cleans it (common-items.md *Validation*): a line break, with the spaces on either side of it, becomes
    // one space, and the ends are trimmed; 200 characters remain, so the name is stored.
    [Fact]
    public async Task Line_breaks_are_stored_as_one_space_and_200_characters_once_cleaned_are_stored()
    {
        var group = await AddGroupAsync(new(StoredName, 0));

        using var response = await PutAsync(
            Caller.ManagerOfA,
            group.Id,
            new { name = $"  {new string('a', 100)} \r\n\n {new string('b', 99)}\t" },
            group.ETag);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new string('a', 100) + " " + new string('b', 99), (await StoredAsync(group.Id)).Name);
    }

    // D84: the save finds the name unchanged, so it saves nothing, and the version stays the one the If-Match matched.
    [Theory]
    [InlineData(StoredName)]
    [InlineData(" Community\n  outing ")]
    public async Task A_rename_to_the_stored_name_answers_200_and_writes_nothing(string name)
    {
        var group = await AddGroupAsync(new(StoredName, 0));

        using var response = await PutAsync(Caller.ManagerOfA, group.Id, new { name }, group.ETag);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(response.Headers.ETag);
        await AssertUnchangedAsync(group);
    }

    [Fact]
    public async Task With_no_If_Match_it_answers_428_and_changes_nothing()
    {
        var group = await AddGroupAsync(new(StoredName, 0));

        using var response = await PutAsync(Caller.ManagerOfA, group.Id, new { name = "Outings" }, null);

        await AssertProblemAsync(response, ErrorCode.PreconditionRequired);
        await AssertUnchangedAsync(group);
    }

    // As when another manager saved first: the second rename, sent with the version both loaded, changes nothing.
    [Fact]
    public async Task With_a_stale_If_Match_it_answers_412_and_writes_nothing()
    {
        var group = await AddGroupAsync(new(StoredName, 0));
        using (var first = await PutAsync(Caller.ManagerOfA, group.Id, new { name = "Outings" }, group.ETag))
        {
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        }

        var afterFirst = await StoredAsync(group.Id);

        using var response = await PutAsync(Caller.ManagerOfA, group.Id, new { name = "Day trips" }, group.ETag);

        await AssertProblemAsync(response, ErrorCode.PreconditionFailed);
        var stored = await StoredAsync(group.Id);
        Assert.Equal(("Outings", RowVersionToken.ETag(afterFirst)), (stored.Name, RowVersionToken.ETag(stored)));
        Assert.Equal([AuditEventTypes.CommonItemGroup.Updated], await EventTypesAsync(group.Id));
    }

    // The race that If-Match cannot catch: two managers rename at once, both with the version that their pages loaded.
    // The first's handler has loaded the group and checked it, and the second's rename commits before the first's
    // save, whose concurrency check then matches no row. That is a group changed since it was loaded, as a stale
    // If-Match is, so it gets 412, never 500, and the second's name stands.
    [Fact]
    public async Task A_rename_whose_save_finds_the_group_changed_since_it_was_loaded_gets_412_and_writes_nothing()
    {
        var group = await AddGroupAsync(new(StoredName, 0));
        var gate = new HoldsTheFirstSaveOfTheGroup(group.Id);
        await using var gated = app.WithWebHostBuilder(builder => builder.ConfigureTestServices(
            services => services.ConfigureDbContext<Grow2NotesDbContext>(options => options.AddInterceptors(gate))));

        var first = PutAsync(gated, Caller.ManagerOfA, group.Id, new { name = "Day trips" }, group.ETag);
        try
        {
            await gate.Reached.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            using var second = await PutAsync(gated, Caller.ManagerOfA, group.Id, new { name = "Outings" }, group.ETag);
            Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        }
        finally
        {
            gate.Release();
        }

        using var response = await first;
        await AssertProblemAsync(response, ErrorCode.PreconditionFailed);
        Assert.Equal("Outings", (await StoredAsync(group.Id)).Name);
        Assert.Equal([AuditEventTypes.CommonItemGroup.Updated], await EventTypesAsync(group.Id));
    }

    [Fact]
    public async Task A_name_of_201_characters_once_cleaned_is_refused_and_nothing_is_changed()
    {
        await AssertRefusedAsync(new { name = new string('a', Limits.GroupName + 1) }, TooLong);
        await AssertRefusedAsync(new { name = $"{new string('a', 100)}\n{new string('b', 100)}" }, TooLong);
    }

    [Theory]
    [MemberData(nameof(BlankNames))]
    public async Task A_blank_or_null_name_is_refused_and_nothing_is_changed(string? name)
    {
        await AssertRefusedAsync(new { name }, EnterTheGroupName);
    }

    [Fact]
    public async Task A_body_with_no_name_is_refused_and_nothing_is_changed()
    {
        await AssertRefusedAsync(new { title = "Outings" }, EnterTheGroupName);
    }

    // Every note is looked at after If-Match and before the name, so it is refused as fixed even when the name would be
    // refused too (A43). The screen never offers its rename, so the screen takes this as something gone wrong.
    [Theory]
    [InlineData("Outings")]
    [InlineData("")]
    public async Task Every_note_is_a_422_every_note_fixed_problem_and_keeps_its_name(string name)
    {
        var everyNote = await StoredAsync(seeded.A.EveryNoteGroupId);
        var lastEventId = await LastEventIdAsync();

        using var response = await PutAsync(
            Caller.ManagerOfA, everyNote.Id, new { name }, RowVersionToken.ETag(everyNote));

        await AssertProblemAsync(response, ErrorCode.EveryNoteFixed);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        var stored = await StoredAsync(everyNote.Id);
        Assert.Equal(
            ("Every note", true, RowVersionToken.ETag(everyNote)),
            (stored.Name, stored.IsEveryNote, RowVersionToken.ETag(stored)));
        Assert.Equal(lastEventId, await LastEventIdAsync());
    }

    // If-Match is checked before Every note is, so a missing one is refused as such, as for any other group.
    [Fact]
    public async Task Every_note_with_no_If_Match_is_refused_for_its_If_Match()
    {
        var lastEventId = await LastEventIdAsync();

        using var response = await PutAsync(
            Caller.ManagerOfA, seeded.A.EveryNoteGroupId, new { name = "Outings" }, null);

        await AssertProblemAsync(response, ErrorCode.PreconditionRequired);
        Assert.Equal("Every note", (await StoredAsync(seeded.A.EveryNoteGroupId)).Name);
        Assert.Equal(lastEventId, await LastEventIdAsync());
    }

    // The screen offers Rename on active groups alone, but the server refuses nothing on an archived group, which keeps
    // its place and stays archived (T07's Notes).
    [Fact]
    public async Task An_archived_group_is_renamed_and_stays_archived_in_its_place()
    {
        var group = await AddGroupAsync(new(StoredName, 4, ArchivedAtUtc: ArchivedAtUtc));

        using var response = await PutAsync(Caller.ManagerOfA, group.Id, new { name = "Outings" }, group.ETag);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var stored = await StoredAsync(group.Id);
        Assert.Equal(("Outings", 4, (DateTime?)ArchivedAtUtc), (stored.Name, stored.SortOrder, stored.ArchivedAtUtc));
    }

    // B's manager does not find A's groups (design.md §9.2), so the version is never checked, and one who sends the
    // group's current ETag, as one who had it would, gets the same 404 as one who sends none.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Another_organisation_s_group_is_a_404_problem_and_not_changed(bool withItsETag)
    {
        var group = await AddGroupAsync(new(StoredName, 0));

        using var response = await PutAsync(
            Caller.ManagerOfB, group.Id, new { name = "Outings" }, withItsETag ? group.ETag : null);

        await response.ReadProblemAsync(HttpStatusCode.NotFound);
        await AssertUnchangedAsync(group);
    }

    // B's own Every note is not found by A's manager either, so it is a 404, never the 422 that would say it exists.
    [Fact]
    public async Task Another_organisation_s_Every_note_is_a_404_problem()
    {
        var lastEventId = await LastEventIdAsync();

        using var response = await PutAsync(
            Caller.ManagerOfA, seeded.B.EveryNoteGroupId, new { name = "Outings" }, IfMatch.ETag("AAAAAAAAB9E="));

        await response.ReadProblemAsync(HttpStatusCode.NotFound);
        Assert.Equal(lastEventId, await LastEventIdAsync());
    }

    // The group is looked for first, so an unknown one is a 404 even when its If-Match or name would be refused too.
    [Theory]
    [InlineData("Outings", true)]
    [InlineData("", false)]
    public async Task A_group_of_no_organisation_is_a_404_problem(string name, bool withIfMatch)
    {
        var lastEventId = await LastEventIdAsync();

        using var response = await PutAsync(
            Caller.ManagerOfA, Guid.NewGuid(), new { name }, withIfMatch ? IfMatch.ETag("AAAAAAAAB9E=") : null);

        await response.ReadProblemAsync(HttpStatusCode.NotFound);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Equal(lastEventId, await LastEventIdAsync());
    }

    // The route takes a GUID alone, so another ID matches no endpoint, and gets the unknown API path's 404.
    [Fact]
    public async Task An_ID_that_is_not_a_GUID_is_a_404()
    {
        using var client = app.CreateHttpsClient().SignInAs(Caller.ManagerOfA, seeded);

        using var response = await client.PutWithTokenAsync(
            "/api/admin/common-item-groups/community-outing",
            await client.FetchTokenAsync(),
            new { name = "Outings" },
            IfMatch.ETag("AAAAAAAAB9E="));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // The /api/admin group's policy refuses the worker before the handler, which would otherwise rename the group.
    [Fact]
    public async Task A_worker_gets_403_and_nothing_is_changed()
    {
        var group = await AddGroupAsync(new(StoredName, 0));

        using var response = await PutAsync(Caller.WorkerOfA, group.Id, new { name = "Outings" }, group.ETag);

        await response.ReadProblemAsync(HttpStatusCode.Forbidden);
        await AssertUnchangedAsync(group);
    }

    public ValueTask DisposeAsync() => app.DisposeAsync();

    private static async Task AssertProblemAsync(HttpResponseMessage response, ErrorCode code)
    {
        using var problem = JsonDocument.Parse(await response.ReadProblemAsync((HttpStatusCode)code.Status));
        Assert.Equal(code.Code, problem.RootElement.GetProperty(ErrorCode.Extension).GetString());
        Assert.Null(response.Headers.ETag);
    }

    /// <summary>
    /// Sends <paramref name="body"/> as A's manager, with the ETag, to a group added to A for the call, and asserts
    /// that it gets <c>422 validation.failed</c> with <paramref name="message"/> alone, under <c>name</c>, and that the
    /// group was not changed and no event written.
    /// </summary>
    private async Task AssertRefusedAsync(object body, string message)
    {
        var group = await AddGroupAsync(new(StoredName, 0));

        using var response = await PutAsync(Caller.ManagerOfA, group.Id, body, group.ETag);

        using var problem = JsonDocument.Parse(await response.ReadProblemAsync(HttpStatusCode.UnprocessableEntity));
        Assert.Equal(
            ErrorCode.ValidationFailed.Code, problem.RootElement.GetProperty(ErrorCode.Extension).GetString());
        Assert.Equal(
            [("name", message)],
            problem.RootElement.GetProperty("errors").EnumerateObject()
                .Select(field => (field.Name, Assert.Single(field.Value.EnumerateArray()).GetString() ?? "")));
        await AssertUnchangedAsync(group);
    }

    // The group as it was added, at the same version, with no event written about it.
    private async Task AssertUnchangedAsync(AddedGroup group)
    {
        var stored = await StoredAsync(group.Id);
        Assert.Equal((StoredName, group.ETag), (stored.Name, RowVersionToken.ETag(stored)));
        Assert.Empty(await EventTypesAsync(group.Id));
    }

    // A group, as given, added to A for the test.
    private Task<AddedGroup> AddGroupAsync(TestCommonItemGroup group) =>
        app.Services.AddGroupAsync(seeded.A.OrganisationId, group);

    private Task<HttpResponseMessage> PutAsync(Caller caller, Guid groupId, object body, string? ifMatch) =>
        PutAsync(app, caller, groupId, body, ifMatch);

    // As the SPA renames one: over HTTPS, with a token fetched as the caller, and, unless null, the ETag as If-Match.
    private async Task<HttpResponseMessage> PutAsync(
        WebApplicationFactory<Program> host, Caller caller, Guid groupId, object body, string? ifMatch)
    {
        using var client = host.CreateHttpsClient().SignInAs(caller, seeded);
        return await client.PutWithTokenAsync(
            $"/api/admin/common-item-groups/{groupId}", await client.FetchTokenAsync(), body, ifMatch);
    }

    // The group as its row holds it in A, through the app's context under A's tenant.
    private async Task<CommonItemGroup> StoredAsync(Guid groupId)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(seeded.A.OrganisationId))
        {
            return await db.CommonItemGroups.AsNoTracking()
                .SingleAsync(group => group.Id == groupId, TestContext.Current.CancellationToken);
        }
    }

    // The types of the events written about the group, oldest first, read in SQL, with no tenant needed.
    private async Task<List<string>> EventTypesAsync(Guid groupId)
    {
        await using var scope = app.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Database
            .SqlQuery<string>(
                $"SELECT [EventType] AS [Value] FROM [AuditEvent] WHERE [EntityId] = {groupId} ORDER BY [Id]")
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
    /// Holds back the first save that changes the group whose ID it is given, until the test lets it go: in a rename,
    /// the one save of the name and its event, made once the handler has loaded the group, checked its ETag and found
    /// it is not Every note. Every other save passes.
    /// </summary>
    private sealed class HoldsTheFirstSaveOfTheGroup(Guid groupId) : SaveChangesInterceptor
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
                && context.ChangeTracker.Entries<CommonItemGroup>().Any(
                    entry => entry.State == EntityState.Modified && entry.Entity.Id == groupId)
                && Interlocked.Exchange(ref held, 1) == 0)
            {
                reached.SetResult();
                await released.Task.WaitAsync(cancellationToken);
            }

            return result;
        }
    }
}
