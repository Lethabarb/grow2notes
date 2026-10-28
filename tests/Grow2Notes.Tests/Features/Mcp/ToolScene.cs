using System.Text.Json;
using System.Text.Json.Nodes;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Grow2Notes.Tests.Features.Mcp;

/// <summary>Who calls a tool in a test of every tool: a person of the first organisation, or of another.</summary>
public enum ToolCaller
{
    Manager,
    Support,
    ManagerOfAnotherOrganisation,
}

/// <summary>A goal of <see cref="ToolScene.Participant"/>, with its <c>ETag</c>.</summary>
internal sealed record SceneGoal(Guid Id, string Text, bool Archived, string ETag);

/// <summary>A common item group of the scene's first organisation, with its <c>ETag</c>.</summary>
internal sealed record SceneGroup(Guid Id, string Name, bool Archived, string ETag);

/// <summary>A common item of the scene's first organisation, with its <c>ETag</c>.</summary>
internal sealed record SceneItem(Guid Id, string Text, bool Archived, string ETag);

/// <summary>A user account of the scene's first organisation, with the address it signs in with.</summary>
internal sealed record SceneUser(Guid Id, string Email, string DisplayName);

/// <summary>
/// The world that the tests of the MCP tools share, in the database of an <see cref="AssistantFlow"/>: a first
/// organisation with a manager, a Support person, a participant whose names and goals are unique to the scene, an
/// archived participant with no goals, another with an active goal, and that participant's date of birth; common item
/// groups, items and users whose names hold the scene's token; and a second organisation with a manager and a
/// participant. Every later tool joins the matrix, the contract rules and the canary through this scene, which is also
/// where they find the organisation to add what they need to (<see cref="Organisation"/>). A tool that changes a row
/// changes one that it adds (<see cref="AddParticipantAsync"/>, <see cref="AddGoalAsync"/>,
/// <see cref="AddGroupAsync"/>, <see cref="AddItemAsync"/>), never the rows of the scene, which the error cases of
/// every tool share.
/// </summary>
internal sealed class ToolScene
{
    // A date that is in no other test's data, and in no number the scene makes up.
    private static readonly DateOnly BornOn = new(1991, 6, 23);

    private static readonly DateTime FirstGoalAtUtc = new(2026, 10, 2, 3, 4, 5, 678, DateTimeKind.Utc);

    private readonly AssistantFlow flow;

    private ToolScene(
        AssistantFlow flow,
        SeededOrganisation organisation,
        SeededOrganisation other,
        string token,
        TestParticipant participant,
        IReadOnlyList<SceneGoal> goals,
        TestParticipant archived,
        TestParticipant archivedParticipant,
        SceneGoal archivedGoal,
        SceneGroup group,
        IReadOnlyList<SceneItem> items,
        SceneGroup archivedGroup,
        SceneItem archivedGroupItem,
        SceneUser invitedUser,
        SceneUser deactivatedUser)
    {
        this.flow = flow;
        Organisation = organisation;
        OtherOrganisation = other;
        Token = token;
        Participant = participant;
        Goals = goals;
        Archived = archived;
        ArchivedParticipant = archivedParticipant;
        ArchivedGoal = archivedGoal;
        Group = group;
        Items = items;
        ArchivedGroup = archivedGroup;
        ArchivedGroupItem = archivedGroupItem;
        InvitedUser = invitedUser;
        DeactivatedUser = deactivatedUser;
    }

    /// <summary>The first organisation: the one that every tool is called in, and the one that tests add to.</summary>
    public SeededOrganisation Organisation { get; }

    /// <summary>The second organisation, whose manager must see nothing of the first.</summary>
    public SeededOrganisation OtherOrganisation { get; }

    /// <summary>A hex token in every name and goal of the scene, which no other test's data holds.</summary>
    public string Token { get; }

    public string GivenName => $"Gwen{Token}";

    public string FamilyName => $"Vale{Token}";

    /// <summary>The active participant with the unique names, born on <see cref="DateOfBirth"/>.</summary>
    public TestParticipant Participant { get; }

