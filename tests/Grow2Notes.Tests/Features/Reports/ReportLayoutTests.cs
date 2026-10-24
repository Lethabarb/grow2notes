using Grow2Notes.Web.Features.Notes;
using Grow2Notes.Web.Features.Reports;

namespace Grow2Notes.Tests.Features.Reports;

public sealed class ReportLayoutTests
{
    private const string Nbsp = " ";

    // The title and its six subtitles, which come before the first note.
    private const int TitleBlockLength = 7;

    // 23:14 UTC on 1 October is 9:14 am on 2 October in Melbourne.
    private static readonly DailyReportModel Model = new(
        "Example Care",
        new DateOnly(2026, 10, 1),
        new DateTime(2026, 10, 1, 23, 14, 0, DateTimeKind.Utc),
        "Jo Smith",
        []);

    [Fact]
    public void The_footer_names_the_exporter_and_time_of_the_title_block_and_the_title_names_the_day()
    {
        var document = ReportLayout.DailyReport(Model);

        Assert.Equal(
            $"Confidential: health information. Copy exported from Grow2Notes by Jo Smith, Fri 2{Nbsp}Oct 2026, "
            + $"9:14{Nbsp}am. The record in Grow2Notes is authoritative.",
            document.Footer);
        Assert.Equal($"Daily progress notes: Thu 1{Nbsp}Oct 2026", document.Title);
        Assert.Equal("Example Care", document.OrganisationName);

        var subtitles = document.Blocks.Where(block => block.Kind == ReportBlockKind.Subtitle)
            .Select(block => block.Text)
            .ToList();
        var exportedBy = Assert.Single(subtitles, text => text.StartsWith("Exported by ", StringComparison.Ordinal));
        var asAt = Assert.Single(subtitles, text => text.StartsWith("As at ", StringComparison.Ordinal));
        Assert.Contains(exportedBy["Exported by ".Length..] + ", " + asAt["As at ".Length..] + ".", document.Footer);
    }

    [Fact]
    public void A_reviewed_note_whose_flag_was_removed_and_whose_review_has_no_comment()
    {
        // Version 2 is unflagged, the review is of version 1 and the reviewer left no comment.
        var review = new NoteReadReview(
            1,
            new NoteReadAuthor(Guid.NewGuid(), "Jo Smith"),
            new DateTime(2026, 10, 1, 8, 30, 0, DateTimeKind.Utc),
            null);
        var note = new DailyReportNote(
            "Zoë Adams",
            "Priya Nair",
            null,
            new DateTime(2026, 10, 1, 6, 42, 0, DateTimeKind.Utc),
            new NoteReadVersion(2, "Text", false, null, [], []),
            null,
            new DailyReportFlag("Seemed tired all day.", review));

        var lines = ReportLayout.NoteBody(note)
            .Where(block => block.Kind == ReportBlockKind.Line)
            .Select(block => block.Text)
            .ToList();

        Assert.Equal(
            [
                $"Written by Priya Nair · Submitted Thu 1{Nbsp}Oct, 4:42{Nbsp}pm",
                "Flagged for manager: \"Seemed tired all day.\" (flag removed in a later edit)",
                $"Reviewed by Jo Smith on Thu 1{Nbsp}Oct 2026, 6:30{Nbsp}pm",
                "No goals set",
                "No common items set",
                "Text",
            ],
            lines);
    }

    [Theory]
    [InlineData(false, "Earlier versions: not included")]
    [InlineData(true, "Earlier versions: included")]
    public void A_participant_record_opens_with_the_title_block_and_says_whether_it_has_earlier_versions(
        bool includesEarlierVersions, string lastLine)
    {
        var document = ReportLayout.ParticipantRecord(RecordModel(includesEarlierVersions, []));

        Assert.Equal(
            [
                new ReportBlock(ReportBlockKind.Title, "Progress notes record"),
                new ReportBlock(ReportBlockKind.Subtitle, "Zoë Adams"),
                new ReportBlock(ReportBlockKind.Subtitle, $"Date of birth: 7{Nbsp}March 1987"),
                new ReportBlock(ReportBlockKind.Subtitle, $"1{Nbsp}January 2026 to 30{Nbsp}September 2026"),
                new ReportBlock(ReportBlockKind.Subtitle, "Example Care"),
                new ReportBlock(ReportBlockKind.Subtitle, $"Exported by Jo Smith, Fri 2{Nbsp}Oct 2026, 9:14{Nbsp}am"),
                new ReportBlock(ReportBlockKind.Subtitle, lastLine),
            ],
            document.Blocks);
    }

