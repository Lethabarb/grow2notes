using System.Diagnostics;
using System.Text.RegularExpressions;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Features.Reports;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Time.Testing;
using UglyToad.PdfPig;

namespace Grow2Notes.Tests.Features.Reports;

/// <summary>
/// How long a manager waits for a participant's record of a year (S05.01.04, design.md §7.1): a made-up year (A35) of
/// 365 notes of one participant, one for every day from 2 October 2025 to 1 October 2026, each with three goals, Every
/// note and two of three groups picked, and about 2,000 characters of guided notes, which is well under
/// <c>Limits.GuidedNotes</c>. Every 5th note is edited and every 15th edited twice, each edit with a change of picked
/// groups; every 10th is flagged, and half of those are reviewed with a comment. Each format is downloaded once with
/// earlier versions, Word first, through the real endpoint on a host that has already served a one-day record. The
/// ceiling is 30 seconds against a time that is expected to be a few seconds, a margin that shared build machines,
/// which run other test collections at the same time, can be trusted to keep. Both timings go to the test output so
/// that they can be quoted in the task's notes. The files are checked for their structure, and for the exact text of
/// three sample days; the exact full text of a small record is <see cref="ParticipantExportTextMatchTests"/>'s.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class ParticipantExportTimingTests(Grow2NotesFactory factory) : IClassFixture<Grow2NotesFactory>
{
    // A no-break space, which the file puts between a day and its month and between a time and am or pm.
    private const string Nb = " ";

    private const int NoteCount = 365;

    private const string Sentence = "Jane had a quiet morning and ate her breakfast without help. ";

    private static readonly TimeSpan Ceiling = TimeSpan.FromSeconds(30);

    // 9:14 am on Friday 2 October 2026 in Melbourne, before the daylight-saving change on the 4th.
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 23, 14, 0, TimeSpan.Zero);

    private static readonly DateOnly Start = new(2025, 10, 2);

    private static readonly DateOnly End = Start.AddDays(NoteCount - 1);

    [Fact]
    public async Task A_year_of_notes_with_earlier_versions_downloads_in_under_30_seconds_in_each_format()
    {
        var organisation = await factory.Services.AddOrganisationAsync();
        var priya = await factory.Services.AddUserAsync(organisation, UserRole.Worker, "Priya Nair");
        var jo = await factory.Services.AddUserAsync(organisation, UserRole.Manager, "Jo Smith");
        var groups = await AddCommonItemsAsync(organisation);
        var participant = await factory.Services.AddParticipantAsync(
            organisation, givenName: "Zoë", familyName: "Adams");
        var goals = await factory.Services.AddGoalsAsync(
            organisation,
            participant.Id,
            new TestGoal("Catch the bus to the library on her own", 1),
            new TestGoal("Make her own lunch", 2),
            new TestGoal("Call a friend each week", 3));
        var editedNotes = 0;
        var earlierVersions = 0;
        for (var number = 1; number <= NoteCount; number++)
        {
            await AddNoteAsync(organisation, participant.Id, priya, jo, groups, goals, number);
            editedNotes += number % 5 == 0 ? 1 : 0;
            earlierVersions += number % 15 == 0 ? 2 : number % 5 == 0 ? 1 : 0;
        }

        await using var app = factory.WithWebHostBuilder(builder => builder.UseClock(new FakeTimeProvider(Now)));

        // The client starts the host, and a one-day record is the warm-up, so that the timings are the year's alone.
        using var client = app.CreateHttpsClient().SignInAs(jo);
        await client.GetByteArrayAsync(
            Address(participant.Id, End, End, "docx"), TestContext.Current.CancellationToken);

        var (docx, docxTime) = await TimeAsync(client, participant.Id, "docx");
        var (pdf, pdfTime) = await TimeAsync(client, participant.Id, "pdf");

        TestContext.Current.TestOutputHelper?.WriteLine(
            $"Record of {NoteCount} notes with earlier versions: docx {docxTime.TotalMilliseconds:N0} ms, "
            + $"pdf {pdfTime.TotalMilliseconds:N0} ms.");
        Assert.True(docxTime < Ceiling, $"The Word file took {docxTime.TotalSeconds:N1} s.");
        Assert.True(pdfTime < Ceiling, $"The PDF took {pdfTime.TotalSeconds:N1} s.");
        AssertWordStructure(docx, editedNotes, earlierVersions);
        AssertWordSamples(ReportText.WordParagraphs(docx));
        AssertPdfStructure(pdf, editedNotes, earlierVersions);
    }

    private static string Address(Guid participantId, DateOnly from, DateOnly to, string format) =>
        $"/api/reports/participants/{participantId}/export?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}"
        + $"&format={format}&includeHistory=true";

    private static async Task<(byte[] Bytes, TimeSpan Time)> TimeAsync(
        HttpClient client, Guid participantId, string format)
    {
        var stopwatch = Stopwatch.StartNew();
        var bytes = await client.GetByteArrayAsync(
            Address(participantId, Start, End, format), TestContext.Current.CancellationToken);
        stopwatch.Stop();

        Assert.NotEmpty(bytes);
        return (bytes, stopwatch.Elapsed);
    }

    // One note heading for every day, in date order; one Earlier versions heading for every edited note; and one
    // version heading for every earlier version that the seed added.
    private static void AssertWordStructure(byte[] docx, int editedNotes, int earlierVersions)
    {
        var paragraphs = ReportText.WordStyledParagraphs(docx);

        Assert.Equal(
            Enumerable.Range(0, NoteCount).Select(day => ReportDates.DateLong(Start.AddDays(day))),
            paragraphs.Where(paragraph => paragraph.Style == "Heading1").Select(paragraph => paragraph.Text));
        Assert.Equal(
            editedNotes, paragraphs.Count(paragraph => paragraph is { Style: "Heading2", Text: "Earlier versions" }));
        Assert.Equal(
            earlierVersions,
            paragraphs.Count(paragraph =>
                paragraph.Style == "Heading3"
                && Regex.IsMatch(paragraph.Text, @"^Version \d+: (saved|submitted) by ")));
    }

    // The PDF's words in order: the same days in date order, and the same counts of the headings that are words.
    private static void AssertPdfStructure(byte[] pdf, int editedNotes, int earlierVersions)
    {
        var text = ReportText.Normalised(string.Join(' ', ReportText.PdfWords(pdf)));
        var position = 0;
        for (var day = 0; day < NoteCount; day++)
        {
            var heading = ReportText.Normalised(ReportDates.DateLong(Start.AddDays(day)));
            var found = text.IndexOf(heading, position, StringComparison.Ordinal);
            Assert.True(found >= position, $"The heading {heading} is not in the PDF, in order.");
            position = found + heading.Length;
        }

        Assert.Equal(editedNotes, Regex.Count(text, @"Earlier versions(?!:)"));
        Assert.Equal(earlierVersions, Regex.Count(text, @"Version \d+: (saved|submitted) by "));
        using var document = PdfDocument.Open(pdf);
        Assert.True(document.NumberOfPages > 1);
    }

    // The exact paragraphs of three days: a note with one version, a note edited once and flagged, and a note edited
    // twice. All three are in the winter, when Melbourne is 10 hours ahead of UTC.
    private static void AssertWordSamples(IReadOnlyList<string> paragraphs)
    {
        string[] plain =
        [
            $"Wednesday 15{Nb}April 2026",
            $"Written by Priya Nair · Submitted Wed 15{Nb}Apr, 3:00{Nb}pm",
            .. GoalLines(196),
            "Common items",
            .. CommonItemLines(outing: true, care: true, household: false),
            "Guided notes",
            Narrative(196, 1),
        ];
        string[] editedAndFlagged =
        [
            $"Tuesday 19{Nb}May 2026",
            $"Written by Priya Nair · Submitted Tue 19{Nb}May, 3:00{Nb}pm",
            $"Edited · 2 versions · last change by Priya Nair, Tue 19{Nb}May 2026, 5:00{Nb}pm",
            "Flagged for manager: \"Mentioned pain in her left knee, note 230.\"",
            "To review",
            "Goals",
            "No goals set",
            "Common items",
            .. CommonItemLines(outing: false, care: true, household: true),
            "Guided notes",
            Narrative(230, 2),
            "Earlier versions",
            $"Version 1: submitted by Priya Nair, Tue 19{Nb}May 2026, 3:00{Nb}pm",
            "Flagged for manager: \"Mentioned pain in her left knee, note 230.\"",
            .. GoalLines(230),
            "Common items",
            .. CommonItemLines(outing: true, care: true, household: false),
            "Guided notes",
            Narrative(230, 1),
        ];
        string[] editedTwice =
        [
            $"Thursday 14{Nb}May 2026",
            $"Written by Priya Nair · Submitted Thu 14{Nb}May, 3:00{Nb}pm",
            $"Edited · 3 versions · last change by Priya Nair, Thu 14{Nb}May 2026, 6:00{Nb}pm",
            "Goals",
            "No goals set",
            "Common items",
            .. CommonItemLines(outing: true, care: false, household: true),
            "Guided notes",
            Narrative(225, 3),
            "Earlier versions",
            $"Version 2: saved by Priya Nair, Thu 14{Nb}May 2026, 5:00{Nb}pm",
            "Goals",
            "No goals set",
            "Common items",
            .. CommonItemLines(outing: false, care: true, household: true),
            "Guided notes",
            Narrative(225, 2),
            $"Version 1: submitted by Priya Nair, Thu 14{Nb}May 2026, 3:00{Nb}pm",
            .. GoalLines(225),
            "Common items",
            .. CommonItemLines(outing: true, care: true, household: false),
            "Guided notes",
            Narrative(225, 1),
        ];

        Assert.Equal(plain, DayOf(paragraphs, new DateOnly(2026, 4, 15)));
        Assert.Equal(editedAndFlagged, DayOf(paragraphs, new DateOnly(2026, 5, 19)));
        Assert.Equal(editedTwice, DayOf(paragraphs, new DateOnly(2026, 5, 14)));
    }

    // The paragraphs from the day's heading up to the next day's, which every day has.
    private static IEnumerable<string> DayOf(IReadOnlyList<string> paragraphs, DateOnly date)
    {
        var first = paragraphs.ToList().IndexOf(ReportDates.DateLong(date));
        var next = paragraphs.ToList().IndexOf(ReportDates.DateLong(date.AddDays(1)));
        Assert.True(first >= 0 && next > first, $"{date} is not in the file, followed by the next day.");
        return paragraphs.Skip(first).Take(next - first);
    }

    // The three goals of version 1, the first reached.
    private static IEnumerable<string> GoalLines(int number) =>
        ["Goals", $"☑ Goal 1 of {number}", $"☐ Goal 2 of {number}", $"☐ Goal 3 of {number}"];

    // Every note and the picked groups, each with its first item done, as the seed gives them.
    private static IEnumerable<string> CommonItemLines(bool outing, bool care, bool household)
    {
        string[] every = ["Every note", "☑ Every note item 1", "☐ Every note item 2"];
        string[] outingLines = ["Community outing", "☑ Community outing item 1", "☐ Community outing item 2"];
        string[] careLines = ["Personal care", "☑ Personal care item 1", "☐ Personal care item 2"];
        string[] householdLines = ["Household tasks", "☑ Household tasks item 1"];
        return
        [
            .. every,
            .. outing ? outingLines : [],
            .. care ? careLines : [],
            .. household ? householdLines : [],
        ];
    }

    // About 2,000 characters, with the note and the version in its first words so that no two are alike. The cut can
    // leave a space at the end, which a paragraph need not keep.
    private static string Narrative(int number, int version) =>
        $"Note {number}, version {version}. {string.Concat(Enumerable.Repeat(Sentence, 40))}"[..2_000].TrimEnd();

    // The organisation's Every note group gets two items, and three more groups get two, two and one, so that every
    // note holds the same shape.
    private async Task<GroupIds> AddCommonItemsAsync(SeededOrganisation organisation)
    {
        var organisationId = organisation.OrganisationId;
        var every = await factory.Services.AddItemsAsync(
            organisationId,
            organisation.EveryNoteGroupId,
            new TestCommonItem("Medication prompted", 1),
            new TestCommonItem("Meal prepared", 2));
        var groupIds = await factory.Services.AddGroupsAsync(
            organisationId,
            new TestCommonItemGroup("Community outing", 1),
            new TestCommonItemGroup("Personal care", 2),
            new TestCommonItemGroup("Household tasks", 3));
        var outing = await factory.Services.AddItemsAsync(
            organisationId,
            groupIds[0],
            new TestCommonItem("Travelled by bus or train", 1),
            new TestCommonItem("Paid for own purchases", 2));
        var care = await factory.Services.AddItemsAsync(
            organisationId,
            groupIds[1],
            new TestCommonItem("Showered with prompting", 1),
            new TestCommonItem("Dressed independently", 2));
        var household = await factory.Services.AddItemsAsync(
            organisationId, groupIds[2], new TestCommonItem("Washed the dishes", 1));
        return new(organisation.EveryNoteGroupId, groupIds, every, outing, care, household);
    }

    // Note number 1 is 2 October 2025. Every 10th note is flagged and every 20th of those is reviewed by Jo with a
    // comment, before any edit. Every 5th is edited by the worker, its flag kept, and every 15th edited again, and
    // each edit changes the picked groups.
    private async Task AddNoteAsync(
        SeededOrganisation organisation,
        Guid participantId,
        Guid priya,
        Guid jo,
        GroupIds groups,
        IReadOnlyList<Guid> goals,
        int number)
    {
        var date = Start.AddDays(number - 1);
        var flagged = number % 10 == 0;
        var reviewed = number % 20 == 0;
        var flagReason = $"Mentioned pain in her left knee, note {number}.";
        var noteId = await factory.Services.AddSubmittedAsync(
            organisation,
            new(
                participantId,
                date,
                priya,
                UtcAt(date, 1),
                UtcAt(date, 5),
                Narrative(number, 1),
                flagged,
                flagged ? flagReason : null,
                reviewed ? FlagStatus.Reviewed : null,
                [.. goals.Select((goal, i) => new TestVersionGoal(goal, $"Goal {i + 1} of {number}", i == 0))],
                Shape(groups, outing: true, care: true, household: false)));
        if (reviewed)
        {
            await factory.Services.AddReviewAsync(
                organisation, noteId, 1, jo, UtcAt(date, 6), "Booked a physio visit.");
        }

        if (number % 5 == 0)
        {
            await factory.Services.AddVersionAsync(
                organisation,
                noteId,
                2,
                UtcAt(date, 7),
                flagged ? flagReason : null,
                flagged ? 2 : 1,
                Shape(groups, outing: false, care: true, household: true),
                priya,
                Narrative(number, 2));
        }

        if (number % 15 == 0)
        {
            await factory.Services.AddVersionAsync(
                organisation,
                noteId,
                3,
                UtcAt(date, 8),
                flagged ? flagReason : null,
                flagged ? 3 : 1,
                Shape(groups, outing: true, care: false, household: true),
                priya,
                Narrative(number, 3));
        }
    }

    // The time of the given hour in UTC on the date. In the winter Melbourne is 10 hours ahead, so hour 5 is 3 pm
    // there; in every season the hours 1 to 8 are on the same day there.
    private static DateTime UtcAt(DateOnly date, int hour) =>
        date.ToDateTime(new TimeOnly(hour, 0), DateTimeKind.Utc);

    // Every note, never picked, and the three groups, each as picked or not.
    private static IReadOnlyList<TestVersionGroup> Shape(GroupIds groups, bool outing, bool care, bool household) =>
    [
        Group(groups.EveryNote, "Every note", true, false, groups.Every),
        Group(groups.Others[0], "Community outing", false, outing, groups.Outing),
        Group(groups.Others[1], "Personal care", false, care, groups.Care),
        Group(groups.Others[2], "Household tasks", false, household, groups.Household),
    ];

    private static TestVersionGroup Group(
        Guid groupId, string name, bool isEveryNote, bool isPicked, IReadOnlyList<Guid> itemIds) =>
        new(
            groupId,
            name,
            isEveryNote,
            isPicked,
            [.. itemIds.Select((itemId, i) => new TestVersionItem(itemId, $"{name} item {i + 1}", i == 0))]);

    // The IDs of the live groups and items that the notes' versions point at.
    private sealed record GroupIds(
        Guid EveryNote,
        IReadOnlyList<Guid> Others,
        IReadOnlyList<Guid> Every,
        IReadOnlyList<Guid> Outing,
        IReadOnlyList<Guid> Care,
        IReadOnlyList<Guid> Household);
}
