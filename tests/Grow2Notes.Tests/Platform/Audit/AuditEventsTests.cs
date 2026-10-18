using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Features.Notes;
using Grow2Notes.Web.Platform;
using Grow2Notes.Web.Platform.Audit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests.Platform.Audit;

/// <summary>
/// The shared audit-events test (design.md §5.3, §5.4, §14 M5; S05.02.01): one table-driven test with a row for each
/// event type of the catalogue, and for each trigger of an event type that has more than one, which triggers the event
/// through the real endpoint or operator command, as the app's callers do, and finds exactly one new entry, with its
/// <c>EventType</c>, <c>OrganisationId</c>, <c>ActorUserId</c>, <c>EntityType</c>, <c>EntityId</c>,
/// <c>ParticipantId</c> and the values in its <c>Details</c>. Each task that writes an event adds its row here, and T24
/// completes the table. Rows act in seeded organisation A and add rows of their own to it, changing no seeded row, but
/// for a change to an organisation's own row, such as its guide prompts, or one whose values depend on every row of
/// the organisation's list, such as a new group's place among its groups, which a row makes in an organisation that it
/// adds, and for a change to every organisation, which a row makes on a database of its own (tests/README.md).
/// </summary>
[Collection<SqlServerCollection>]
public sealed class AuditEventsTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    : IClassFixture<Grow2NotesFactory>, IAsyncDisposable
{
    /// <summary>
    /// Each row: what triggers its event, through the real endpoint or operator command, which gives the entry that it
    /// must write. A row is named by its event type, followed, for an event type with more than one trigger, by " by "
    /// and the trigger.
    /// </summary>
    private static readonly Dictionary<string, Func<AuditEventsTests, Task<WrittenEvent>>> Rows = new()
    {
        [AuditEventTypes.User.Invited] = test => test.InviteUserAsync(),
        [AuditEventTypes.User.Updated] = test => test.RenameUserAsync(),
        [$"{AuditEventTypes.User.SetupLinkSent} by Resend invite"] = test => test.ResendInviteAsync(),
        [$"{AuditEventTypes.User.SetupLinkSent} by Reset sign-in"] = test => test.ResetSignInAsync(),
        [AuditEventTypes.User.Deactivated] = test => test.DeactivateUserAsync(),
        [AuditEventTypes.User.Reactivated] = test => test.ReactivateUserAsync(),
        [AuditEventTypes.Participant.Created] = test => test.AddParticipantAsync(),
        [AuditEventTypes.Participant.Updated] = test => test.CorrectParticipantAsync(),
        [AuditEventTypes.Participant.Archived] = test => test.ArchiveParticipantAsync(),
        [AuditEventTypes.Participant.Restored] = test => test.RestoreParticipantAsync(),
        [AuditEventTypes.Goal.Created] = test => test.AddGoalAsync(),
        [AuditEventTypes.Goal.Updated] = test => test.RewordGoalAsync(),
        [AuditEventTypes.Goal.Reordered] = test => test.ReorderGoalsAsync(),
        [AuditEventTypes.Goal.Archived] = test => test.ArchiveGoalAsync(),
        [AuditEventTypes.Goal.Restored] = test => test.RestoreGoalAsync(),
        [AuditEventTypes.CommonItem.Created] = test => test.AddCommonItemAsync(),
        [AuditEventTypes.CommonItem.Updated] = test => test.RewordCommonItemAsync(),
        [AuditEventTypes.CommonItem.Reordered] = test => test.ReorderCommonItemsAsync(),
        [AuditEventTypes.CommonItem.Archived] = test => test.ArchiveCommonItemAsync(),
        [AuditEventTypes.CommonItem.Restored] = test => test.RestoreCommonItemAsync(),
        [AuditEventTypes.CommonItemGroup.Created] = test => test.AddCommonItemGroupAsync(),
        [AuditEventTypes.GuidePrompts.Updated] = test => test.SaveGuidePromptsAsync(),
        [AuditEventTypes.Note.DraftStarted] = test => test.StartDraftAsync(),
        [AuditEventTypes.Admin.SignInReset] = test => test.RunResetSignInAsync(),
        [AuditEventTypes.Admin.SignOutAll] = test => test.RunSignOutAllAsync(),
    };

    /// <summary>
    /// The rows whose trigger changes every organisation, each with the name of the migrated database of its own, with
    /// nothing seeded, on which it runs, so that the one organisation it adds there is the only one.
    /// </summary>
    private static readonly Dictionary<string, string> DatabasesOfTheirOwn = new()
    {
        [AuditEventTypes.Admin.SignOutAll] = "AuditEventsSignOutAll",
    };

    private readonly SqlServerFixture sqlServer = sqlServer;
    private readonly SeededOrganisation a = sqlServer.Seeded.A;

    // The app on the database of the row's own, for a row in DatabasesOfTheirOwn.
    private WebApplicationFactory<Program>? appOnItsOwnDatabase;

    public static TheoryData<string> RowNames { get; } = new(Rows.Keys);

    // The app that the row acts through, and on whose database the test finds the entry.
    private WebApplicationFactory<Program> App => appOnItsOwnDatabase ?? factory;