    public DateOnly DateOfBirth => BornOn;

    /// <summary>
    /// The goals of <see cref="Participant"/> in the order that <c>get_participant</c> lists them: the active ones
    /// (with a gap in their order and a tie), then the archived ones, one of which sorts between the active ones.
    /// </summary>
    public IReadOnlyList<SceneGoal> Goals { get; }

    /// <summary>An archived participant, with no goals and a family name of <see cref="ArchivedFamilyName"/>.</summary>
    public TestParticipant Archived { get; }

    public string ArchivedFamilyName => $"Dusk{Token}";

    /// <summary>
    /// An archived participant who has a goal, <see cref="ArchivedGoal"/>: the participant of a change that is refused
    /// because they are archived. Their names are <c>Ida{token} Fern{token}</c>.
    /// </summary>
    public TestParticipant ArchivedParticipant { get; }

    /// <summary>An active goal of <see cref="ArchivedParticipant"/>, <c>Goal F {token}</c>.</summary>
    public SceneGoal ArchivedGoal { get; }

    /// <summary>The Every note group of the first organisation, which has no items.</summary>
    public Guid EveryNoteGroupId => Organisation.EveryNoteGroupId;

    /// <summary>An active group, <c>Group A {token}</c>, whose items are <see cref="Items"/>.</summary>
    public SceneGroup Group { get; }

    /// <summary>
    /// The items of <see cref="Group"/> in the order that <c>list_common_items</c> lists them: the active
    /// <c>Item A {token}</c> and <c>Item B {token}</c>, then the archived <c>Item C {token}</c>, which the app's order
    /// puts first.
    /// </summary>
    public IReadOnlyList<SceneItem> Items { get; }

    /// <summary>
    /// An archived group, <c>Group Z {token}</c>, which the app's order puts before <see cref="Group"/>, with an
    /// active item, <see cref="ArchivedGroupItem"/>.
    /// </summary>
    public SceneGroup ArchivedGroup { get; }

    /// <summary>The active item <c>Item Z {token}</c> of <see cref="ArchivedGroup"/>.</summary>
    public SceneItem ArchivedGroupItem { get; }

    /// <summary>An invited worker, <c>Una{token} Kerr</c>, with an example.test address.</summary>
    public SceneUser InvitedUser { get; }

    /// <summary>A deactivated worker, <c>Vic{token} Lane</c>, with an example.test address.</summary>
    public SceneUser DeactivatedUser { get; }

    public static async Task<ToolScene> CreateAsync(AssistantFlow flow)
    {
        var services = flow.App.Services;
        var token = NewHex();
        var organisation = await services.AddOrganisationAsync();
        var other = await services.AddOrganisationAsync();

        var participant = await services.AddParticipantAsync(
            organisation, givenName: $"Gwen{token}", familyName: $"Vale{token}", dateOfBirth: BornOn);
        var archived = await services.AddParticipantAsync(
            organisation, archived: true, givenName: $"Ned{token}", familyName: $"Dusk{token}");
        var archivedWithGoal = await services.AddParticipantAsync(
            organisation, archived: true, givenName: $"Ida{token}", familyName: $"Fern{token}");

        // Added in an order that is not the list's, so that only the ordering can put them right.
        var archivedLate = await SeedGoalAsync(services, organisation, participant.Id, $"Goal E {token}", 15, 0, true);
        var activeTied = await SeedGoalAsync(services, organisation, participant.Id, $"Goal C {token}", 30, 1, false);
        var activeFirst = await SeedGoalAsync(services, organisation, participant.Id, $"Goal A {token}", 10, 0, false);
        var archivedEarly = await SeedGoalAsync(services, organisation, participant.Id, $"Goal D {token}", 5, 0, true);
        var activeSecond = await SeedGoalAsync(services, organisation, participant.Id, $"Goal B {token}", 30, 0, false);

        var archivedGoal = await SeedGoalAsync(
            services, organisation, archivedWithGoal.Id, $"Goal F {token}", 10, 0, false);

        // The archived group and item come first in the app's order, so that only the tools' own order puts them last.
        var group = await SeedGroupAsync(services, organisation, $"Group A {token}", 10, false);
        var itemA = await SeedItemAsync(services, organisation, group.Id, $"Item A {token}", 10, false);
        var itemB = await SeedItemAsync(services, organisation, group.Id, $"Item B {token}", 20, false);
        var itemC = await SeedItemAsync(services, organisation, group.Id, $"Item C {token}", 5, true);
        var archivedGroup = await SeedGroupAsync(services, organisation, $"Group Z {token}", 5, true);
        var archivedGroupItem = await SeedItemAsync(
            services, organisation, archivedGroup.Id, $"Item Z {token}", 10, false);

        var invited = await SeedUserAsync(
            services, organisation, $"Una{token} Kerr", $"una.{token}@example.test", UserStatus.Invited);
        var deactivated = await SeedUserAsync(
            services, organisation, $"Vic{token} Lane", $"vic.{token}@example.test", UserStatus.Deactivated);

        return new(
            flow,
            organisation,
            other,
            token,
            participant,
            [activeFirst, activeSecond, activeTied, archivedEarly, archivedLate],
            archived,
            archivedWithGoal,
            archivedGoal,
            group,
            [itemA, itemB, itemC],
            archivedGroup,
            archivedGroupItem,
            invited,
            deactivated);
    }

