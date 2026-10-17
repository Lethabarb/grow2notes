using Grow2Notes.Web.Platform;

namespace Grow2Notes.Web.Features.Participants;

/// <summary>
/// What <see cref="GoalChanges"/> did with a change to a participant's goals: found no such participant or goal in the
/// tenant's organisation, another organisation's included (design.md §9.2); refused the change with a design.md §6.9
/// code, <c>If-Match</c>'s or the save's (<see cref="Error"/>); refused the text, with its message
/// (<see cref="Errors"/>); or took the change, and saved it with its audit event when a value changed, giving the
/// goal's <c>ETag</c> after a reword (<see cref="ETag"/>).
/// </summary>
internal sealed record GoalChangeResult
{
    /// <summary>No participant or goal of that ID is in the tenant's organisation, so nothing was changed.</summary>
    public static readonly GoalChangeResult NotFound = new() { Found = false };

    /// <summary>The change is saved, with its audit event.</summary>
    public static readonly GoalChangeResult Saved = new();

    private GoalChangeResult()
    {
    }

    public bool Found { get; private init; } = true;

    /// <summary>The code that the change was refused with, or <see langword="null"/>.</summary>
    public ErrorCode? Error { get; private init; }

    /// <summary>
    /// Each refused field's messages, keyed by its member's name in the request, or <see langword="null"/>.
    /// </summary>
    public IReadOnlyDictionary<string, string[]>? Errors { get; private init; }

    /// <summary>
    /// The goal's <c>ETag</c> once a reword is taken: a new one after a save, or, when the text was already stored, the
    /// one that the <c>If-Match</c> matched (D84); otherwise <see langword="null"/>.
    /// </summary>
    public string? ETag { get; private init; }

    /// <summary>The change was refused with <paramref name="error"/>, and nothing was saved.</summary>
    public static GoalChangeResult Refused(ErrorCode error) => new() { Error = error };

    /// <summary>The change was refused for the fields <paramref name="errors"/> name, and nothing was saved.</summary>
    public static GoalChangeResult Invalid(IReadOnlyDictionary<string, string[]> errors) => new() { Errors = errors };

    /// <summary>A reword was taken, and the goal's <c>ETag</c> is now <paramref name="etag"/>.</summary>
    public static GoalChangeResult Reworded(string etag) => new() { ETag = etag };
}