    [Fact]
    public void A_participant_record_s_title_footer_and_organisation_never_name_the_participant()
    {
        var document = ReportLayout.ParticipantRecord(RecordModel(includesEarlierVersions: false, []));

        Assert.Equal($"Progress notes record: 1{Nbsp}January 2026 to 30{Nbsp}September 2026", document.Title);
        Assert.Equal("Example Care", document.OrganisationName);
        Assert.Equal(
            $"Confidential: health information. Copy exported from Grow2Notes by Jo Smith, Fri 2{Nbsp}Oct 2026, "
            + $"9:14{Nbsp}am. The record in Grow2Notes is authoritative.",
            document.Footer);
        Assert.DoesNotContain("Zoë", document.Title + document.OrganisationName + document.Footer);
    }

    [Fact]
    public void A_note_without_earlier_versions_is_its_date_and_its_note_body_alone()
    {
        var note = Note(version: 1);

        var document = ReportLayout.ParticipantRecord(RecordModel(includesEarlierVersions: true, [Exported(note, [])]));

        Assert.Equal(
            [
                new ReportBlock(ReportBlockKind.Heading1, $"Thursday 1{Nbsp}October 2026"),
                .. ReportLayout.NoteBody(note),
            ],
            document.Blocks.Skip(TitleBlockLength));
    }

    // Version 3 is the note's current one. Version 2 was flagged and reviewed twice, once with a comment, and version 1
    // was not flagged: each flag and review shows under its own version and nowhere else, the note's own lines are
    // NoteBody's, and the version headings are the third level under the second-level Earlier versions.
    [Fact]
    public void An_edited_note_s_earlier_versions_come_newest_first_each_with_its_own_flag_and_reviews()
    {
        var note = Note(version: 3);
        var versionTwo = new ParticipantExportVersion(
            Summary(2, "Priya Nair", new DateTime(2026, 10, 1, 23, 1, 0, DateTimeKind.Utc), isFlagged: true),
            new NoteReadVersion(
                2,
                "Second text.",
                true,
                "Knee sore.",
                [new NoteReadItem("Make her own lunch", true)],
                [new NoteReadGroup("Community outing", false, [new NoteReadItem("Bus", false)])]),
            [
                new NoteReadReview(
                    2,
                    new NoteReadAuthor(Guid.NewGuid(), "Jo Smith"),
                    new DateTime(2026, 10, 1, 23, 5, 0, DateTimeKind.Utc),
                    "Booked a visit."),
                new NoteReadReview(
                    2,
                    new NoteReadAuthor(Guid.NewGuid(), "Jo Smith"),
                    new DateTime(2026, 10, 1, 22, 5, 0, DateTimeKind.Utc),
                    null),
            ]);
        var versionOne = new ParticipantExportVersion(
            Summary(1, "Alex Pham", new DateTime(2026, 10, 1, 6, 42, 0, DateTimeKind.Utc), isFlagged: false),
            new NoteReadVersion(1, "First text.", false, null, [], []),
            []);

        var document = ReportLayout.ParticipantRecord(
            RecordModel(includesEarlierVersions: true, [Exported(note, [versionTwo, versionOne])]));

        Assert.Equal(
            [
                new ReportBlock(ReportBlockKind.Heading1, $"Thursday 1{Nbsp}October 2026"),
                .. ReportLayout.NoteBody(note),
                new ReportBlock(ReportBlockKind.Heading2, "Earlier versions"),
                new ReportBlock(
                    ReportBlockKind.Heading3, $"Version 2: saved by Priya Nair, Fri 2{Nbsp}Oct 2026, 9:01{Nbsp}am"),
                new ReportBlock(ReportBlockKind.Line, "Flagged for manager: \"Knee sore.\""),
                new ReportBlock(ReportBlockKind.Line, $"Reviewed by Jo Smith on Fri 2{Nbsp}Oct 2026, 9:05{Nbsp}am"),
                new ReportBlock(ReportBlockKind.Line, "Comment: Booked a visit."),
                new ReportBlock(ReportBlockKind.Line, $"Reviewed by Jo Smith on Fri 2{Nbsp}Oct 2026, 8:05{Nbsp}am"),
                new ReportBlock(ReportBlockKind.Line, "Goals"),
                new ReportBlock(ReportBlockKind.Tick, "Make her own lunch", true),
                new ReportBlock(ReportBlockKind.Line, "Common items"),
                new ReportBlock(ReportBlockKind.Line, "Community outing"),
                new ReportBlock(ReportBlockKind.Tick, "Bus"),
                new ReportBlock(ReportBlockKind.Line, "Guided notes"),
                new ReportBlock(ReportBlockKind.Line, "Second text."),
                new ReportBlock(
                    ReportBlockKind.Heading3,
                    $"Version 1: submitted by Alex Pham, Thu 1{Nbsp}Oct 2026, 4:42{Nbsp}pm"),
                new ReportBlock(ReportBlockKind.Line, "Goals"),
                new ReportBlock(ReportBlockKind.Line, "No goals set"),
                new ReportBlock(ReportBlockKind.Line, "Common items"),
                new ReportBlock(ReportBlockKind.Line, "No common items set"),
                new ReportBlock(ReportBlockKind.Line, "Guided notes"),
                new ReportBlock(ReportBlockKind.Line, "First text."),
            ],
            document.Blocks.Skip(TitleBlockLength));
    }