    [Theory]
    [MemberData(nameof(RowNames))]
    public async Task Its_trigger_writes_exactly_one_entry_with_its_fields_and_values(string row)
    {
        if (DatabasesOfTheirOwn.TryGetValue(row, out var database))
        {
            appOnItsOwnDatabase = await OnADatabaseOfItsOwnAsync(database);
        }

        var lastEventId = await LastEventIdAsync();

        var expected = await Rows[row](this);

        var written = Assert.Single(await EventsAfterAsync(lastEventId));
        Assert.Equal(row.Split(' ')[0], expected.EventType);
        Assert.Equal(expected with { Details = null }, written with { Details = null });
        Assert.True(
            JsonNode.DeepEquals(Parse(expected.Details), Parse(written.Details)),
            $"Details: expected {expected.Details}, written {written.Details}.");
    }

    public ValueTask DisposeAsync() => appOnItsOwnDatabase?.DisposeAsync() ?? ValueTask.CompletedTask;

    private static JsonNode? Parse(string? details) => details is null ? null : JsonNode.Parse(details);

    /// <summary>
    /// <c>user.invited</c>: A's manager invites a worker with an address of the row's own with
    /// <c>POST /api/admin/users</c>, which records the new user as the entity, with no participant, and their address,
    /// name and role as <c>new</c> alone, as sent.
    /// </summary>
    private async Task<WrittenEvent> InviteUserAsync()
    {
        var email = $"avery.brooks.{Guid.NewGuid():N}@example.org";
        using var client = factory.CreateHttpsClient().SignInAs(a.ManagerId);
        using var response = await client.PostWithTokenAsync(
            "/api/admin/users",
            await client.FetchTokenAsync(),
            new { displayName = "Avery Brooks", email, role = "Worker" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var values = new JsonObject { ["email"] = email, ["displayName"] = "Avery Brooks", ["role"] = "Worker" };
        return new(
            AuditEventTypes.User.Invited,
            a.OrganisationId,
            a.ManagerId,
            AuditEntityTypes.User,
            (await StoredUserAsync(email)).Id,
            null,
            new JsonObject { ["new"] = values }.ToJsonString());
    }

    /// <summary>
    /// <c>user.updated</c>: A's manager renames an Active worker added for the row with
    /// <c>PUT /api/admin/users/{id}</c> and the worker's ETag, which records the worker as the entity, with no
    /// participant, and the old and new name alone, in <c>ChangeDetails.Of</c>'s shape, not the address or the role,
    /// sent as they were.
    /// </summary>
    private async Task<WrittenEvent> RenameUserAsync()
    {
        const string newName = "Sam Taylor-Jones";
        var worker = await AddWorkerAsync(UserStatus.Active);
        using var client = factory.CreateHttpsClient().SignInAs(a.ManagerId);
        using var response = await client.PutWithTokenAsync(
            $"/api/admin/users/{worker.Id}",
            await client.FetchTokenAsync(),
            new { displayName = newName, email = worker.Email, role = "Worker" },
            IfMatch.ETag(worker.ConcurrencyStamp!));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var details = new JsonObject
        {
            ["old"] = new JsonObject { ["displayName"] = PasswordSignIn.DisplayName },
            ["new"] = new JsonObject { ["displayName"] = newName },
        };
        return new(
            AuditEventTypes.User.Updated,
            a.OrganisationId,
            a.ManagerId,
            AuditEntityTypes.User,
            worker.Id,
            null,
            details.ToJsonString());
    }

    /// <summary>
    /// <c>user.setup_link_sent</c>, by Resend invite: A's manager resends the invite of a worker invited for the row
    /// through the invite core, which writes no event, with <c>POST /api/admin/users/{id}/setup-link</c> and the
    /// worker's ETag, which records the worker as the entity, with no participant and no <c>Details</c>, so no address
    /// (A28).
    /// </summary>
    private async Task<WrittenEvent> ResendInviteAsync()
    {
        var invited = await factory.InviteAsync(a);
        var user = await StoredUserAsync(invited.Email);
        using var client = factory.CreateHttpsClient().SignInAs(a.ManagerId);
        using var response = await client.PostWithTokenAsync(
            $"/api/admin/users/{user.Id}/setup-link",
            await client.FetchTokenAsync(),
            ifMatch: IfMatch.ETag(user.ConcurrencyStamp!));
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        return new(
            AuditEventTypes.User.SetupLinkSent,
            a.OrganisationId,
            a.ManagerId,
            AuditEntityTypes.User,
            user.Id,
            null,
            null);
    }

    /// <summary>
    /// <c>user.setup_link_sent</c>, by Reset sign-in: A's manager resets the sign-in of an Active worker added for the
    /// row with <c>POST /api/admin/users/{id}/setup-link</c> and the worker's ETag, which records the worker as the
    /// entity, with no participant and no <c>Details</c>, as a resend does.
    /// </summary>
    private async Task<WrittenEvent> ResetSignInAsync()
    {
        var worker = await AddWorkerAsync(UserStatus.Active);
        await PostAsync($"/api/admin/users/{worker.Id}/setup-link", IfMatch.ETag(worker.ConcurrencyStamp!));

        return new(
            AuditEventTypes.User.SetupLinkSent,
            a.OrganisationId,
            a.ManagerId,
            AuditEntityTypes.User,
            worker.Id,
            null,
            null);
    }

    /// <summary>
    /// <c>user.deactivated</c>: A's manager deactivates an Active worker added for the row with
    /// <c>POST /api/admin/users/{id}/deactivate</c> and the worker's ETag, which records the worker as the entity, with
    /// no participant and no <c>Details</c>, the event type saying what changed.
    /// </summary>
    private async Task<WrittenEvent> DeactivateUserAsync()
    {
        var worker = await AddWorkerAsync(UserStatus.Active);
        await PostAsync($"/api/admin/users/{worker.Id}/deactivate", IfMatch.ETag(worker.ConcurrencyStamp!));

        return new(
            AuditEventTypes.User.Deactivated,
            a.OrganisationId,
            a.ManagerId,
            AuditEntityTypes.User,
            worker.Id,
            null,
            null);
    }

    /// <summary>
    /// <c>user.reactivated</c>: A's manager reactivates a Deactivated worker added for the row with
    /// <c>POST /api/admin/users/{id}/reactivate</c> and the worker's ETag, which records the worker as the entity, with
    /// no participant and no <c>Details</c>, the event type saying what changed.
    /// </summary>
    private async Task<WrittenEvent> ReactivateUserAsync()
    {
        var worker = await AddWorkerAsync(UserStatus.Deactivated);
        await PostAsync($"/api/admin/users/{worker.Id}/reactivate", IfMatch.ETag(worker.ConcurrencyStamp!));

        return new(
            AuditEventTypes.User.Reactivated,
            a.OrganisationId,
            a.ManagerId,
            AuditEntityTypes.User,
            worker.Id,
            null,
            null);
    }

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

    /// <summary>
    /// <c>participant.updated</c>: A's manager corrects the family name and date of birth of a participant added for
    /// the row with <c>PUT /api/admin/participants/{id}</c>, which records the participant as the entity and the
    /// participant, and the old and new value of the two fields that changed alone, not the given name, sent as it was.
    /// </summary>
    private async Task<WrittenEvent> CorrectParticipantAsync()
    {
        var participant = await factory.Services.AddParticipantAsync(a);
        using var client = factory.CreateHttpsClient().SignInAs(a.ManagerId);
        using var response = await client.PutWithTokenAsync(
            $"/api/admin/participants/{participant.Id}",
            await client.FetchTokenAsync(),
            new { givenName = "Jane", familyName = " Smith", dateOfBirth = "1987-03-17" },
            participant.ETag);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var details = new JsonObject
        {
            ["old"] = new JsonObject { ["familyName"] = "Citizen", ["dateOfBirth"] = "1987-03-07" },
            ["new"] = new JsonObject { ["familyName"] = "Smith", ["dateOfBirth"] = "1987-03-17" },
        };
        return new(
            AuditEventTypes.Participant.Updated,
            a.OrganisationId,
            a.ManagerId,
            AuditEntityTypes.Participant,
            participant.Id,
            participant.Id,
            details.ToJsonString());
    }

    /// <summary>
    /// <c>participant.archived</c>: A's manager archives an active participant added for the row with
    /// <c>POST /api/admin/participants/{id}/archive</c>, which records the participant as the entity and the
    /// participant, and their status and the time they were archived at, before and after, the time as stored.
    /// </summary>
    private async Task<WrittenEvent> ArchiveParticipantAsync()
    {
        var participant = await factory.Services.AddParticipantAsync(a);
        await PostAsync($"/api/admin/participants/{participant.Id}/archive", participant.ETag);

        var details = new JsonObject
        {
            ["old"] = new JsonObject { ["status"] = "Active", ["archivedAtUtc"] = null },
            ["new"] = new JsonObject
            {
                ["status"] = "Archived", ["archivedAtUtc"] = await StoredArchivedAtUtcAsync(participant.Id),
            },
        };
        return new(
            AuditEventTypes.Participant.Archived,
            a.OrganisationId,
            a.ManagerId,
            AuditEntityTypes.Participant,
            participant.Id,
            participant.Id,
            details.ToJsonString());
    }

    /// <summary>
    /// <c>participant.restored</c>: A's manager restores an archived participant added for the row with
    /// <c>POST /api/admin/participants/{id}/restore</c>, which records the participant as the entity and the
    /// participant, and their status and the time they had been archived at, before and after, which is none.
    /// </summary>
    private async Task<WrittenEvent> RestoreParticipantAsync()
    {
        var participant = await factory.Services.AddParticipantAsync(a, archived: true);
        await PostAsync($"/api/admin/participants/{participant.Id}/restore", participant.ETag);

        var details = new JsonObject
        {
            ["old"] = new JsonObject { ["status"] = "Archived", ["archivedAtUtc"] = TestParticipants.ArchivedAtUtc },
            ["new"] = new JsonObject { ["status"] = "Active", ["archivedAtUtc"] = null },
        };
        return new(
            AuditEventTypes.Participant.Restored,
            a.OrganisationId,
            a.ManagerId,
            AuditEntityTypes.Participant,
            participant.Id,
            participant.Id,
            details.ToJsonString());
    }

    /// <summary>
    /// <c>goal.created</c>: A's manager adds a goal, typed over two lines, to a participant added for the row with
    /// <c>POST /api/admin/participants/{participantId}/goals</c>, which records the new goal as the entity and its
    /// participant as the participant, and the goal's values as <c>new</c> alone, named as design.md §6.6's list names
    /// them: its text as stored, on one line, its place at the end of a list that had no goal, and no time archived.
    /// </summary>
    private async Task<WrittenEvent> AddGoalAsync()
    {
        var participant = await factory.Services.AddParticipantAsync(a);
        using var client = factory.CreateHttpsClient().SignInAs(a.ManagerId);
        using var response = await client.PostWithTokenAsync(
            $"/api/admin/participants/{participant.Id}/goals",
            await client.FetchTokenAsync(),
            new { text = " Makes own\nbreakfast " });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var values = new JsonObject { ["text"] = "Makes own breakfast", ["sortOrder"] = 0, ["archivedAtUtc"] = null };
        return new(
            AuditEventTypes.Goal.Created,
            a.OrganisationId,
            a.ManagerId,
            AuditEntityTypes.Goal,
            await StoredGoalIdAsync(participant.Id),
            participant.Id,
            new JsonObject { ["new"] = values }.ToJsonString());
    }

    /// <summary>
    /// <c>goal.updated</c>: A's manager rewords a goal of a participant added for the row, typing it over two lines,
    /// with <c>PUT /api/admin/goals/{goalId}</c> and the goal's ETag, which records the goal as the entity and its
    /// participant as the participant, and the old and new text alone, the new as stored, on one line, not its place or
    /// time archived, which a reword leaves as they were.
    /// </summary>
    private async Task<WrittenEvent> RewordGoalAsync()
    {
        var participant = await factory.Services.AddParticipantAsync(a);
        var goal = await factory.Services.AddGoalAsync(a, participant.Id, new("Makes own breakfast", 3));
        using var client = factory.CreateHttpsClient().SignInAs(a.ManagerId);
        using var response = await client.PutWithTokenAsync(
            $"/api/admin/goals/{goal.Id}",
            await client.FetchTokenAsync(),
            new { text = " Makes own\nlunch " },
            goal.ETag);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var details = new JsonObject
        {
            ["old"] = new JsonObject { ["text"] = "Makes own breakfast" },
            ["new"] = new JsonObject { ["text"] = "Makes own lunch" },
        };
        return new(
            AuditEventTypes.Goal.Updated,
            a.OrganisationId,
            a.ManagerId,
            AuditEntityTypes.Goal,
            goal.Id,
            participant.Id,
            details.ToJsonString());
    }

    /// <summary>
    /// <c>goal.reordered</c>: A's manager moves the second of a participant's two active goals to the top with
    /// <c>PUT /api/admin/participants/{participantId}/goals/order</c>, which records no goal as the entity, as it is
    /// about the list, and the participant as the participant, and the active goals' IDs in their old and new order,
    /// the archived goal left out of both.
    /// </summary>
    private async Task<WrittenEvent> ReorderGoalsAsync()
    {
        var participant = await factory.Services.AddParticipantAsync(a);
        var goals = await factory.Services.AddGoalsAsync(
            a,
            participant.Id,
            new("Makes own breakfast", 0),
            new("Uses the washing machine", 1, ArchivedAtUtc: TestGoals.AddedAtUtc),
            new("Phones her sister", 2));
        var (breakfast, sister) = (goals[0], goals[2]);
        using var client = factory.CreateHttpsClient().SignInAs(a.ManagerId);
        using var response = await client.PutWithTokenAsync(
            $"/api/admin/participants/{participant.Id}/goals/order",
            await client.FetchTokenAsync(),
            new { goalIds = new[] { sister, breakfast } },
            ifMatch: null);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var details = new JsonObject
        {
            ["old"] = new JsonObject { ["order"] = new JsonArray(breakfast.ToString(), sister.ToString()) },
            ["new"] = new JsonObject { ["order"] = new JsonArray(sister.ToString(), breakfast.ToString()) },
        };
        return new(
            AuditEventTypes.Goal.Reordered,
            a.OrganisationId,
            a.ManagerId,
            AuditEntityTypes.Goal,
            null,
            participant.Id,
            details.ToJsonString());
    }

    /// <summary>
    /// <c>goal.archived</c>: A's manager archives an active goal of a participant added for the row with
    /// <c>POST /api/admin/goals/{goalId}/archive</c> and the goal's ETag, which records the goal as the entity and its
    /// participant as the participant, and the time it was archived at, before and after, the time as stored; not its
    /// <c>sortOrder</c>, which archiving leaves as it was.
    /// </summary>
    private async Task<WrittenEvent> ArchiveGoalAsync()
    {
        var participant = await factory.Services.AddParticipantAsync(a);
        var goal = await factory.Services.AddGoalAsync(a, participant.Id, new("Makes own breakfast", 3));
        await PostAsync($"/api/admin/goals/{goal.Id}/archive", goal.ETag);

        var details = new JsonObject
        {
            ["old"] = new JsonObject { ["archivedAtUtc"] = null },
            ["new"] = new JsonObject { ["archivedAtUtc"] = await StoredGoalArchivedAtUtcAsync(goal.Id) },
        };
        return new(
            AuditEventTypes.Goal.Archived,
            a.OrganisationId,
            a.ManagerId,
            AuditEntityTypes.Goal,
            goal.Id,
            participant.Id,
            details.ToJsonString());
    }

    /// <summary>
    /// <c>goal.restored</c>: A's manager restores an archived goal of a participant added for the row with
    /// <c>POST /api/admin/goals/{goalId}/restore</c> and the goal's ETag, which records the goal as the entity and its
    /// participant as the participant, and its place before and after: from where it was archived to one after the
    /// highest active <c>sortOrder</c>, and from the time it was archived at to none.
    /// </summary>
    private async Task<WrittenEvent> RestoreGoalAsync()
    {
        var participant = await factory.Services.AddParticipantAsync(a);
        await factory.Services.AddGoalAsync(a, participant.Id, new("Phones her sister", 4));
        var goal = await factory.Services.AddGoalAsync(
            a, participant.Id, new("Makes own breakfast", 1, ArchivedAtUtc: TestGoals.AddedAtUtc));
        await PostAsync($"/api/admin/goals/{goal.Id}/restore", goal.ETag);

        var details = new JsonObject
        {
            ["old"] = new JsonObject { ["sortOrder"] = 1, ["archivedAtUtc"] = TestGoals.AddedAtUtc },
            ["new"] = new JsonObject { ["sortOrder"] = 5, ["archivedAtUtc"] = null },
        };
        return new(
            AuditEventTypes.Goal.Restored,
            a.OrganisationId,
            a.ManagerId,
            AuditEntityTypes.Goal,
            goal.Id,
            participant.Id,
            details.ToJsonString());
    }

    /// <summary>
    /// <c>common_item.created</c>: A's manager adds an item, typed over two lines, to a group added for the row with
    /// <c>POST /api/admin/common-item-groups/{groupId}/items</c>, which records the new item as the entity, with no
    /// participant, and its group and values as <c>new</c> alone, named as design.md §6.6's list names them: its text
    /// as stored, on one line, its place at the end of a group that had no item, and no time archived.
    /// </summary>
    private async Task<WrittenEvent> AddCommonItemAsync()
    {
        var groupId = Assert.Single(
            await factory.Services.AddGroupsAsync(a.OrganisationId, new TestCommonItemGroup("Community outing", 0)));
        using var client = factory.CreateHttpsClient().SignInAs(a.ManagerId);
        using var response = await client.PostWithTokenAsync(
            $"/api/admin/common-item-groups/{groupId}/items",
            await client.FetchTokenAsync(),
            new { text = " Travelled by\nbus or train " });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var values = new JsonObject
        {
            ["groupId"] = groupId,
            ["text"] = "Travelled by bus or train",
            ["sortOrder"] = 0,
            ["archivedAtUtc"] = null,
        };
        return new(
            AuditEventTypes.CommonItem.Created,
            a.OrganisationId,
            a.ManagerId,
            AuditEntityTypes.CommonItem,
            await StoredItemIdAsync(groupId),
            null,
            new JsonObject { ["new"] = values }.ToJsonString());
    }

    /// <summary>
    /// <c>common_item.updated</c>: A's manager rewords an item of a group added for the row, typing the new text over
    /// two lines, with <c>PUT /api/admin/common-items/{id}</c> and the item's <c>ETag</c>, which records the item as
    /// the entity, with no participant, and the old and new text, as stored, alone: the reword keeps the item in its
    /// group, so no other value changes.
    /// </summary>
    private async Task<WrittenEvent> RewordCommonItemAsync()
    {
        var groupId = Assert.Single(
            await factory.Services.AddGroupsAsync(a.OrganisationId, new TestCommonItemGroup("Community outing", 0)));
        var item = await factory.Services.AddItemAsync(
            a.OrganisationId, groupId, new TestCommonItem("Travelled by bus or train", 3));
        using var client = factory.CreateHttpsClient().SignInAs(a.ManagerId);
        using var response = await client.PutWithTokenAsync(
            $"/api/admin/common-items/{item.Id}",
            await client.FetchTokenAsync(),
            new { text = " Travelled by\ntram " },
            item.ETag);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var details = new JsonObject
        {
            ["old"] = new JsonObject { ["text"] = "Travelled by bus or train" },
            ["new"] = new JsonObject { ["text"] = "Travelled by tram" },
        };
        return new(
            AuditEventTypes.CommonItem.Updated,
            a.OrganisationId,
            a.ManagerId,
            AuditEntityTypes.CommonItem,
            item.Id,
            null,
            details.ToJsonString());
    }

    /// <summary>
    /// <c>common_item.reordered</c>: A's manager moves the second of the two active items of a group added for the row
    /// to the top with <c>PUT /api/admin/common-item-groups/{groupId}/items/order</c>, which records no item as the
    /// entity, as it is about the list, and no participant, and the active items' IDs in their old and new order, the
    /// archived item left out of both, with the group's ID beside them (S01.03.03's Note).
    /// </summary>
    private async Task<WrittenEvent> ReorderCommonItemsAsync()
    {
        var groupId = Assert.Single(
            await factory.Services.AddGroupsAsync(a.OrganisationId, new TestCommonItemGroup("Community outing", 0)));
        var items = await factory.Services.AddItemsAsync(
            a.OrganisationId,
            groupId,
            new("Travelled by bus or train", 0),
            new("Packed a day bag", 1, ArchivedAtUtc: TestCommonItems.AddedAtUtc),
            new("Bought lunch", 2));
        var (bus, lunch) = (items[0], items[2]);
        using var client = factory.CreateHttpsClient().SignInAs(a.ManagerId);
        using var response = await client.PutWithTokenAsync(
            $"/api/admin/common-item-groups/{groupId}/items/order",
            await client.FetchTokenAsync(),
            new { commonItemIds = new[] { lunch, bus } },
            ifMatch: null);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var details = new JsonObject
        {
            ["old"] = new JsonObject { ["order"] = new JsonArray(bus.ToString(), lunch.ToString()) },
            ["new"] = new JsonObject { ["order"] = new JsonArray(lunch.ToString(), bus.ToString()) },
            ["groupId"] = groupId,
        };
        return new(
            AuditEventTypes.CommonItem.Reordered,
            a.OrganisationId,
            a.ManagerId,
            AuditEntityTypes.CommonItem,
            null,
            null,
            details.ToJsonString());
    }

    /// <summary>
    /// <c>common_item.archived</c>: A's manager archives an active item of a group added for the row with
    /// <c>POST /api/admin/common-items/{id}/archive</c> and the item's <c>ETag</c>, which records the item as the
    /// entity, with no participant, and the time it was archived at, before and after, the time as stored; not its
    /// <c>sortOrder</c>, which archiving leaves as it was.
    /// </summary>
    private async Task<WrittenEvent> ArchiveCommonItemAsync()
    {
        var groupId = Assert.Single(
            await factory.Services.AddGroupsAsync(a.OrganisationId, new TestCommonItemGroup("Community outing", 0)));
        var item = await factory.Services.AddItemAsync(
            a.OrganisationId, groupId, new TestCommonItem("Travelled by bus or train", 3));
        await PostAsync($"/api/admin/common-items/{item.Id}/archive", item.ETag);

        var details = new JsonObject
        {
            ["old"] = new JsonObject { ["archivedAtUtc"] = null },
            ["new"] = new JsonObject { ["archivedAtUtc"] = await StoredItemArchivedAtUtcAsync(item.Id) },
        };
        return new(
            AuditEventTypes.CommonItem.Archived,
            a.OrganisationId,
            a.ManagerId,
            AuditEntityTypes.CommonItem,
            item.Id,
            null,
            details.ToJsonString());
    }

    /// <summary>
    /// <c>common_item.restored</c>: A's manager restores an archived item of a group added for the row with
    /// <c>POST /api/admin/common-items/{id}/restore</c> and the item's <c>ETag</c>, which records the item as the
    /// entity, with no participant, and its place before and after: from where it was archived to one after the
    /// highest active <c>sortOrder</c> of its group, and from the time it was archived at to none.
    /// </summary>
    private async Task<WrittenEvent> RestoreCommonItemAsync()
    {
        var groupId = Assert.Single(
            await factory.Services.AddGroupsAsync(a.OrganisationId, new TestCommonItemGroup("Community outing", 0)));
        await factory.Services.AddItemAsync(a.OrganisationId, groupId, new TestCommonItem("Bought lunch", 4));
        var item = await factory.Services.AddItemAsync(
            a.OrganisationId,
            groupId,
            new TestCommonItem("Travelled by bus or train", 1, ArchivedAtUtc: TestCommonItems.AddedAtUtc));
        await PostAsync($"/api/admin/common-items/{item.Id}/restore", item.ETag);

        var details = new JsonObject
        {
            ["old"] = new JsonObject { ["sortOrder"] = 1, ["archivedAtUtc"] = TestCommonItems.AddedAtUtc },
            ["new"] = new JsonObject { ["sortOrder"] = 5, ["archivedAtUtc"] = null },
        };
        return new(
            AuditEventTypes.CommonItem.Restored,
            a.OrganisationId,
            a.ManagerId,
            AuditEntityTypes.CommonItem,
            item.Id,
            null,
            details.ToJsonString());
    }

    /// <summary>
    /// <c>common_item_group.created</c>: the manager of an organisation that the row adds, whose groups are Every note
    /// alone, so that the new group's place is known, adds a group, typed over two lines, with
    /// <c>POST /api/admin/common-item-groups</c>, which records the new group as the entity, with no participant, and
    /// its values as <c>new</c> alone, named as design.md §6.6's list names them: its name as stored, on one line, its
    /// place, the first after Every note, and no time archived.
    /// </summary>
    private async Task<WrittenEvent> AddCommonItemGroupAsync()
    {
        var (organisationId, managerId) = await AddOrganisationAsync();
        await AddEveryNoteAsync(organisationId);
        using var client = factory.CreateHttpsClient().SignInAs(managerId);
        using var response = await client.PostWithTokenAsync(
            "/api/admin/common-item-groups", await client.FetchTokenAsync(), new { name = " Community\nouting " });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var added = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        var values = new JsonObject { ["name"] = "Community outing", ["sortOrder"] = 0, ["archivedAtUtc"] = null };
        return new(
            AuditEventTypes.CommonItemGroup.Created,
            organisationId,
            managerId,
            AuditEntityTypes.CommonItemGroup,
            added.GetProperty("id").GetGuid(),
            null,
            new JsonObject { ["new"] = values }.ToJsonString());
    }

    /// <summary>
    /// <c>guide_prompts.updated</c>: the manager of an organisation that the row adds, so that no seeded row changes,
    /// loads its guide prompts with <c>GET /api/admin/guide-prompts</c> and saves new ones with <c>PUT</c> and the
    /// <c>ETag</c> loaded, which records the organisation as the entity, with no participant, and the text before and
    /// after.
    /// </summary>
    private async Task<WrittenEvent> SaveGuidePromptsAsync()
    {
        const string path = "/api/admin/guide-prompts";
        const string oldText = "What did they enjoy today?";
        const string newText = "What did they enjoy today?\nWho did they see?";
        var (organisationId, managerId) = await AddOrganisationAsync(oldText);
        using var client = factory.CreateHttpsClient().SignInAs(managerId);
        string etag;
        using (var loaded = await client.GetAsync(path, TestContext.Current.CancellationToken))
        {
            Assert.Equal(HttpStatusCode.OK, loaded.StatusCode);
            Assert.NotNull(loaded.Headers.ETag);
            etag = loaded.Headers.ETag.ToString();
        }

        using var saved = await client.PutWithTokenAsync(
            path, await client.FetchTokenAsync(), new { text = newText }, etag);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);

        var details = new JsonObject
        {
            ["old"] = new JsonObject { ["text"] = oldText },
            ["new"] = new JsonObject { ["text"] = newText },
        };
        return new(
            AuditEventTypes.GuidePrompts.Updated,
            organisationId,
            managerId,
            AuditEntityTypes.Organisation,
            organisationId,
            null,
            details.ToJsonString());
    }

    /// <summary>
    /// <c>note.draft_started</c>: A's worker makes the first change to today's note of a participant added for the row,
    /// a goal ticked and a sentence written, with <c>PUT {base}/draft</c> and the template's lists' version, which
    /// records the new note as the entity and its participant as the participant, and no <c>Details</c>, so no note
    /// text or goal wording (design.md §5.3, §9.5).
    /// </summary>
    private async Task<WrittenEvent> StartDraftAsync()
    {
        var participant = await factory.Services.AddParticipantAsync(a);
        var goal = await factory.Services.AddGoalAsync(a, participant.Id, new("Makes own breakfast", 0));
        var today = factory.Services.GetRequiredService<MelbourneClock>().Today();
        using var client = factory.CreateHttpsClient().SignInAs(a.WorkerId);
        using var response = await client.PutWithTokenAsync(
            string.Create(
                CultureInfo.InvariantCulture, $"/api/participants/{participant.Id}/notes/{today:yyyy-MM-dd}/draft"),
            await client.FetchTokenAsync(),
            new
            {
                clientId = Guid.NewGuid(),
                seq = 1,
                listsVersion = ListsVersion.Of([new(goal.Id, "Makes own breakfast", IsTicked: false)]),
                narrative = "Jane made toast.",
                tickedGoalIds = new[] { goal.Id },
            },
            ifMatch: null);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return new(
            AuditEventTypes.Note.DraftStarted,
            a.OrganisationId,
            a.WorkerId,
            AuditEntityTypes.Note,
            await StoredNoteIdAsync(participant.Id),
            participant.Id,
            null);
    }

    /// <summary>
    /// <c>admin.signin_reset</c>: the operator runs <c>admin reset-signin</c> with the address of an Active worker
    /// added for the row, which records the worker as the entity, with no actor, as the operator is no user, no
    /// participant and no <c>Details</c>, so no address (A28).
    /// </summary>
    private async Task<WrittenEvent> RunResetSignInAsync()
    {
        var worker = await AddWorkerAsync(UserStatus.Active);

        var run = await factory.RunAdminCommandAsync(["reset-signin", worker.Email!]);

        Assert.Equal((0, ""), (run.ExitCode, run.Error));
        return new(
            AuditEventTypes.Admin.SignInReset,
            a.OrganisationId,
            null,
            AuditEntityTypes.User,
            worker.Id,
            null,
            null);
    }

    /// <summary>
    /// <c>admin.signout_all</c>: the operator runs <c>admin signout-all</c>, on a database of the row's own, where an
    /// organisation that the row adds is the only one, which records that organisation as the entity, with no actor,
    /// no participant and no <c>Details</c>. It writes one entry for each organisation and gives every user a new
    /// security stamp, the seeded users' too, so it cannot run on the collection's database (tests/README.md).
    /// </summary>
    private async Task<WrittenEvent> RunSignOutAllAsync()
    {
        var (organisationId, _) = await AddOrganisationAsync();

        var run = await App.RunAdminCommandAsync(["signout-all"]);

        Assert.Equal((0, "", ""), (run.ExitCode, run.Output, run.Error));
        return new(
            AuditEventTypes.Admin.SignOutAll,
            organisationId,
            null,
            AuditEntityTypes.Organisation,
            organisationId,
            null,
            null);
    }

    // An app on a migrated database of the row's own, with nothing seeded, named database.
    private async Task<WebApplicationFactory<Program>> OnADatabaseOfItsOwnAsync(string database)
    {
        var connectionString = await sqlServer.CreateDatabaseAsync(database, TestContext.Current.CancellationToken);
        return factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration(configuration =>
            configuration.AddInMemoryCollection([new("ConnectionStrings:Grow2Notes", connectionString)])));
    }

