using Grow2Notes.Web.Features.Notes;
using Grow2Notes.Web.Features.Reports;

namespace Grow2Notes.Tests.Features.Reports;

/// <summary>
/// What the report files say and in what order (design.md §11.3, D104): the daily report's title block, STAFF, index
/// and one block per participant in the Word template's order, and the participant record, which takes the same note
/// body and keeps its own title block, its dates and no index or page breaks. The blocks are compared as short strings
/// (<see cref="Describe"/>) so that an order is easy to read.
/// </summary>
public sealed class ReportLayoutTests
{
    private const string Nbsp = " ";

    // The title and its six subtitles, which come before the first note in a participant's record.
    private const int TitleBlockLength = 7;

    private static readonly Guid PriyaId = new("00000000-0000-0000-0000-000000000001");

    private static readonly Guid JoId = new("00000000-0000-0000-0000-000000000002");

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
        var document = ReportLayout.DailyReport(Model, null);

        Assert.Equal(
            $"Confidential: health information. Copy exported from Grow2Notes by Jo Smith, Fri 2{Nbsp}Oct 2026, "
            + $"9:14{Nbsp}am. The record in Grow2Notes is authoritative.",
            document.Footer);
        Assert.Equal("SUPPORT NOTES: Thursday 01.10.2026", document.Title);

