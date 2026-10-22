using Grow2Notes.Web.Platform;

namespace Grow2Notes.Web.Features.Notes;

/// <summary>
/// What <see cref="NoteDrafts.ReadAsync"/> found: the form with the caller's working copy (<see cref="Form"/>);
/// another user's draft, which only its author may open (<see cref="InProgress"/>); a submitted note that the caller
/// may not edit (<see cref="Refusal"/>); or nothing to open.
/// </summary>
internal sealed record DraftRead
{
    /// <summary>
    /// No participant of that ID is in the tenant's organisation, another organisation's included (design.md §9.2);
    /// or no note can exist for the date, which is after today; or a caller who is not a manager may not start the
    /// note, for a day before yesterday (design.md A65); or the caller has no working copy of their own draft, which a
    /// submit has taken away since.
    /// </summary>
    public static readonly DraftRead NotFound = new();

    /// <summary>
    /// The note is submitted, and only its author or a manager may edit it (design.md §6.3): the caller is a worker
    /// who did not write it.
    /// </summary>
    public static readonly DraftRead NotEditable = new() { Refusal = ErrorCode.NotEditable };

    private DraftRead()
    {
    }

    /// <summary>The form with the caller's working copy, or <see langword="null"/>.</summary>
    public DraftForm? Form { get; private init; }

    /// <summary>Another user's draft, or <see langword="null"/>.</summary>
    public DraftInProgress? InProgress { get; private init; }

    /// <summary><see cref="ErrorCode.NotEditable"/> (<see cref="NotEditable"/>), or <see langword="null"/>.</summary>
    public ErrorCode? Refusal { get; private init; }

    public static DraftRead Of(DraftForm form) => new() { Form = form };

    public static DraftRead StartedByOther(DraftInProgress inProgress) => new() { InProgress = inProgress };
}
