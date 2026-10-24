using Grow2Notes.Web.Features.Notes;

namespace Grow2Notes.Web.Features.Reports;

/// <summary>
/// What a report file says and in what order (design.md §11.3), as <see cref="ReportBlock"/>s that both renderers read.
/// The file's own words are constants here, outside the SPA's copy, as the file is not a screen.
/// </summary>
internal static class ReportLayout
{
    private const string DailyTitle = "Daily progress notes";

    private const string RecordTitle = "Progress notes record";

    private const string DateOfBirth = "Date of birth: ";

    private const string To = " to ";

    private const string EarlierVersionsIncluded = "Earlier versions: included";

    private const string EarlierVersionsNotIncluded = "Earlier versions: not included";

    private const string EarlierVersions = "Earlier versions";

    private const string Version = "Version ";

    private const string SavedBy = ": saved by ";

    private const string SubmittedBy = ": submitted by ";

    private const string AsAt = "As at ";

    private const string ExportedBy = "Exported by ";

    private const string WrittenBy = "Written by ";

    private const string PastDayWrittenBy = "Past-day note, written by ";

    private const string PastDayManagerOn = " (manager) on ";

    private const string Submitted = " · Submitted ";

    private const string Edited = "Edited · ";

    private const string VersionsLastChangeBy = " versions · last change by ";

    private const string FlaggedStart = "Flagged for manager: \"";

    private const string FlaggedEnd = "\"";

    private const string FlagRemoved = " (flag removed in a later edit)";

    private const string ToReview = "To review";

    private const string ReviewedBy = "Reviewed by ";

    private const string On = " on ";

    private const string Comment = "Comment: ";

    private const string CommonItems = "Common items";

    private const string NoCommonItems = "No common items set";

    private const string Goals = "Goals";

    private const string NoGoals = "No goals set";

    private const string GuidedNotes = "Guided notes";

    private const string FooterStart = "Confidential: health information. Copy exported from Grow2Notes by ";

    private const string FooterEnd = ". The record in Grow2Notes is authoritative.";

    /// <summary>The words before the page number, as both renderers print them.</summary>
    public const string FooterPage = " Page ";

    /// <summary>The words between the page number and the number of pages.</summary>
    public const string FooterOf = " of ";

    /// <summary>
    /// The day's report: the running header, the footer sentence and the properties, then the body, which is the
    /// organisation's name, the title block and each note under its participant's name, in the model's order. The
    /// exporter and the time in the footer are the strings the title block shows.
    /// </summary>
    public static ReportDocument DailyReport(DailyReportModel model)
    {
        var asAt = ReportDates.DateTimeWithYear(model.AsAtUtc);
        List<ReportBlock> blocks =
        [
            new(ReportBlockKind.Title, model.OrganisationName),
            new(ReportBlockKind.Subtitle, DailyTitle),
            new(ReportBlockKind.Subtitle, ReportDates.DateLong(model.NoteDate)),
            new(ReportBlockKind.Subtitle, AsAt + asAt),
            new(ReportBlockKind.Subtitle, ExportedBy + model.ExportedBy),
        ];
        foreach (var note in model.Notes)
        {
            blocks.Add(new(ReportBlockKind.Heading1, note.ParticipantName));
            blocks.AddRange(NoteBody(note));
        }

        return new ReportDocument(
            DailyTitle + ": " + ReportDates.DateWithYear(model.NoteDate),
            model.OrganisationName,
            FooterStart + model.ExportedBy + ", " + asAt + FooterEnd,
            blocks);
    }

    /// <summary>
    /// A participant's record for a range of dates (design.md §11.6): the title block, which names the participant,
    /// their date of birth, the range as the manager entered it, the organisation, who exported it and when, and
    /// whether earlier versions are in it; then each note under its date, in the model's order. The running header, the
    /// footer sentence and the properties hold the title and the organisation, never the participant's name.
    /// </summary>
    public static ReportDocument ParticipantRecord(ParticipantExportModel model)
    {
        var asAt = ReportDates.DateTimeWithYear(model.AsAtUtc);
        var range = ReportDates.DatePlain(model.From) + To + ReportDates.DatePlain(model.To);
        List<ReportBlock> blocks =
        [
            new(ReportBlockKind.Title, RecordTitle),
            new(ReportBlockKind.Subtitle, model.ParticipantName),
            new(ReportBlockKind.Subtitle, DateOfBirth + ReportDates.DatePlain(model.DateOfBirth)),
            new(ReportBlockKind.Subtitle, range),
            new(ReportBlockKind.Subtitle, model.OrganisationName),
            new(ReportBlockKind.Subtitle, $"{ExportedBy}{model.ExportedBy}, {asAt}"),
            new(
                ReportBlockKind.Subtitle,
                model.IncludesEarlierVersions ? EarlierVersionsIncluded : EarlierVersionsNotIncluded),
        ];
        foreach (var note in model.Notes)
        {
            blocks.Add(new(ReportBlockKind.Heading1, ReportDates.DateLong(note.NoteDate)));
            blocks.AddRange(NoteBody(note.Note));
            blocks.AddRange(EarlierVersionBlocks(note.EarlierVersions));
        }

        return new ReportDocument(
            $"{RecordTitle}: {range}",
            model.OrganisationName,
            FooterStart + model.ExportedBy + ", " + asAt + FooterEnd,
            blocks);
    }

