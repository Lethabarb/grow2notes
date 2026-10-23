using Microsoft.AspNetCore.Http.HttpResults;

namespace Grow2Notes.Web.Platform;

/// <summary>
/// A design.md §6.9 error code, with the HTTP status it is answered with, and the one way to answer it. Only the codes
/// the app answers are here, in §6.9's order, each added by the story that first answers it. A test reads §6.9's table
/// from design.md and fails unless each is a code there with the same status; the constructor is private, so the app
/// can answer no code that the test has not checked.
/// </summary>
internal sealed class ErrorCode
{
    /// <summary>The problem details extension member that holds the code (design.md §6.1).</summary>
    public const string Extension = "code";

    /// <summary>
    /// A worker who is not the author tried to open or save a pending edit of a submitted note, or to save its
    /// changes (§6.3), which only its author or a manager may edit. It changes nothing.
    /// </summary>
    public static readonly ErrorCode NotEditable = new(StatusCodes.Status403Forbidden, "note.not_editable");

    /// <summary>
    /// Another user started this participant's note for that day (§3.2), and only they can open or save its draft
    /// (§6.3). It includes <c>{authorDisplayName, startedAtUtc}</c>.
    /// </summary>
    public static readonly ErrorCode InProgressByOther =
        new(StatusCodes.Status409Conflict, "note.in_progress_by_other");

    /// <summary>
    /// The participant's goals changed between the form opening and the note's first save (§5.7), which would store
    /// other lists than the form showed, and so creates nothing. A first save with no lists' version is refused alike.
    /// </summary>
    public static readonly ErrorCode ListsChanged = new(StatusCodes.Status409Conflict, "note.lists_changed");

    /// <summary>
    /// The base version that a save of the note's content works on is no longer one that the note has for the caller
    /// (§6.7): a submit of a note that is already submitted, a save of Save changes after another version was saved, or
    /// an autosave whose base the note does not accept. <c>POST {base}/versions</c> adds <c>{currentVersion, createdBy,
    /// createdAtUtc}</c> when the note has a version; <c>PUT {base}/draft</c> adds none. It creates and changes
    /// nothing.
    /// </summary>
    public static readonly ErrorCode VersionConflict = new(StatusCodes.Status409Conflict, "note.version_conflict");

    /// <summary>
    /// A discard of a note that is not a draft (§5.8 <i>Discard</i>): a submitted note, which can never be discarded
    /// (§5.5 item 5), or a draft discarded already, by an earlier discard or one that committed while this one ran. It
    /// changes nothing.
    /// </summary>
    public static readonly ErrorCode NotADraft = new(StatusCodes.Status409Conflict, "note.not_a_draft");

    /// <summary>
    /// Another page, a second tab or another device, has taken over the caller's working copy since this page last
    /// saved it (§5.6): any autosave but the first from a page other than the one that saved last. It changes nothing.
    /// </summary>
    public static readonly ErrorCode TakenOver = new(StatusCodes.Status409Conflict, "draft.taken_over");

    /// <summary>
    /// A Mark reviewed on a note that is not in To review at the version the manager had open (§6.4): already reviewed,
    /// including by a retried press, edited since it was opened, a draft, or never flagged. It changes nothing.
    /// </summary>
    public static readonly ErrorCode ReviewNotCurrent = new(StatusCodes.Status409Conflict, "review.not_current");

    /// <summary>The email already belongs to an account, in any organisation and any letter case (A27).</summary>
    public static readonly ErrorCode EmailInUse = new(StatusCodes.Status409Conflict, "user.email_in_use");

    /// <summary>
    /// The change would leave the organisation with no Active manager: it would reset, deactivate, give a new address
    /// to, or make a worker of, the only one (A26).
    /// </summary>
    public static readonly ErrorCode LastManager = new(StatusCodes.Status409Conflict, "user.last_manager");