    // The note's own block keeps NoteBody's second and third level headings, a note with no earlier version has no
    // Earlier versions heading although the record includes them, and the next note starts at its own date.
    [Fact]
    public void The_headings_of_a_record_run_from_the_date_to_the_note_s_sections_and_its_earlier_versions()
    {
        var edited = Note(version: 2);
        var earlier = new ParticipantExportVersion(
            Summary(1, "Alex Pham", new DateTime(2026, 10, 1, 6, 42, 0, DateTimeKind.Utc), isFlagged: false),
            new NoteReadVersion(1, "First text.", false, null, [], []),
            []);

        var document = ReportLayout.ParticipantRecord(RecordModel(
            includesEarlierVersions: true,
            [
                Exported(edited, [earlier]),
                new ParticipantExportNote(new DateOnly(2026, 10, 2), Note(version: 1), []),
            ]));

        Assert.Equal(
            [
                (ReportBlockKind.Heading1, $"Thursday 1{Nbsp}October 2026"),
                (ReportBlockKind.Heading2, "Goals"),
                (ReportBlockKind.Heading2, "Common items"),
                (ReportBlockKind.Heading2, "Guided notes"),
                (ReportBlockKind.Heading2, "Earlier versions"),
                (ReportBlockKind.Heading3, $"Version 1: submitted by Alex Pham, Thu 1{Nbsp}Oct 2026, 4:42{Nbsp}pm"),
                (ReportBlockKind.Heading1, $"Friday 2{Nbsp}October 2026"),
                (ReportBlockKind.Heading2, "Goals"),
                (ReportBlockKind.Heading2, "Common items"),
                (ReportBlockKind.Heading2, "Guided notes"),
            ],
            document.Blocks.Skip(TitleBlockLength)
                .Where(block => block.Kind is >= ReportBlockKind.Heading1 and <= ReportBlockKind.Heading3)
                .Select(block => (block.Kind, block.Text)));
    }

    private static ParticipantExportModel RecordModel(
        bool includesEarlierVersions, IReadOnlyList<ParticipantExportNote> notes) =>
        new(
            "Example Care",
            "Zoë Adams",
            new DateOnly(1987, 3, 7),
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 9, 30),
            includesEarlierVersions,
            new DateTime(2026, 10, 1, 23, 14, 0, DateTimeKind.Utc),
            "Jo Smith",
            notes);

    // A note for 1 October whose current version is the given one, last saved by Jo Smith if it was edited.
    private static DailyReportNote Note(int version) =>
        new(
            "Zoë Adams",
            "Alex Pham",
            null,
            new DateTime(2026, 10, 1, 6, 42, 0, DateTimeKind.Utc),
            new NoteReadVersion(version, "Current text.", false, null, [], []),
            version > 1 ? new NoteReadLastEdit("Jo Smith", new DateTime(2026, 10, 2, 1, 0, 0, DateTimeKind.Utc)) : null,
            null);

    private static ParticipantExportNote Exported(
        DailyReportNote note, IReadOnlyList<ParticipantExportVersion> earlier) =>
        new(new DateOnly(2026, 10, 1), note, earlier);

    private static NoteVersionSummary Summary(int number, string savedBy, DateTime atUtc, bool isFlagged) =>
        new(number, new NoteReadAuthor(Guid.NewGuid(), savedBy), atUtc, isFlagged);
}
