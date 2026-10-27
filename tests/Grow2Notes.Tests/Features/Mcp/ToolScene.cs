using System.Text.Json;
using System.Text.Json.Nodes;
using Grow2Notes.Tests.Fixtures;
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

/// <summary>
/// The world that the tests of the MCP tools share, in the database of an <see cref="AssistantFlow"/>: a first
/// organisation with a manager, a Support person, a participant whose names and goals are unique to the scene, an
/// archived participant and that participant's date of birth; and a second organisation with a manager and a
/// participant. Every later tool joins the matrix, the contract rules and the canary through this scene, which is also
/// where they find the organisation to add what they need to (<see cref="Organisation"/>).
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
        TestParticipant archived)
    {
        this.flow = flow;
        Organisation = organisation;
        OtherOrganisation = other;
        Token = token;
        Participant = participant;
        Goals = goals;
        Archived = archived;
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

    public static async Task<ToolScene> CreateAsync(AssistantFlow flow)
    {
        var services = flow.App.Services;
        var token = Guid.NewGuid().ToString("N")[..12];
        var organisation = await services.AddOrganisationAsync();
        var other = await services.AddOrganisationAsync();

        var participant = await services.AddParticipantAsync(
            organisation, givenName: $"Gwen{token}", familyName: $"Vale{token}", dateOfBirth: BornOn);
        var archived = await services.AddParticipantAsync(
            organisation, archived: true, givenName: $"Ned{token}", familyName: $"Dusk{token}");

        // Added in an order that is not the list's, so that only the ordering can put them right.
        var archivedLate = await AddGoalAsync(services, organisation, participant, $"Goal E {token}", 15, 0, true);
        var activeTied = await AddGoalAsync(services, organisation, participant, $"Goal C {token}", 30, 1, false);
        var activeFirst = await AddGoalAsync(services, organisation, participant, $"Goal A {token}", 10, 0, false);
        var archivedEarly = await AddGoalAsync(services, organisation, participant, $"Goal D {token}", 5, 0, true);
        var activeSecond = await AddGoalAsync(services, organisation, participant, $"Goal B {token}", 30, 0, false);

        return new(
            flow,
            organisation,
            other,
            token,
            participant,
            [activeFirst, activeSecond, activeTied, archivedEarly, archivedLate],
            archived);
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

    /// <summary>Calls <paramref name="tool"/> with <paramref name="arguments"/> as <paramref name="client"/> does.</summary>
    public static ValueTask<CallToolResult> CallAsync(McpClient client, string tool, JsonObject arguments) =>
        client.CallToolAsync(
            tool,
            arguments.ToDictionary(
                argument => argument.Key,
                argument => argument.Value is null
                    ? null
                    : (object?)JsonSerializer.Deserialize<JsonElement>(argument.Value.ToJsonString())),
            cancellationToken: TestContext.Current.CancellationToken);

    private static async Task<SceneGoal> AddGoalAsync(
        IServiceProvider services,
        SeededOrganisation organisation,
        TestParticipant participant,
        string text,
        int sortOrder,
        int addedMinutesAfterFirst,
        bool archived)
    {
        var added = await services.AddGoalAsync(
            organisation,
            participant.Id,
            new(
                text,
                sortOrder,
                FirstGoalAtUtc.AddMinutes(addedMinutesAfterFirst),
                ArchivedAtUtc: archived ? FirstGoalAtUtc : null));
        return new(added.Id, text, archived, added.ETag);
    }
}
