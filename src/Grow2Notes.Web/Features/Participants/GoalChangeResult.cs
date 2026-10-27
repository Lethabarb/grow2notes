using Grow2Notes.Web.Platform;
using Grow2Notes.Web.Platform.Configuration;

namespace Grow2Notes.Web.Features.Participants;

/// <summary>
/// What <see cref="GoalChanges"/> did with a change to a participant's goals: found no such participant or goal in the
/// tenant's organisation, another organisation's included (design.md §9.2); refused the change with a design.md §6.9
/// code, the archived participant's, <c>If-Match</c>'s, the reorder's or the save's (<see cref="Error"/>); refused the
/// text, with its message (<see cref="Errors"/>); or took the change, and saved it with its audit event only when a
/// value changed (<see cref="Changed"/>), giving the new goal's ID after an add (<see cref="GoalId"/>) and the goal's
/// <c>ETag</c> after a reword (<see cref="ETag"/>).
/// </summary>
internal sealed record GoalChangeResult
{
    /// <summary>No participant or goal of that ID is in the tenant's organisation, so nothing was changed.</summary>
    public static readonly GoalChangeResult NotFound = new() { Found = false };

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
    /// Whether the change was saved, with its event: <see langword="false"/> for one that changed no value, which
    /// Release 2's tools give as <c>changed</c> (mcp-server.md §5.1).
    /// </summary>
    public bool Changed { get; private init; }

    /// <summary>The ID of the goal that an add made, or <see langword="null"/>.</summary>
    public Guid? GoalId { get; private init; }

    /// <summary>
    /// The goal's <c>ETag</c> once a reword is taken: a new one after a save, or, when the text was already stored, the
    /// one that the <c>If-Match</c> matched (D84); otherwise <see langword="null"/>.
    /// </summary>
    public string? ETag { get; private init; }

    /// <summary>The change was refused with <paramref name="error"/>, and nothing was saved.</summary>
    public static GoalChangeResult Refused(ErrorCode error) => new() { Error = error };

    /// <summary>The change was refused for the fields <paramref name="errors"/> name, and nothing was saved.</summary>
    public static GoalChangeResult Invalid(IReadOnlyDictionary<string, string[]> errors) => new() { Errors = errors };

    /// <summary>
    /// What <see cref="ConfigurationChanges.SaveAsync"/> did: saved the change, found nothing to change, or refused it,
    /// with the code it gave.
    /// </summary>
    public static GoalChangeResult Of(ConfigurationSaveResult saved) =>
        new() { Error = saved.Error, Changed = saved.Changed };

    /// <summary>A goal was added, with the ID <paramref name="goalId"/>, and saved with its audit event.</summary>
    public static GoalChangeResult Added(Guid goalId) => new() { Changed = true, GoalId = goalId };

    /// <summary>
    /// A reword was taken, and the goal's <c>ETag</c> is now <paramref name="etag"/>; <paramref name="changed"/> is
    /// whether the text was saved.
    /// </summary>
    public static GoalChangeResult Reworded(string etag, bool changed) => new() { ETag = etag, Changed = changed };
}