    // An organisation of the row's own, on the database of the app that the row acts through, with any guide prompts
    // given and one Active manager with an address of their own, as setup leaves them apart from the passkey or
    // password, which the test-only sign-in does not need. An organisation is the tenant, not tenant-owned, and
    // AspNetUsers is not filtered, so this needs no tenant.
    private async Task<(Guid OrganisationId, Guid ManagerId)> AddOrganisationAsync(string guidePrompts = "")
    {
        var addedAtUtc = new DateTime(2026, 10, 18, 0, 0, 0, DateTimeKind.Utc);
        await using var scope = App.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        var organisation = new Organisation
        {
            Name = "Audit events test",
            GuidePrompts = guidePrompts,
            CreatedAtUtc = addedAtUtc,
        };
        db.Organisations.Add(organisation);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var email = $"{Guid.NewGuid():N}@example.org";
        var manager = new ApplicationUser
        {
            OrganisationId = organisation.Id,
            DisplayName = "Quinn Harper",
            Role = UserRole.Manager,
            Status = UserStatus.Active,
            InvitedAtUtc = addedAtUtc,
            ActivatedAtUtc = addedAtUtc,
            UserName = email,
            Email = email,
            EmailConfirmed = true,
        };
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var created = await users.CreateAsync(manager);
        Assert.True(created.Succeeded, string.Join(" ", created.Errors.Select(error => error.Code)));
        return (organisation.Id, manager.Id);
    }

