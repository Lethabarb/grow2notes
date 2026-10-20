namespace Grow2Notes.Web.Features.Notes;

/// <summary>
/// What <see cref="NoteDiscard.DiscardAsync"/> did with a discard. Each but the first changes nothing.
/// </summary>
internal enum NoteDiscardResult
{
    /// <summary>
    /// The draft is Discarded, with who discarded it and when, and its day is free (design.md §5.5 item 5).
    /// </summary>
    Discarded,

    /// <summary>
    /// No participant of that ID is in the tenant's organisation, another organisation's included (design.md §9.2), or
    /// the participant has no note for the day, live or discarded.
    /// </summary>
    NotFound,

    /// <summary>
    /// The caller is a worker who did not start the draft, which only its author or a manager may discard (§3.4).
    /// </summary>
    NotTheAuthor,

    /// <summary>
    /// The note is submitted, which can never be discarded (§5.5 item 5), or the day's draft is discarded already, by
    /// another discard that may have committed while this one ran (§5.8 <i>Discard</i>).
    /// </summary>
    NotADraft,
}