    /// <summary>
    /// The value that a change sends as <c>expectedVersion</c> for a row with <paramref name="etag"/>: the token, as
    /// the look-ups give it, without the quotes of an <c>ETag</c>.
    /// </summary>
    public static string ExpectedVersion(string etag) => etag.Trim('"');

    /// <summary>
    /// Adds an active participant of the first organisation, with names that are made up each time and do not hold
    /// <see cref="Token"/>, so that no search or count of the scene finds them. Nothing is audited.
    /// </summary>
    public Task<TestParticipant> AddParticipantAsync()
    {
        var unique = NewHex();
        return flow.App.Services.AddParticipantAsync(
            Organisation, givenName: $"Zed{unique}", familyName: $"Mott{unique}");
    }

    /// <summary>
    /// Adds a goal to <paramref name="participantId"/>, a participant of the first organisation whom the test added,
    /// with wording that is made up each time and does not hold <see cref="Token"/>; archived or active.
    /// </summary>
    public Task<SceneGoal> AddGoalAsync(Guid participantId, bool archived) =>
        SeedGoalAsync(flow.App.Services, Organisation, participantId, $"Goal {NewHex()}", 0, 0, archived);

    /// <summary>
    /// Adds a group to the first organisation, after the scene's groups, with a name that is made up each time and does
    /// not hold <see cref="Token"/>; archived or active, and empty.
    /// </summary>
    public Task<SceneGroup> AddGroupAsync(bool archived = false) =>
        SeedGroupAsync(flow.App.Services, Organisation, $"Group {NewHex()}", 100, archived);

    /// <summary>
    /// Adds an item to <paramref name="groupId"/>, a group of the first organisation, after its other items, with
    /// wording that is made up each time and does not hold <see cref="Token"/>; archived or active.
    /// </summary>
    public Task<SceneItem> AddItemAsync(Guid groupId, bool archived = false) =>
        SeedItemAsync(flow.App.Services, Organisation, groupId, $"Item {NewHex()}", 100, archived);

    /// <summary>
    /// The version of the first organisation's guide prompts as the tools give it: the token of the organisation row,
    /// read now.
    /// </summary>
    public async Task<string> GuidePromptsVersionAsync()
    {
        await using var scope = flow.App.Services.CreateAsyncScope();
        var organisation = await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Organisations
            .AsNoTracking()
            .SingleAsync(row => row.Id == Organisation.OrganisationId, TestContext.Current.CancellationToken);
        return RowVersionToken.Of(organisation);
    }