    // The Every note group of an organisation that a row adds, as admin bootstrap adds it (design.md §7.4), saved under
    // the organisation's tenant, as a tenant-owned row is.
    private async Task AddEveryNoteAsync(Guid organisationId)
    {
        await using var scope = App.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        db.CommonItemGroups.Add(CommonItemGroup.EveryNote(organisationId, TestCommonItems.AddedAtUtc));
        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(organisationId))
        {
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
    }

    // The user who has the address, as Identity stores them.
    private async Task<ApplicationUser> StoredUserAsync(string email)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByEmailAsync(email);
        Assert.NotNull(user);
        return user;
    }

    // A worker added to A with a password and a code, as setup leaves one (PasswordSignIn), in status, as Identity
    // stores them, which writes no event.
    private async Task<ApplicationUser> AddWorkerAsync(UserStatus status) =>
        await StoredUserAsync((await factory.AddPasswordUserAsync(a, status)).Email);

    // A change with no body by A's manager, such as an archive or a deactivation, with the ETag of what it changes as
    // If-Match, which answers 204.
    private async Task PostAsync(string path, string ifMatch)
    {
        using var client = factory.CreateHttpsClient().SignInAs(a.ManagerId);
        using var response = await client.PostWithTokenAsync(path, await client.FetchTokenAsync(), ifMatch: ifMatch);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    // The time that the participant was archived at, as their row holds it, through the app's context under A's tenant.
    private async Task<DateTime?> StoredArchivedAtUtcAsync(Guid participantId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(a.OrganisationId))
        {
            return await db.Participants.Where(p => p.Id == participantId)
                .Select(p => p.ArchivedAtUtc)
                .SingleAsync(TestContext.Current.CancellationToken);
        }
    }

    // The time that the goal was archived at, as its row holds it, through the app's context under A's tenant.
    private async Task<DateTime?> StoredGoalArchivedAtUtcAsync(Guid goalId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(a.OrganisationId))
        {
            return await db.Goals.Where(g => g.Id == goalId)
                .Select(g => g.ArchivedAtUtc)
                .SingleAsync(TestContext.Current.CancellationToken);
        }
    }

    // The ID of the one note of a participant added for a row, through the app's context under A's tenant.
    private async Task<Guid> StoredNoteIdAsync(Guid participantId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(a.OrganisationId))
        {
            return await db.Notes.Where(n => n.ParticipantId == participantId)
                .Select(n => n.Id)
                .SingleAsync(TestContext.Current.CancellationToken);
        }
    }

    // The ID of the one goal of a participant added for a row, through the app's context under A's tenant.
    private async Task<Guid> StoredGoalIdAsync(Guid participantId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(a.OrganisationId))
        {
            return await db.Goals.Where(g => g.ParticipantId == participantId)
                .Select(g => g.Id)
                .SingleAsync(TestContext.Current.CancellationToken);
        }
    }

    // The time that the item was archived at, as its row holds it, through the app's context under A's tenant.
    private async Task<DateTime?> StoredItemArchivedAtUtcAsync(Guid itemId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(a.OrganisationId))
        {
            return await db.CommonItems.Where(item => item.Id == itemId)
                .Select(item => item.ArchivedAtUtc)
                .SingleAsync(TestContext.Current.CancellationToken);
        }
    }

    // The ID of the one item of a group added for a row, through the app's context under A's tenant.
    private async Task<Guid> StoredItemIdAsync(Guid groupId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(a.OrganisationId))
        {
            return await db.CommonItems.Where(item => item.GroupId == groupId)
                .Select(item => item.Id)
                .SingleAsync(TestContext.Current.CancellationToken);
        }
    }

    // The ID of the last event written on the row's database, by any test. The tests of the collection run one at a
    // time, so the events after it are the row's alone.
    private async Task<long> LastEventIdAsync()
    {
        await using var scope = App.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Database
            .SqlQuery<long>($"SELECT COALESCE(MAX([Id]), 0) AS [Value] FROM [AuditEvent]")
            .SingleAsync(TestContext.Current.CancellationToken);
    }

    // Read in SQL, so the query filter, which would need a tenant, does not decide what is seen.
    private async Task<List<WrittenEvent>> EventsAfterAsync(long eventId)
    {
        await using var scope = App.Services.CreateAsyncScope();
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
