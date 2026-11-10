using Grow2Notes.Web.Features.Notes;

namespace Grow2Notes.Web.Features.Reports;

/// <summary>
/// What a report file says and in what order (design.md §11.3), as <see cref="ReportBlock"/>s that both renderers read.
/// The file's own words are constants here, outside the SPA's copy, as the file is not a screen. The words that a note
/// shares with its screens are <see cref="NoteWords"/>.
/// </summary>
internal static class ReportLayout
{
    private const string DailyTitle = "SUPPORT NOTES";

    // An en dash, as the Word template's title has it.
    private const string WeekLabel = " – WEEK ";

    private const string DateLabel = "DATE: ";

    private const string StaffLabel = "STAFF:";

    private const string Participants = "Participants";

    private const string NameLabel = "NAME: ";

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

    private const string NoteWrittenBy = "Note written by: ";

    private const string PastDayManagerWritten = " (manager) · Past-day note, written ";

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

    private const string FooterStart = "Confidential: health information. Copy exported from Grow2Notes by ";

    private const string FooterEnd = ". The record in Grow2Notes is authoritative.";

    /// <summary>The words before the page number, as both renderers print them.</summary>
    public const string FooterPage = " Page ";

    /// <summary>The words between the page number and the number of pages.</summary>
    public const string FooterOf = " of ";

    /// <summary>
    /// The day's report, as the provider's Word template has it (D104): the running header, the footer sentence and
    /// the properties, then the title block, which is the title with the week when one is chosen, the day, the staff on
    /// shift, and who exported it and when; a Participants index; and each participant's note on a page of its own,
    /// under their name in capitals, in the model's order. The organisation is the file's property alone.
    /// </summary>
    /// <param name="model">The day's notes.</param>
    /// <param name="week"><c>A</c> or <c>B</c>, which the title prints, or <c>null</c> for no label.</param>
    public static ReportDocument DailyReport(DailyReportModel model, string? week)
    {
        var asAt = ReportDates.DateTimeWithYear(model.AsAtUtc);
        var day = ReportDates.DayDotted(model.NoteDate);
        var title = week is null ? DailyTitle : DailyTitle + WeekLabel + week;
        var staff = model.StaffOnShift;
        List<ReportBlock> blocks =
        [
            new(ReportBlockKind.Title, title),
            new(ReportBlockKind.Subtitle, DateLabel + day),
            new(ReportBlockKind.Subtitle, staff.Count == 0 ? StaffLabel : $"{StaffLabel} {string.Join(", ", staff)}"),
            new(ReportBlockKind.Subtitle, AsAt + asAt),
            new(ReportBlockKind.Subtitle, ExportedBy + model.ExportedBy),
            new(ReportBlockKind.Heading1, Participants),
        ];

        // The anchors are named by position, so that no participant's name is in the file's internals.
        for (var i = 0; i < model.Notes.Count; i++)
        {
            blocks.Add(new(ReportBlockKind.IndexEntry, model.Notes[i].ParticipantName.ToUpperInvariant())
            {
                Anchor = $"p{i + 1}",
            });
        }

        for (var i = 0; i < model.Notes.Count; i++)
        {
            var note = model.Notes[i];
            blocks.Add(new(ReportBlockKind.Heading1, NameLabel + note.ParticipantName.ToUpperInvariant())
            {
                PageBreakBefore = true,
                Anchor = $"p{i + 1}",
            });
            blocks.AddRange(NoteBody(note));
        }

        return new ReportDocument(
            $"{title}: {day}",
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

            foreach (var block in VersionBody(version.Content, ReportBlockKind.Line))
            {
                yield return block;
            }
        }
    }

    /// <summary>
    /// A note under its participant's name, in the order of the Word template and then of design.md §11.3's layout
    /// example: its version's body; who wrote it and when it was first submitted, with the day it was written for a
    /// past-day note; that it was edited, by whom and when; its flag, and whether a manager has reviewed it. The
    /// submitted time carries its day, so a note first submitted on a later day already shows its own date.
    /// </summary>
    public static IEnumerable<ReportBlock> NoteBody(DailyReportNote note)
    {
        foreach (var block in VersionBody(note.Current, ReportBlockKind.Heading2))
        {
            yield return block;
        }

        var submitted = Submitted + ReportDates.DateTimeShort(note.FirstSubmittedAtUtc);
        yield return new(
            ReportBlockKind.Line,
            note.PastDayWrittenAtUtc is { } writtenAtUtc
                ? $"{NoteWrittenBy}{note.AuthorDisplayName}{PastDayManagerWritten}"
                    + $"{ReportDates.DateTimeWithYear(writtenAtUtc)}{submitted}"
                : $"{NoteWrittenBy}{note.AuthorDisplayName}{submitted}");
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
    }

    /// <summary>
    /// A version as the Word template has it: the three attendance boxes; its goals under their term, or that it has
    /// none; its Medication, when the note has the box; each of its common item groups under its own name, with the
    /// details written for an item after it; and its Daily Support Notes exactly as stored. The headings are blocks of
    /// <paramref name="labelKind"/>, so a note's current version can use headings and an earlier version, which sits
    /// under a heading of its own, plain lines.
    /// </summary>
    public static IEnumerable<ReportBlock> VersionBody(NoteReadVersion version, ReportBlockKind labelKind)
    {
        yield return new(ReportBlockKind.TickRow, string.Empty)
        {
            Ticks =
            [
                new(NoteWords.NotIn, version.IsNotIn),
                new(NoteWords.LateNotice, version.IsLateNotice),
                new(NoteWords.Sick, version.IsSick),
            ],
        };

        if (version.Goals.Count == 0)
        {
            yield return new(ReportBlockKind.Line, NoteWords.NoGoals);
        }

        foreach (var term in new[]
        {
            (Heading: NoteWords.ShortTermGoals, IsMediumOrLong: false),
            (Heading: NoteWords.MediumOrLongTermGoal, IsMediumOrLong: true),
        })
        {
            var goals = version.Goals.Where(goal => goal.IsMediumOrLongTerm == term.IsMediumOrLong).ToList();
            if (goals.Count == 0)
            {
                continue;
            }

            yield return new(labelKind, term.Heading);
            foreach (var goal in goals)
            {
                yield return new(ReportBlockKind.Tick, goal.Text, goal.IsTicked);
            }
        }

        if (version.Medication is { } medication)
        {
            yield return new(labelKind, NoteWords.Medication);
            if (medication.Length > 0)
            {
                yield return new(ReportBlockKind.Line, medication);
            }
        }

        if (version.CommonItemGroups.Count == 0)
        {
            yield return new(ReportBlockKind.Line, NoteWords.NoCommonItems);
        }

        foreach (var group in version.CommonItemGroups)
        {
            yield return new(labelKind, group.Name);
            foreach (var item in group.Items)
            {
                yield return new(ReportBlockKind.Tick, item.Text, item.IsTicked);
                if (item.Detail is { Length: > 0 } detail)
                {
                    yield return new(ReportBlockKind.Line, $"{NoteWords.Details}: {detail}");
                }
            }
        }

        yield return new(labelKind, NoteWords.DailySupportNotes);
        yield return new(ReportBlockKind.Line, version.Narrative);
    }
}
