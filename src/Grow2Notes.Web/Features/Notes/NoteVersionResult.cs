using Grow2Notes.Web.Platform;

namespace Grow2Notes.Web.Features.Notes;

/// <summary>
/// What <see cref="NoteVersions.SaveAsync"/> did with a version save: saved the version (<see cref="Saved"/>,
/// <see cref="Created"/>); found the version that an earlier try with the same key saved (<see cref="Saved"/> alone);
/// found another user's draft, which only its author may submit (<see cref="InProgress"/>); found the note's version
/// newer than the save's base (<see cref="Refusal"/>); refused a field of the request (<see cref="Errors"/>); or found
/// no note to save. Each but the first writes nothing.
/// </summary>
internal sealed record NoteVersionResult
{
    /// <summary>
    /// No participant of that ID is in the tenant's organisation, another organisation's included (design.md §9.2), or
    /// the participant has no live note for the day.
    /// </summary>
    public static readonly NoteVersionResult NotFound = new();

    /// <summary>
    /// The note's current version is not the save's base (design.md §6.7): for Submit, whose base is 0, the note is
    /// already submitted.
    /// </summary>
    public static readonly NoteVersionResult VersionConflict = new() { Refusal = ErrorCode.VersionConflict };

    private NoteVersionResult()
    {
    }

    /// <summary>The version, or <see langword="null"/>.</summary>
    public NoteVersionSaved? Saved { get; private init; }

    /// <summary>Whether this request saved the version, rather than an earlier try with the same key.</summary>
    public bool Created { get; private init; }

    /// <summary>Another user's draft, or <see langword="null"/>.</summary>
    public DraftInProgress? InProgress { get; private init; }

    /// <summary>
    /// <see cref="ErrorCode.VersionConflict"/> (<see cref="VersionConflict"/>), or <see langword="null"/>.
    /// </summary>
    public ErrorCode? Refusal { get; private init; }

    /// <summary>
    /// Each refused field's messages, keyed by its member's or parameter's C# name, or <see langword="null"/>.
    /// </summary>
    public IReadOnlyDictionary<string, string[]>? Errors { get; private init; }

    /// <summary>The request saved the version.</summary>
    public static NoteVersionResult Added(NoteVersionSaved saved) => new() { Saved = saved, Created = true };

    /// <summary>An earlier try with the same key saved the version, and this one changed nothing.</summary>
    public static NoteVersionResult Retried(NoteVersionSaved saved) => new() { Saved = saved };

    public static NoteVersionResult StartedByOther(DraftInProgress inProgress) => new() { InProgress = inProgress };

    public static NoteVersionResult Invalid(IReadOnlyDictionary<string, string[]> errors) => new() { Errors = errors };
}
