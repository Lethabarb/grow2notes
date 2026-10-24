using Grow2Notes.Web.Data;
using Grow2Notes.Web.Features.Auth;
using Grow2Notes.Web.Features.Notes;
using Microsoft.EntityFrameworkCore;

namespace Grow2Notes.Web.Features.Reports;

/// <summary>
/// A participant's record as the file shows it (design.md §11.6): their submitted notes for a range of dates, oldest
/// first, each as its current version and, when asked for, with its earlier versions. It is a read model, with no
/// table. Times stay in UTC here; <see cref="ReportDates"/> turns them into the file's words.
/// </summary>
/// <param name="OrganisationName">The organisation's name, for the title block and the file's properties.</param>
/// <param name="ParticipantName">The participant's given and family name as stored.</param>
/// <param name="DateOfBirth">The participant's date of birth, which only the title block shows.</param>
/// <param name="From">The first date of the range, as the manager entered it.</param>
/// <param name="To">The last date of the range, as entered, which may be after today (A60).</param>
/// <param name="IncludesEarlierVersions">Whether the file was asked to include earlier versions.</param>
/// <param name="AsAtUtc">When the record was exported.</param>
/// <param name="ExportedBy">The display name of the manager who exported it.</param>
/// <param name="Notes">The submitted notes in the range, oldest first.</param>
internal sealed record ParticipantExportModel(
    string OrganisationName,
    string ParticipantName,
    DateOnly DateOfBirth,
    DateOnly From,
    DateOnly To,
    bool IncludesEarlierVersions,
    DateTime AsAtUtc,
    string ExportedBy,
    IReadOnlyList<ParticipantExportNote> Notes)
{
    /// <summary>
    /// The record of <paramref name="participantId"/> for the dates <paramref name="from"/> to <paramref name="to"/>,
    /// or <c>null</c> when the participant has no submitted note in that range, so that <see cref="Notes"/> is never
    /// empty. The caller has found the participant through the tenant query filter.
    /// </summary>
    /// <remarks>
    /// The notes are read as the daily report reads them, and a draft or a discarded draft is not a note to report.
    /// Without <paramref name="includeHistory"/>, no version below a note's current one is read. Every read is a
    /// projection or untracked, so that the audit event the caller writes next saves nothing else.
    /// </remarks>
    public static async Task<ParticipantExportModel?> ReadAsync(
        Grow2NotesDbContext db,
        Guid organisationId,
        Guid participantId,
        DateOnly from,
        DateOnly to,
        bool includeHistory,
        Guid exportedByUserId,
        DateTime asAtUtc,
        CancellationToken cancellationToken)
    {
        var reads = await DailyReportModel.ReadNotesAsync(
            db,
            db.Notes.Where(n => n.ParticipantId == participantId
                && n.Status == NoteStatus.Submitted
                && n.NoteDate >= from
                && n.NoteDate <= to),
            cancellationToken);
        if (reads.Count == 0)
        {
            return null;
        }

        var notes = new List<ParticipantExportNote>(reads.Count);
        foreach (var read in reads)
        {
            var earlier = includeHistory && read.Note.Current.VersionNumber is > 1 and var current
                ? await EarlierVersionsAsync(db, read.NoteId, current, cancellationToken)
                : [];
            notes.Add(new(read.NoteDate, read.Note, earlier));
        }

        var participant = await db.Participants
            .AsNoTracking()
            .Where(p => p.Id == participantId)
            .Select(p => new { p.GivenName, p.FamilyName, p.DateOfBirth })
            .SingleAsync(cancellationToken);
        var organisationName = await MeEndpoint.OrganisationNameAsync(db, organisationId, cancellationToken);
        var exportedBy = await db.Users
            .Where(user => user.Id == exportedByUserId)
            .Select(user => user.DisplayName)
            .SingleAsync(cancellationToken);
        return new(
            organisationName,
            $"{participant.GivenName} {participant.FamilyName}",
            participant.DateOfBirth,
            from,
            to,
            includeHistory,
            asAtUtc,
            exportedBy,
            notes);
    }

    // Every version below the current one, newest first. A review is only ever of a flagged version, so the reviews
    // are read, once for the note, only when an earlier version is flagged. One context cannot run two reads at once,
    // so the reads are made in turn.
    private static async Task<IReadOnlyList<ParticipantExportVersion>> EarlierVersionsAsync(
        Grow2NotesDbContext db, Guid noteId, int currentVersionNumber, CancellationToken cancellationToken)
    {
        var summaries = (await NoteVersionSummary.Query(db, noteId, versionNumber: null).ToListAsync(cancellationToken))
            .Where(summary => summary.VersionNumber < currentVersionNumber)
            .ToList();
        var reviews = summaries.Any(summary => summary.IsFlagged)
            ? await NoteReadReview.NewestFirstAsync(db, noteId, cancellationToken)
            : [];
        var versions = new List<ParticipantExportVersion>(summaries.Count);
        foreach (var summary in summaries)
        {
            var content = await NoteVersionContent.ReadAsync(db, noteId, summary.VersionNumber, cancellationToken);
            versions.Add(new(
                summary,
                NoteReadVersion.Of(
                    summary.VersionNumber, content.Snapshot, content.Narrative, content.IsFlagged, content.FlagReason),
                [.. reviews.Where(review => review.VersionNumber == summary.VersionNumber)]));
        }

        return versions;
    }
}

/// <summary>
/// One submitted note in a participant's record: its date, the note as a daily report shows it, and, when the record
/// includes them and the note has been edited, every earlier version, newest first.
/// </summary>
internal sealed record ParticipantExportNote(
    DateOnly NoteDate, DailyReportNote Note, IReadOnlyList<ParticipantExportVersion> EarlierVersions);

/// <summary>
/// An earlier version of a note in a participant's record: who saved it and when, its content, with its own flag, and
/// the reviews of that version, newest first.
/// </summary>
internal sealed record ParticipantExportVersion(
    NoteVersionSummary Summary, NoteReadVersion Content, IReadOnlyList<NoteReadReview> Reviews);