    // The earlier versions sit under a heading of their own, newest first. Each version's own flag, reviews and
    // comments come before its body, and its labels are plain lines, as the headings are the note's and the version's.
    private static IEnumerable<ReportBlock> EarlierVersionBlocks(IReadOnlyList<ParticipantExportVersion> versions)
    {
        if (versions.Count == 0)
        {
            yield break;
        }

        yield return new(ReportBlockKind.Heading2, EarlierVersions);
        foreach (var version in versions)
        {
            var summary = version.Summary;
            yield return new(
                ReportBlockKind.Heading3,
                $"{Version}{summary.VersionNumber}{(summary.VersionNumber == 1 ? SubmittedBy : SavedBy)}"
                + $"{summary.CreatedBy.DisplayName}, {ReportDates.DateTimeWithYear(summary.CreatedAtUtc)}");
            if (version.Content.IsFlagged)
            {
                yield return new(
                    ReportBlockKind.Line, $"{FlaggedStart}{version.Content.FlagReason}{FlaggedEnd}");
            }

            foreach (var review in version.Reviews)
            {
                yield return new(
                    ReportBlockKind.Line,
                    ReviewedBy + review.ReviewedBy.DisplayName + On
                        + ReportDates.DateTimeWithYear(review.ReviewedAtUtc));
                if (review.Comment is { Length: > 0 } comment)
                {
                    yield return new(ReportBlockKind.Line, Comment + comment);
                }
            }

            foreach (var block in VersionBody(version.Content, ReportBlockKind.Line, ReportBlockKind.Line))
            {
                yield return block;
            }
        }
    }

    /// <summary>
    /// A note under its participant's name, in the order of design.md §11.3's layout example: who wrote it and when it
    /// was first submitted, with the day it was written for a past-day note; that it was edited, by whom and when; its
    /// flag, and whether a manager has reviewed it; its goals in the order of its version; its common items by group,
    /// as its version shows them; and its Guided notes exactly as stored. The submitted time carries its day, so a note
    /// first submitted on a later day already shows its own date.
    /// </summary>
    public static IEnumerable<ReportBlock> NoteBody(DailyReportNote note)
    {
        var submitted = Submitted + ReportDates.DateTimeShort(note.FirstSubmittedAtUtc);
        yield return new(
            ReportBlockKind.Line,
            note.PastDayWrittenAtUtc is { } writtenAtUtc
                ? $"{PastDayWrittenBy}{note.AuthorDisplayName}{PastDayManagerOn}"
                    + $"{ReportDates.DateTimeWithYear(writtenAtUtc)}{submitted}"
                : $"{WrittenBy}{note.AuthorDisplayName}{submitted}");
        if (note.LastEdit is { } lastEdit)
        {
            yield return new(
                ReportBlockKind.Line,
                $"{Edited}{note.Current.VersionNumber}{VersionsLastChangeBy}{lastEdit.By}, "
                + ReportDates.DateTimeWithYear(lastEdit.AtUtc));
        }

        if (note.Flag is { } flag)
        {
            yield return new(
                ReportBlockKind.Line,
                $"{FlaggedStart}{flag.Reason}{FlaggedEnd}{(note.Current.IsFlagged ? string.Empty : FlagRemoved)}");
            var review = flag.Review;
            yield return new(
                ReportBlockKind.Line,
                review is null
                    ? ToReview
                    : ReviewedBy + review.ReviewedBy.DisplayName + On
                        + ReportDates.DateTimeWithYear(review.ReviewedAtUtc));
            if (review?.Comment is { Length: > 0 } comment)
            {
                yield return new(ReportBlockKind.Line, Comment + comment);
            }
        }

        foreach (var block in VersionBody(note.Current, ReportBlockKind.Heading2, ReportBlockKind.Heading3))
        {
            yield return block;
        }
    }

    /// <summary>
    /// A version's goals, its common items by group, as the version shows them, and its Guided notes exactly as
    /// stored. The section labels are blocks of <paramref name="sectionKind"/> and the group names of
    /// <paramref name="groupKind"/>, so a note's current version can use headings and an earlier version, which sits
    /// under a heading of its own, plain lines.
    /// </summary>
    public static IEnumerable<ReportBlock> VersionBody(
        NoteReadVersion version, ReportBlockKind sectionKind, ReportBlockKind groupKind)
    {
        yield return new(sectionKind, Goals);
        if (version.Goals.Count == 0)
        {
            yield return new(ReportBlockKind.Line, NoGoals);
        }

        foreach (var goal in version.Goals)
        {
            yield return new(ReportBlockKind.Tick, goal.Text, goal.IsTicked);
        }

        yield return new(sectionKind, CommonItems);
        if (version.CommonItemGroups.Count == 0)
        {
            yield return new(ReportBlockKind.Line, NoCommonItems);
        }

        foreach (var group in version.CommonItemGroups)
        {
            yield return new(groupKind, group.Name);
            foreach (var item in group.Items)
            {
                yield return new(ReportBlockKind.Tick, item.Text, item.IsTicked);
            }
        }

        yield return new(sectionKind, GuidedNotes);
        yield return new(ReportBlockKind.Line, version.Narrative);
    }
}
