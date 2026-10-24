using Grow2Notes.Web.Data;
using Grow2Notes.Web.Features.Auth;
using Grow2Notes.Web.Features.Notes;
using Microsoft.EntityFrameworkCore;

namespace Grow2Notes.Web.Features.Reports;

/// <summary>
/// A daily report as the file shows it (design.md §11.1): the organisation's submitted notes for one day, each as its
/// current version, in participant order. It is a read model, with no table, and it holds no count of notes. Times
/// stay in UTC here; <see cref="ReportDates"/> turns them into the file's words.
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
            from n in SubmittedOn(db, noteDate)
            join participant in db.Participants on n.ParticipantId equals participant.Id
            join author in db.Users on n.AuthorUserId equals author.Id
            orderby participant.FamilyName, participant.GivenName, participant.Id
            select new
            {
                n.Id,
                n.CurrentVersionNumber,
                n.CreatedAtUtc,
                n.FlagStatus,
                FlagReason = db.NoteVersions
                    .Where(v => v.NoteId == n.Id && v.VersionNumber == n.FlaggedVersionNumber)
                    .Select(v => v.FlagReason)
                    .FirstOrDefault(),
                FirstSubmittedAtUtc = n.FirstSubmittedAtUtc!.Value,
                participant.GivenName,
                participant.FamilyName,
                author.DisplayName,
            }).ToListAsync(cancellationToken);
        if (rows.Count == 0)
        {
            return null;
        }

        // One context cannot run two reads at once, so each note's reads are made in turn.
        var notes = new List<DailyReportNote>(rows.Count);
        foreach (var row in rows)
        {
            var content = await NoteVersionContent.ReadAsync(db, row.Id, row.CurrentVersionNumber, cancellationToken);
            DateTime? pastDayWrittenAtUtc = LiveNote.IsPastDay(row.CreatedAtUtc, noteDate) ? row.CreatedAtUtc : null;
            var lastEdit = row.CurrentVersionNumber > 1
                && await LatestVersion.FindAsync(db, row.Id, cancellationToken) is { } latest
                    ? new NoteReadLastEdit(latest.CreatedBy, latest.CreatedAtUtc)
                    : null;
            notes.Add(new(
                $"{row.GivenName} {row.FamilyName}",
                row.DisplayName,
                pastDayWrittenAtUtc,
                row.FirstSubmittedAtUtc,
                NoteReadVersion.Of(
                    row.CurrentVersionNumber,
                    content.Snapshot,
                    content.Narrative,
                    content.IsFlagged,
                    content.FlagReason),
                lastEdit,
                await FlagOfAsync(db, row.Id, row.FlagStatus, row.FlagReason, cancellationToken)));
        }

        var organisationName = await MeEndpoint.OrganisationNameAsync(db, organisationId, cancellationToken);
        var exportedBy = await db.Users
            .Where(user => user.Id == exportedByUserId)
            .Select(user => user.DisplayName)
            .SingleAsync(cancellationToken);
        return new(organisationName, noteDate, asAtUtc, exportedBy, notes);
    }

    // A note is flagged when its flag status is not None, and its reason is that of its flagged version. A review
    // moves it to Reviewed, and a re-flag moves it back to To review, so only the newest review counts.
    private static async Task<DailyReportFlag?> FlagOfAsync(
        Grow2NotesDbContext db,
        Guid noteId,
        FlagStatus flagStatus,
        string? flagReason,
        CancellationToken cancellationToken)
    {
        if (flagStatus == FlagStatus.None)
        {
            return null;
        }

        var review = flagStatus == FlagStatus.Reviewed
            ? (await NoteReadReview.NewestFirstAsync(db, noteId, cancellationToken)).FirstOrDefault()
            : null;
        return new(flagReason!, review);
    }

    /// <summary>
    /// Whether <paramref name="noteDate"/> has a submitted note in the caller's organisation. It is <c>false</c>
    /// exactly when <see cref="ReadAsync"/> returns <c>null</c>: both read <see cref="SubmittedOn"/>, and the joins in
    /// <see cref="ReadAsync"/> lose no row, because a note's participant and author are in the note's own organisation
    /// (composite foreign keys).
    /// </summary>
    public static Task<bool> HasNotesAsync(
        Grow2NotesDbContext db, DateOnly noteDate, CancellationToken cancellationToken)
        => SubmittedOn(db, noteDate).AnyAsync(cancellationToken);

    private static IQueryable<Note> SubmittedOn(Grow2NotesDbContext db, DateOnly noteDate)
        => db.Notes.Where(n => n.Status == NoteStatus.Submitted && n.NoteDate == noteDate);
}

/// <summary>
/// One submitted note in a daily report: the participant's name, who wrote the note, when it was started if that was
/// on a later day than its date, as a past-day note is, when it was first submitted, and the version that was current
/// when the report was read; who saved that version and when, when the note has been edited; and its flag, when it has
/// one.
/// </summary>
internal sealed record DailyReportNote(
    string ParticipantName,
    string AuthorDisplayName,
    DateTime? PastDayWrittenAtUtc,
    DateTime FirstSubmittedAtUtc,
    NoteReadVersion Current,
    NoteReadLastEdit? LastEdit,
    DailyReportFlag? Flag);

/// <summary>
/// A note's flag in a daily report: the reason that the writer gave when they flagged it, and the newest review of it,
/// or <c>null</c> when it is still to review.
/// </summary>
internal sealed record DailyReportFlag(string Reason, NoteReadReview? Review);
