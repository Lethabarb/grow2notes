using Grow2Notes.Web.Platform;

namespace Grow2Notes.Web.Features.Notes;

/// <summary>
/// What <see cref="NoteDrafts.SaveAsync"/> did with a save: saved the working copy (<see cref="Saved"/>), creating the
/// note with it when the save was the note's first (<see cref="Created"/>); found another user's draft, which only its
/// author may save (<see cref="InProgress"/>); refused to start the note, or found it submitted
/// (<see cref="Refusal"/>); refused a field of the body (<see cref="Errors"/>); or found nothing to save. Each but the
/// first writes nothing.
/// </summary>
internal sealed record DraftSaveResult
{
    /// <summary>
    /// No participant of that ID is in the tenant's organisation, another organisation's included (design.md §9.2).
    /// </summary>
    public static readonly DraftSaveResult NotFound = new();

    /// <summary>
    /// The note is submitted, and the caller has no working copy of it, which T14 gives them as a pending edit, so the
    /// save's base, a draft's 0, is stale (design.md §6.7).
    /// </summary>
    public static readonly DraftSaveResult VersionConflict = new() { Refusal = ErrorCode.VersionConflict };

    private DraftSaveResult()
    {
    }

    /// <summary>The save, or <see langword="null"/>.</summary>
    public DraftSaved? Saved { get; private init; }

    /// <summary>Whether the save created the note and its author's working copy (design.md §5.5 item 2).</summary>
    public bool Created { get; private init; }

    /// <summary>Another user's draft, or <see langword="null"/>.</summary>
    public DraftInProgress? InProgress { get; private init; }

    /// <summary>
    /// Why the note cannot be started, by the date rules, the participant's status or the lists' version (design.md
    /// §5.5 item 4, §5.7); or <see cref="ErrorCode.VersionConflict"/> (<see cref="VersionConflict"/>); or
    /// <see langword="null"/>.
    /// </summary>
    public ErrorCode? Refusal { get; private init; }

    /// <summary>
    /// Each refused field's messages, keyed by its member's name in the request, or <see langword="null"/>.
    /// </summary>
    public IReadOnlyDictionary<string, string[]>? Errors { get; private init; }

    /// <summary>The save created the note, a draft, and the caller's working copy.</summary>
    public static DraftSaveResult Started(DraftSaved saved) => new() { Saved = saved, Created = true };

    /// <summary>The save replaced the caller's working copy.</summary>
    public static DraftSaveResult Updated(DraftSaved saved) => new() { Saved = saved };

    public static DraftSaveResult StartedByOther(DraftInProgress inProgress) => new() { InProgress = inProgress };

    public static DraftSaveResult NotStarted(ErrorCode refusal) => new() { Refusal = refusal };

    public static DraftSaveResult Invalid(IReadOnlyDictionary<string, string[]> errors) => new() { Errors = errors };
}
