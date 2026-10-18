namespace Grow2Notes.Web.Features.Notes;

/// <summary>
/// What <see cref="NoteDrafts.ReadAsync"/> found: the form with the caller's working copy (<see cref="Form"/>);
/// another user's draft, which only its author may open (<see cref="InProgress"/>); or nothing to open.
/// </summary>
internal sealed record DraftRead
{
    /// <summary>
    /// No participant of that ID is in the tenant's organisation, another organisation's included (design.md §9.2);
    /// or no note can exist for the date, which is after today; or the caller has no working copy of a submitted note,
    /// which T14 gives from the note's current version.
    /// </summary>
    public static readonly DraftRead NotFound = new();

    private DraftRead()
    {
    }

    /// <summary>The form with the caller's working copy, or <see langword="null"/>.</summary>
    public DraftForm? Form { get; private init; }

    /// <summary>Another user's draft, or <see langword="null"/>.</summary>
    public DraftInProgress? InProgress { get; private init; }

    public static DraftRead Of(DraftForm form) => new() { Form = form };

    public static DraftRead StartedByOther(DraftInProgress inProgress) => new() { InProgress = inProgress };
}