        var subtitles = document.Blocks.Where(block => block.Kind == ReportBlockKind.Subtitle)
            .Select(block => block.Text)
            .ToList();
        var exportedBy = Assert.Single(subtitles, text => text.StartsWith("Exported by ", StringComparison.Ordinal));
        var asAt = Assert.Single(subtitles, text => text.StartsWith("As at ", StringComparison.Ordinal));
        Assert.Contains(exportedBy["Exported by ".Length..] + ", " + asAt["As at ".Length..] + ".", document.Footer);
    }

    [Theory]
    [InlineData(null, "SUPPORT NOTES", "SUPPORT NOTES: Thursday 01.10.2026")]
    [InlineData("A", "SUPPORT NOTES – WEEK A", "SUPPORT NOTES – WEEK A: Thursday 01.10.2026")]
    [InlineData("B", "SUPPORT NOTES – WEEK B", "SUPPORT NOTES – WEEK B: Thursday 01.10.2026")]
    public void The_title_is_SUPPORT_NOTES_with_the_week_after_an_en_dash_and_the_document_title_names_the_day(
        string? week, string title, string documentTitle)
    {
        var document = ReportLayout.DailyReport(Model, week);

        Assert.Equal(new ReportBlock(ReportBlockKind.Title, title), document.Blocks[0]);
        Assert.Equal(documentTitle, document.Title);
    }

    [Fact]
    public void The_title_block_is_the_title_the_date_the_staff_as_at_and_exported_by_in_that_order()
    {
        var document = ReportLayout.DailyReport(Model with { Notes = [Note("Zoë Adams", PriyaId, "Priya Nair")] }, "B");

        Assert.Equal(
            [
                "Title: SUPPORT NOTES – WEEK B",
                "Subtitle: DATE: Thursday 01.10.2026",
                "Subtitle: STAFF: Priya Nair",
                $"Subtitle: As at Fri 2{Nbsp}Oct 2026, 9:14{Nbsp}am",
                "Subtitle: Exported by Jo Smith",
            ],
            document.Blocks.Take(5).Select(Describe));
    }

    [Fact]
    public void The_organisation_is_the_file_s_property_alone()
    {
        var model = Model with { Notes = [Note("Zoë Adams", PriyaId, "Priya Nair")] };
        var document = ReportLayout.DailyReport(model, null);

        Assert.Equal("Example Care", document.OrganisationName);
        Assert.DoesNotContain("Example Care", string.Join('\n', document.Blocks.Select(block => block.Text)));
        Assert.DoesNotContain("Example Care", document.Title);
    }

    // A157: each author once by user ID, so two people with one name both show; ordered by name without regard to
    // case; a manager's past-day note does not make its writer staff on shift.
    [Fact]
    public void The_staff_are_the_day_s_authors_once_each_in_name_order_and_without_past_day_writers()
    {
        var sam = Guid.NewGuid();
        var otherSam = Guid.NewGuid();
        var manager = Guid.NewGuid();
        var model = Model with
        {
            Notes =
            [
                Note("Ann Zed", sam, "Sam Lee"),
                Note("Bob Adams", otherSam, "Sam Lee"),
                Note("Cy Young", PriyaId, "priya nair"),
                Note("Di Park", PriyaId, "priya nair"),
                Note("Ed Wong", JoId, "Jo Smith"),
                Note("Flo Reid", manager, "Alex Manager", pastDay: true),
                Note("Gus Hill", Guid.NewGuid(), "Alex Pham"),
            ],
        };

        var staff = StaffRow(model);

        Assert.Equal("STAFF: Alex Pham, Jo Smith, priya nair, Sam Lee, Sam Lee", staff.Text);
        Assert.Equal(["Alex Pham", "Jo Smith", "priya nair", "Sam Lee", "Sam Lee"], model.StaffOnShift);
    }

    [Fact]
    public void The_staff_row_is_the_label_alone_when_every_note_is_a_past_day_note()
    {
        var model = Model with { Notes = [Note("Zoë Adams", JoId, "Jo Smith", pastDay: true)] };

        var staff = StaffRow(model);

        Assert.Equal("STAFF:", staff.Text);
    }

    [Fact]
    public void The_index_has_one_entry_per_participant_whose_anchor_is_that_of_the_participant_s_page_break_heading()
    {
        var model = Model with
        {
            Notes =
            [
                Note("Ann Zed", PriyaId, "Priya Nair"),
                Note("Zoë Adams", JoId, "Jo Smith"),
                Note("Bob Adams", PriyaId, "Priya Nair"),
            ],
        };

        var blocks = ReportLayout.DailyReport(model, null).Blocks.ToList();

        var participants = Assert.Single(blocks, block => block.Text == "Participants");
        Assert.Equal(ReportBlockKind.Heading1, participants.Kind);
        Assert.False(participants.PageBreakBefore);
        var entries = blocks.Where(block => block.Kind == ReportBlockKind.IndexEntry).ToList();
        var names = blocks.Where(block => block.Kind == ReportBlockKind.Heading1 && block.PageBreakBefore).ToList();
        Assert.Equal(["ANN ZED", "ZOË ADAMS", "BOB ADAMS"], entries.Select(entry => entry.Text));
        Assert.Equal(["NAME: ANN ZED", "NAME: ZOË ADAMS", "NAME: BOB ADAMS"], names.Select(name => name.Text));
        Assert.Equal(["p1", "p2", "p3"], entries.Select(entry => entry.Anchor));
        Assert.Equal(entries.Select(entry => entry.Anchor), names.Select(name => name.Anchor));
        Assert.Equal(
            Enumerable.Range(0, 3).Select(i => "NAME: " + entries[i].Text),
            names.Select(name => name.Text));
        Assert.True(blocks.IndexOf(entries[^1]) < blocks.IndexOf(names[0]));
    }

    // The Word template's order: attendance, goals by term, Medication, each group, Daily Support Notes, then who wrote
    // it. An unpicked group is in the daily report, with its items unticked, and a detail follows its item.
    [Fact]
    public void A_note_reads_in_the_word_template_s_order()
    {
        var note = Note("Zoë Adams", PriyaId, "Priya Nair", version: FullVersion());

        var lines = ReportLayout.NoteBody(note).Select(Describe);

        Assert.Equal(
            [
                "[ ] Not In  [x] Late Notice  [ ] Sick",
                "Heading2: Short term goals",
                "[x] Make her own lunch",
                "[ ] Walk to the shops",
                "Heading2: Medium or Long term goal",
                "[ ] Live alone",
                "Heading2: Medication",
                "Line: Took one tablet at 9.\nAnd a second “dose” at 3.",
                "Heading2: Every note",
                "[x] Meals",
                "Heading2: Community outing",
                "[ ] Bus",
                "[x] Other (staff to complete if different from above)",
                "Line: Details: Went by taxi",
                "Heading2: Unpicked group",
                "[ ] Haircut",
                "Heading2: Daily Support Notes",
                "Line: First paragraph.\nSecond paragraph.",
                $"Line: Note written by: Priya Nair · Submitted Thu 1{Nbsp}Oct, 4:42{Nbsp}pm",
            ],
            lines);
    }

    [Fact]
    public void A_note_with_no_goals_says_so_and_a_term_with_no_goals_has_no_heading()
    {
        var onlyMedium = FullVersion() with { Goals = [new("Live alone", false, true)] };
        var none = FullVersion() with { Goals = [], CommonItemGroups = [] };

        var mediumLines = ReportLayout.VersionBody(onlyMedium, ReportBlockKind.Heading2).Select(Describe).ToList();
        var noneLines = ReportLayout.VersionBody(none, ReportBlockKind.Heading2).Select(Describe).ToList();

        Assert.DoesNotContain("Heading2: Short term goals", mediumLines);
        Assert.Contains("Heading2: Medium or Long term goal", mediumLines);
        Assert.DoesNotContain("Line: No goals set", mediumLines);
        Assert.Equal("Line: No goals set", noneLines[1]);
        Assert.Contains("Line: No common items set", noneLines);
    }

    [Fact]
    public void Medication_with_no_text_is_its_heading_alone_and_without_the_box_it_is_not_there()
    {
        var empty = FullVersion() with { Medication = string.Empty };
        var absent = FullVersion() with { Medication = null };

        var emptyLines = ReportLayout.VersionBody(empty, ReportBlockKind.Heading2).Select(Describe).ToList();
        var absentLines = ReportLayout.VersionBody(absent, ReportBlockKind.Heading2).Select(Describe).ToList();

        var medication = emptyLines.IndexOf("Heading2: Medication");
        Assert.True(medication >= 0);
        Assert.Equal("Heading2: Every note", emptyLines[medication + 1]);
        Assert.DoesNotContain("Heading2: Medication", absentLines);
    }

    // A version saved before T44 has no ticks, no term, no Medication box and no details.
    [Fact]
    public void A_version_from_before_the_attendance_ticks_prints_empty_boxes_short_term_goals_and_no_medication()
    {
        var old = new NoteReadVersion(
            1,
            "Old text.",
            false,
            null,
            [new("Make her own lunch", true, false)],
            [new("Meals", true, [new("Shopping", true)])]);

        var lines = ReportLayout.VersionBody(old, ReportBlockKind.Heading2).Select(Describe);

        Assert.Equal(
            [
                "[ ] Not In  [ ] Late Notice  [ ] Sick",
                "Heading2: Short term goals",
                "[x] Make her own lunch",
                "Heading2: Meals",
                "[x] Shopping",
                "Heading2: Daily Support Notes",
                "Line: Old text.",
            ],
            lines);
    }

    [Fact]
    public void A_past_day_note_names_its_manager_and_when_it_was_written()
    {
        var note = Note(
            "Zoë Adams",
            JoId,
            "Jo Smith",
            pastDay: true,
            version: new NoteReadVersion(1, "Text", false, null, [], []));

        var written = ReportLayout.NoteBody(note).Last();

        Assert.Equal(
            $"Note written by: Jo Smith (manager) · Past-day note, written Sat 3{Nbsp}Oct 2026, 10:14{Nbsp}am · "
            + $"Submitted Sat 3{Nbsp}Oct, 10:20{Nbsp}am",
            written.Text);
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
            PriyaId,
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
                "No goals set",
                "No common items set",
                "Text",
                $"Note written by: Priya Nair · Submitted Thu 1{Nbsp}Oct, 4:42{Nbsp}pm",
                "Flagged for manager: \"Seemed tired all day.\" (flag removed in a later edit)",
                $"Reviewed by Jo Smith on Thu 1{Nbsp}Oct 2026, 6:30{Nbsp}pm",
            ],
            lines);
    }

    [Fact]
    public void An_edited_note_s_edited_and_flag_lines_follow_who_wrote_it()
    {
        var note = Note("Zoë Adams", PriyaId, "Priya Nair", edited: true)
            with { Flag = new DailyReportFlag("Knee sore.", null) };

        var lines = ReportLayout.NoteBody(note).Select(Describe).TakeLast(4);

        Assert.Equal(
            [
                $"Line: Note written by: Priya Nair · Submitted Thu 1{Nbsp}Oct, 4:42{Nbsp}pm",
                $"Line: Edited · 2 versions · last change by Jo Smith, Fri 2{Nbsp}Oct 2026, 11:00{Nbsp}am",
                "Line: Flagged for manager: \"Knee sore.\" (flag removed in a later edit)",
                "Line: To review",
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

    // The record keeps its dates and has neither an index nor a page break: it is read as a run of days.
    [Fact]
    public void A_participant_record_has_the_new_note_body_and_no_index_or_page_break()
    {
        var note = Note("Zoë Adams", PriyaId, "Priya Nair", version: FullVersion());

        var document = ReportLayout.ParticipantRecord(RecordModel(
            includesEarlierVersions: false, [Exported(note, []), Exported(Note(version: 1), [])]));

        Assert.DoesNotContain(document.Blocks, block => block.Kind == ReportBlockKind.IndexEntry);
        Assert.DoesNotContain(document.Blocks, block => block.PageBreakBefore || block.Anchor is not null);
        Assert.Equal(
            [$"Thursday 1{Nbsp}October 2026", $"Thursday 1{Nbsp}October 2026"],
            document.Blocks.Where(block => block.Kind == ReportBlockKind.Heading1).Select(block => block.Text));
        Assert.Equal(
            ReportLayout.NoteBody(note).Select(Describe),
            document.Blocks.Skip(TitleBlockLength + 1).Take(ReportLayout.NoteBody(note).Count()).Select(Describe));
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
                [new NoteReadGoal("Make her own lunch", true, false)],
                [new NoteReadGroup("Community outing", false, [new NoteReadItem("Bus", false)])],
                IsSick: true),
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
                $"Heading1: Thursday 1{Nbsp}October 2026",
                .. ReportLayout.NoteBody(note).Select(Describe),
                "Heading2: Earlier versions",
                $"Heading3: Version 2: saved by Priya Nair, Fri 2{Nbsp}Oct 2026, 9:01{Nbsp}am",
                "Line: Flagged for manager: \"Knee sore.\"",
                $"Line: Reviewed by Jo Smith on Fri 2{Nbsp}Oct 2026, 9:05{Nbsp}am",
                "Line: Comment: Booked a visit.",
                $"Line: Reviewed by Jo Smith on Fri 2{Nbsp}Oct 2026, 8:05{Nbsp}am",
                "[ ] Not In  [ ] Late Notice  [x] Sick",
                "Line: Short term goals",
                "[x] Make her own lunch",
                "Line: Community outing",
                "[ ] Bus",
                "Line: Daily Support Notes",
                "Line: Second text.",
                $"Heading3: Version 1: submitted by Alex Pham, Thu 1{Nbsp}Oct 2026, 4:42{Nbsp}pm",
                "[ ] Not In  [ ] Late Notice  [ ] Sick",
                "Line: No goals set",
                "Line: No common items set",
                "Line: Daily Support Notes",
                "Line: First text.",
            ],
            document.Blocks.Skip(TitleBlockLength).Select(Describe));
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
                (ReportBlockKind.Heading2, "Daily Support Notes"),
                (ReportBlockKind.Heading2, "Earlier versions"),
                (ReportBlockKind.Heading3, $"Version 1: submitted by Alex Pham, Thu 1{Nbsp}Oct 2026, 4:42{Nbsp}pm"),
                (ReportBlockKind.Heading1, $"Friday 2{Nbsp}October 2026"),
                (ReportBlockKind.Heading2, "Daily Support Notes"),
            ],
            document.Blocks.Skip(TitleBlockLength)
                .Where(block => block.Kind is >= ReportBlockKind.Heading1 and <= ReportBlockKind.Heading3)
                .Select(block => (block.Kind, block.Text)));
    }

    private static string Describe(ReportBlock block) => block.Kind switch
    {
        ReportBlockKind.Tick => $"{Box(block.IsTicked)} {block.Text}",
        ReportBlockKind.TickRow => string.Join("  ", block.Ticks.Select(tick => $"{Box(tick.IsTicked)} {tick.Text}")),
        _ => $"{block.Kind}: {block.Text}",
    };

    private static ReportBlock StaffRow(DailyReportModel model) => Assert.Single(
        ReportLayout.DailyReport(model, null).Blocks,
        block => block.Text.StartsWith("STAFF:", StringComparison.Ordinal));

    private static string Box(bool isTicked) => isTicked ? "[x]" : "[ ]";

    // Two goals of each term; the Every note group, a picked group whose last item asked for details, and a group that
    // was not picked, whose item is unticked. The Medication has two paragraphs and curly quotes.
    private static NoteReadVersion FullVersion() => new(
        1,
        "First paragraph.\nSecond paragraph.",
        false,
        null,
        [
            new("Make her own lunch", true, false),
            new("Live alone", false, true),
            new("Walk to the shops", false, false),
        ],
        [
            new("Every note", true, [new("Meals", true)]),
            new(
                "Community outing",
                false,
                [
                    new("Bus", false),
                    new("Other (staff to complete if different from above)", true, "Went by taxi"),
                ]),
            new("Unpicked group", false, [new("Haircut", false)]),
        ],
        IsNotIn: false,
        IsLateNotice: true,
        IsSick: false,
        Medication: "Took one tablet at 9.\nAnd a second “dose” at 3.");

    // A note for 1 October by the given author, as the daily report holds it; a past-day note was written on 3 October.
    private static DailyReportNote Note(
        string participant,
        Guid authorId,
        string author,
        bool pastDay = false,
        bool edited = false,
        NoteReadVersion? version = null) =>
        new(
            participant,
            author,
            authorId,
            pastDay ? new DateTime(2026, 10, 3, 0, 14, 0, DateTimeKind.Utc) : null,
            pastDay
                ? new DateTime(2026, 10, 3, 0, 20, 0, DateTimeKind.Utc)
                : new DateTime(2026, 10, 1, 6, 42, 0, DateTimeKind.Utc),
            version ?? new NoteReadVersion(edited ? 2 : 1, "Text.", false, null, [], []),
            edited ? new NoteReadLastEdit("Jo Smith", new DateTime(2026, 10, 2, 1, 0, 0, DateTimeKind.Utc)) : null,
            null);

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
            Guid.NewGuid(),
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