    /// <summary>
    /// Stores <paramref name="text"/> as the first organisation's guide prompts, as it is given, without the app's
    /// checks and with no audit event, so that a test starts from prompts of any shape.
    /// </summary>
    public async Task SetGuidePromptsAsync(string text)
    {
        await using var scope = flow.App.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Organisations
            .Where(row => row.Id == Organisation.OrganisationId)
            .ExecuteUpdateAsync(
                update => update.SetProperty(row => row.GuidePrompts, text), TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Adds <paramref name="count"/> active goals to <paramref name="participantId"/> as <see cref="AddGoalAsync"/>
    /// does, in the order given: the first is first.
    /// </summary>
    public async Task<IReadOnlyList<SceneGoal>> AddGoalsAsync(Guid participantId, int count)
    {
        var unique = NewHex();
        var goals = new List<SceneGoal>();
        for (var index = 0; index < count; index++)
        {
            goals.Add(await SeedGoalAsync(
                flow.App.Services, Organisation, participantId, $"Goal {unique} {index}", index, index, false));
        }

        return goals;
    }

    /// <summary>The person of <paramref name="caller"/>.</summary>
    public Guid UserOf(ToolCaller caller) => caller switch
    {
        ToolCaller.Manager => Organisation.ManagerId,
        ToolCaller.Support => Organisation.SupportId,
        ToolCaller.ManagerOfAnotherOrganisation => OtherOrganisation.ManagerId,
    };

    /// <summary>The person of <paramref name="caller"/> signs in and connects an assistant: its access token.</summary>
    public async Task<string> ConnectAsync(ToolCaller caller)
    {
        using var browser = await flow.SignInAsync(UserOf(caller));
        return (await flow.ConnectAsync(browser)).AccessToken;
    }

    /// <summary>
    /// Calls <paramref name="tool"/> with <paramref name="arguments"/> as <paramref name="client"/> does.
    /// </summary>
    public static ValueTask<CallToolResult> CallAsync(McpClient client, string tool, JsonObject arguments) =>
        client.CallToolAsync(
            tool,
            arguments.ToDictionary(
                argument => argument.Key,
                argument => argument.Value is null
                    ? null
                    : (object?)JsonSerializer.Deserialize<JsonElement>(argument.Value.ToJsonString())),
            cancellationToken: TestContext.Current.CancellationToken);

    private static string NewHex() => Guid.NewGuid().ToString("N")[..12];

    private static async Task<SceneGroup> SeedGroupAsync(
        IServiceProvider services, SeededOrganisation organisation, string name, int sortOrder, bool archived)
    {
        var added = await services.AddGroupAsync(
            organisation.OrganisationId,
            new(name, sortOrder, ArchivedAtUtc: archived ? TestCommonItems.AddedAtUtc : null));
        return new(added.Id, name, archived, added.ETag);
    }

    private static async Task<SceneItem> SeedItemAsync(
        IServiceProvider services,
        SeededOrganisation organisation,
        Guid groupId,
        string text,
        int sortOrder,
        bool archived)
    {
        var added = await services.AddItemAsync(
            organisation.OrganisationId,
            groupId,
            new(text, sortOrder, ArchivedAtUtc: archived ? TestCommonItems.AddedAtUtc : null));
        return new(added.Id, text, archived, added.ETag);
    }

    private static async Task<SceneUser> SeedUserAsync(
        IServiceProvider services,
        SeededOrganisation organisation,
        string displayName,
        string email,
        UserStatus status)
    {
        var id = await services.AddUserAsync(organisation, UserRole.Worker, displayName, status, email);
        return new(id, email, displayName);
    }

    private static async Task<SceneGoal> SeedGoalAsync(
        IServiceProvider services,
        SeededOrganisation organisation,
        Guid participantId,
        string text,
        int sortOrder,
        int addedMinutesAfterFirst,
        bool archived)
    {
        var added = await services.AddGoalAsync(
            organisation,
            participantId,
            new(
                text,
                sortOrder,
                FirstGoalAtUtc.AddMinutes(addedMinutesAfterFirst),
                ArchivedAtUtc: archived ? FirstGoalAtUtc : null));
        return new(added.Id, text, archived, added.ETag);
    }
}
