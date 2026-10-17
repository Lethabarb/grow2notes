namespace Grow2Notes.Web.Features.Participants;

/// <summary>
/// What <see cref="GoalChanges"/> did with a change to a participant's goals: found no such participant in the tenant's
/// organisation, another organisation's included (design.md §9.2); refused the text, with its message
/// (<see cref="Errors"/>); or saved the change with its audit event.
/// </summary>
internal sealed record GoalChangeResult
{
    /// <summary>No participant of that ID is in the tenant's organisation, so nothing was changed.</summary>
    public static readonly GoalChangeResult NotFound = new() { Found = false };

    /// <summary>The change is saved, with its audit event.</summary>
    public static readonly GoalChangeResult Saved = new();

    private GoalChangeResult()
    {
    }

    public bool Found { get; private init; } = true;

    /// <summary>
    /// Each refused field's messages, keyed by its member's name in the request, or <see langword="null"/>.
    /// </summary>
    public IReadOnlyDictionary<string, string[]>? Errors { get; private init; }

    /// <summary>The change was refused for the fields <paramref name="errors"/> name, and nothing was saved.</summary>
    public static GoalChangeResult Invalid(IReadOnlyDictionary<string, string[]> errors) => new() { Errors = errors };
}