    /// <summary>
    /// A setup link was asked for a Deactivated user, who is reactivated first (D75). An Invited user's invite is
    /// resent, and an Active user's sign-in reset, instead.
    /// </summary>
    public static readonly ErrorCode UserNotActive = new(StatusCodes.Status409Conflict, "user.not_active");

    /// <summary>
    /// A common item was to be added to, reworded in or (from T07) moved into a group that is archived, which is
    /// restored first (design.md §6.9). Archiving a group leaves its items' versions as they were, so no
    /// <c>If-Match</c> could catch it.
    /// </summary>
    public static readonly ErrorCode GroupArchived = new(StatusCodes.Status409Conflict, "config.group_archived");

    /// <summary>The setup link is expired, used or replaced.</summary>
    public static readonly ErrorCode SetupLinkInvalid = new(StatusCodes.Status410Gone, "auth.setup_link_invalid");

    /// <summary>A stale <c>If-Match</c>.</summary>
    public static readonly ErrorCode PreconditionFailed =
        new(StatusCodes.Status412PreconditionFailed, "precondition.failed");

    /// <summary>
    /// A note's first save for a date after today in Melbourne, for which nobody may start a note (§5.5 item 4).
    /// </summary>
    public static readonly ErrorCode FutureDate = new(StatusCodes.Status422UnprocessableEntity, "note.future_date");

    /// <summary>
    /// A worker's first save of a note for a day before today in Melbourne, which only a manager may start (§5.5 item
    /// 4, D35), as when a form opened before midnight makes its first change after it (§3.3).
    /// </summary>
    public static readonly ErrorCode PastDateManagerOnly =
        new(StatusCodes.Status422UnprocessableEntity, "note.past_date_manager_only");

    /// <summary>A note's first save for an archived participant, who gets no new notes (§3.7, §5.5 item 4).</summary>
    public static readonly ErrorCode ParticipantArchived =
        new(StatusCodes.Status422UnprocessableEntity, "note.participant_archived");

    /// <summary>
    /// A reorder's IDs are not the list's active rows, each exactly once
    /// (<see cref="Configuration.ListOrder.Reorder"/>), or a row of the list changed while it was being reordered.
    /// </summary>
    public static readonly ErrorCode OrderMismatch =
        new(StatusCodes.Status422UnprocessableEntity, "config.order_mismatch");

    /// <summary>
    /// A change that Every note never takes, as it is built in, always first and fixed (A43): a rename, an archive or a
    /// restore, none of which the screen offers. A reorder refuses its ID as <see cref="OrderMismatch"/> instead.
    /// </summary>
    public static readonly ErrorCode EveryNoteFixed =
        new(StatusCodes.Status422UnprocessableEntity, "config.every_note_fixed");

    /// <summary>Field errors, in the problem's <c>errors</c> (<see cref="ValidationProblems"/>).</summary>
    public static readonly ErrorCode ValidationFailed =
        new(StatusCodes.Status422UnprocessableEntity, "validation.failed");

    /// <summary>A required <c>If-Match</c> header is missing.</summary>
    public static readonly ErrorCode PreconditionRequired =
        new(StatusCodes.Status428PreconditionRequired, "precondition.required");

    private ErrorCode(int status, string code)
    {
        Status = status;
        Code = code;
    }

    public int Status { get; }

    public string Code { get; }

    /// <summary>Answers this code: problem details with its status, and the code in their <c>code</c> member.</summary>
    public ProblemHttpResult Problem() => Problem(new Dictionary<string, object?>());

    /// <summary>
    /// Answers this code with the members that design.md §6.9 says it includes beside the code, such as
    /// <see cref="InProgressByOther"/>'s author and start time, each named as the API's JSON names it.
    /// </summary>
    public ProblemHttpResult Problem(IReadOnlyDictionary<string, object?> includes) =>
        TypedResults.Problem(
            statusCode: Status, extensions: new Dictionary<string, object?>(includes) { [Extension] = Code });
}
