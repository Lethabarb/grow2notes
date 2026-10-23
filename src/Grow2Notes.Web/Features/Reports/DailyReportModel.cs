using Grow2Notes.Web.Data;
using Grow2Notes.Web.Features.Auth;
using Grow2Notes.Web.Features.Notes;
using Microsoft.EntityFrameworkCore;

namespace Grow2Notes.Web.Features.Reports;

/// <summary>
/// A daily report as the file shows it (design.md §11.1): the organisation's submitted notes for one day, each as its
/// current version, in participant order. It is a read model, with no table, and it holds no counts. Times stay in UTC
/// here; <see cref="ReportDates"/> turns them into the file's words.
/// </summary>
internal sealed record DailyReportModel(
    string OrganisationName,
    DateOnly NoteDate,
    DateTime AsAtUtc,
    string ExportedBy,
    IReadOnlyList<DailyReportNote> Notes)
{
    /// <summary>
    /// The report for <paramref name="noteDate"/>, or <c>null</c> when the day has no submitted note, so that
    /// <see cref="Notes"/> is never empty.
    /// </summary>
    /// <remarks>
    /// The tenant query filter keeps the notes and participants to the caller's organisation, so
    /// <paramref name="organisationId"/> only names the organisation. An archived participant and a deactivated author
    /// are included, as the note is a record of what happened. A draft or a discarded draft is not a note to report,
    /// so <see cref="LiveNote.All"/>, which admits drafts, is not used. Every read is a projection or untracked, so
    /// that the audit event the caller writes next saves nothing else.
    /// </remarks>
    public static async Task<DailyReportModel?> ReadAsync(
        Grow2NotesDbContext db,
        Guid organisationId,
        DateOnly noteDate,
        Guid exportedByUserId,
        DateTime asAtUtc,
        CancellationToken cancellationToken)
    {
        var rows = await (
            from n in db.Notes
            where n.Status == NoteStatus.Submitted && n.NoteDate == noteDate
            join participant in db.Participants on n.ParticipantId equals participant.Id
            join author in db.Users on n.AuthorUserId equals author.Id
            orderby participant.FamilyName, participant.GivenName, participant.Id
            select new
            {
                n.Id,
                n.CurrentVersionNumber,
                FirstSubmittedAtUtc = n.FirstSubmittedAtUtc!.Value,
                participant.GivenName,
                participant.FamilyName,
                author.DisplayName,
            }).ToListAsync(cancellationToken);
        if (rows.Count == 0)
        {
            return null;
        }

        // One context cannot run two reads at once, so each note's version is read in turn.
        var notes = new List<DailyReportNote>(rows.Count);
        foreach (var row in rows)
        {
            var content = await NoteVersionContent.ReadAsync(db, row.Id, row.CurrentVersionNumber, cancellationToken);
            notes.Add(new(
                $"{row.GivenName} {row.FamilyName}",
                row.DisplayName,
                row.FirstSubmittedAtUtc,
                NoteReadVersion.Of(
                    row.CurrentVersionNumber,
                    content.Snapshot,
                    content.Narrative,
                    content.IsFlagged,
                    content.FlagReason)));
        }

        var organisationName = await MeEndpoint.OrganisationNameAsync(db, organisationId, cancellationToken);
        var exportedBy = await db.Users
            .Where(user => user.Id == exportedByUserId)
            .Select(user => user.DisplayName)
            .SingleAsync(cancellationToken);
        return new(organisationName, noteDate, asAtUtc, exportedBy, notes);
    }
}

/// <summary>
/// One submitted note in a daily report: the participant's name, who wrote the note and when it was first submitted,
/// and the version that was current when the report was read.
/// </summary>
internal sealed record DailyReportNote(
    string ParticipantName, string AuthorDisplayName, DateTime FirstSubmittedAtUtc, NoteReadVersion Current);
