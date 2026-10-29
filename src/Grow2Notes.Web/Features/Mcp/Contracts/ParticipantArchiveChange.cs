namespace Grow2Notes.Web.Features.Mcp.Contracts;

/// <summary>
/// What <c>archive_participant</c> returns (mcp-server.md §5.2, A137): the preview that asks the person to confirm, the
/// outcome once they have, or the answer that they declined. One record with optional fields serves all three, as the
/// contract rules allow no union. It names the participant by ID and name and holds no date of birth.
/// </summary>
/// <param name="Status">The outcome: <c>preview</c>, <c>done</c> or <c>declined</c>.</param>
/// <param name="Action">The tool that was called.</param>
/// <param name="Participant">The participant acted on; absent when the person declined.</param>
/// <param name="Effect">For a preview: what the change does.</param>
/// <param name="ExpectedVersion">
/// For a preview: the participant's version, which the confirming call sends back. A <c>done</c> result has none, so
/// the participant is looked up again before the next change.
/// </param>
/// <param name="ToConfirm">For a preview: what the assistant does next.</param>
/// <param name="Changed">For <c>done</c>: whether the participant was changed, or was already archived.</param>
/// <param name="Message">For <c>declined</c>: that nothing was changed.</param>
internal sealed record ParticipantArchiveChange(
    string Status,
    string Action,
    ParticipantMatch? Participant = null,
    string? Effect = null,
    string? ExpectedVersion = null,
    string? ToConfirm = null,
    bool? Changed = null,
    string? Message = null);
