using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Features.Reports;
using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using UglyToad.PdfPig;

namespace Grow2Notes.Tests.Features.Reports;

/// <summary>
/// What the participant record's Word and PDF files say (S05.01.01 to S05.01.04, design.md §11.6 and A93), for the
/// one participant, Zoë Adams, that the test adds to an organisation of its own (<see cref="TestOrganisations"/>)
/// with six submitted notes, and with a draft, a discarded draft, a note outside the range and another participant's
/// note that must not appear. Without earlier versions the Word file's body paragraphs equal the expected list
/// exactly, and the PDF's body words, white space aside, equal the same list's; with them, the same holds for the list
/// that adds each edited note's earlier versions, newest first, and the Word file's heading styles are checked. The
/// footer, the running header and the file properties are checked on the same files, and so is the PDF's font.
/// </summary>
[Collection<SqlServerCollection>]
public sealed partial class ParticipantExportTextMatchTests(Grow2NotesFactory factory) : IClassFixture<Grow2NotesFactory>
{
    // A no-break space, which the file puts between a day and its month and between a time and am or pm.
    private const string Nb = " ";

    private const string Query = "from=2026-09-01&to=2026-10-02";

    private const string ZoeNarrative = "Zoë said “I’m ready” — then we left…\n\nShe paid at the café herself.";

    private const string WrongPersonNarrative = "Sam Taylor walked to the shops with his support worker.";

    private const string InErrorNarrative = "Recorded against this participant in error. No support recorded here.";

    // 9:14 am on Friday 2 October 2026 in Melbourne, before the daylight-saving change on the 4th.
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 23, 14, 0, TimeSpan.Zero);

    [Fact]
    public async Task The_files_say_the_participant_s_notes_oldest_first_with_earlier_versions_only_when_asked()
    {
        var organisation = await factory.Services.AddOrganisationAsync();
        var priya = await factory.Services.AddUserAsync(organisation, UserRole.Worker, "Priya Nair");
        var jo = await factory.Services.AddUserAsync(organisation, UserRole.Manager, "Jo Smith");
        var items = await AddLiveCommonItemsAsync(organisation);
        var zoe = await factory.Services.AddParticipantAsync(organisation, givenName: "Zoë", familyName: "Adams");
        await AddNextDayNoteAsync(organisation, zoe.Id, priya);
        await AddFlaggedAndReviewedNoteAsync(organisation, zoe.Id, priya, jo);
        await AddEditedNoteAsync(organisation, zoe.Id, priya, items);
        await AddNoteWhoseFlagWasRemovedAsync(organisation, zoe.Id, priya, jo);
        await AddPastDayNoteAsync(organisation, zoe.Id, jo);
        await AddWrongParticipantNoteAsync(organisation, zoe.Id, priya);
        await AddNotesThatMustNotAppearAsync(organisation, zoe.Id, priya);
        var organisationName = await OrganisationNameAsync(organisation);
        await using var app = factory.WithWebHostBuilder(builder => builder.UseClock(new FakeTimeProvider(Now)));
        using var client = app.CreateHttpsClient().SignInAs(jo);
        var title = $"Progress notes record: 1{Nb}September 2026 to 2{Nb}October 2026";
        string[] block =
        [
            "Progress notes record",
            "Zoë Adams",
            $"Date of birth: 7{Nb}March 1987",
            $"1{Nb}September 2026 to 2{Nb}October 2026",
            organisationName,
            $"Exported by Jo Smith, Fri 2{Nb}Oct 2026, 9:14{Nb}am",
        ];

        // Submitted the next morning, but started on the note's own day, so it is not a past-day note.
        string[] nextDay =
        [
            $"Sunday 27{Nb}September 2026",
            $"Written by Priya Nair · Submitted Mon 28{Nb}Sep, 8:05{Nb}am",
            "Goals",
            "No goals set",
            "Common items",
            "No common items set",
            "Guided notes",
            ZoeNarrative,
        ];
        string[] flaggedAndReviewed =
        [
            $"Monday 28{Nb}September 2026",
            $"Written by Priya Nair · Submitted Mon 28{Nb}Sep, 3:00{Nb}pm",
            "Flagged for manager: \"Mentioned pain in her left knee.\"",
            $"Reviewed by Jo Smith on Mon 28{Nb}Sep 2026, 6:00{Nb}pm",
            "Comment: Booked a physio visit.\nWill check on Monday.",
            "Goals",
            "No goals set",
            "Common items",
            "No common items set",
            "Guided notes",
            "Flagged and reviewed text.",
        ];

        // Three versions, whose picked groups differ: the current one shows Every note and both groups.
        string[] editedCurrent =
        [
            $"Tuesday 29{Nb}September 2026",
            $"Written by Priya Nair · Submitted Tue 29{Nb}Sep, 4:42{Nb}pm",
            $"Edited · 3 versions · last change by Priya Nair, Wed 30{Nb}Sep 2026, 8:30{Nb}am",
            "Goals",
            "No goals set",
            "Common items",
            "Every note",
            "☑ Medication prompted",
            "Community outing",
            "☐ Travelled by bus",
            "Personal care",
            "☐ Showered with prompting",
            "Guided notes",
            "Version three text.",
        ];
        string[] editedEarlier =
        [
            "Earlier versions",
            $"Version 2: saved by Priya Nair, Tue 29{Nb}Sep 2026, 6:00{Nb}pm",
            "Goals",
            "No goals set",
            "Common items",
            "Every note",
            "☐ Medication prompted",
            "Personal care",
            "☑ Showered with prompting",
            "Guided notes",
            "Version two text.",
            $"Version 1: submitted by Priya Nair, Tue 29{Nb}Sep 2026, 4:42{Nb}pm",
            "Goals",
            "☑ Catch the bus to the library",
            "Common items",
            "Every note",
            "☑ Medication prompted",
            "Community outing",
            "☑ Travelled by bus",
            "Guided notes",
            "Version one text.",
        ];

        // Version 1 was flagged and reviewed, and version 2 took the flag away: the current block says so, and the
        // review stays with the version it was of.
        string[] flagRemovedCurrent =
        [
            $"Wednesday 30{Nb}September 2026",
            $"Written by Priya Nair · Submitted Wed 30{Nb}Sep, 2:00{Nb}pm",
            $"Edited · 2 versions · last change by Priya Nair, Wed 30{Nb}Sep 2026, 5:00{Nb}pm",
            "Flagged for manager: \"Seemed tired all day.\" (flag removed in a later edit)",
            $"Reviewed by Jo Smith on Wed 30{Nb}Sep 2026, 4:00{Nb}pm",
            "Comment: Rest day arranged.",
            "Goals",
            "No goals set",
            "Common items",
            "No common items set",
            "Guided notes",
            "Rested version two text.",
        ];
        string[] flagRemovedEarlier =
        [
            "Earlier versions",
            $"Version 1: submitted by Priya Nair, Wed 30{Nb}Sep 2026, 2:00{Nb}pm",
            "Flagged for manager: \"Seemed tired all day.\"",
            $"Reviewed by Jo Smith on Wed 30{Nb}Sep 2026, 4:00{Nb}pm",
            "Comment: Rest day arranged.",
            "Goals",
            "No goals set",
            "Common items",
            "No common items set",
            "Guided notes",
            "Tired version one text.",
        ];

        // Started the next morning by the manager, so it is a past-day note.
        string[] pastDay =
        [
            $"Thursday 1{Nb}October 2026",
            $"Past-day note, written by Jo Smith (manager) on Fri 2{Nb}Oct 2026, 8:10{Nb}am · Submitted "
                + $"Fri 2{Nb}Oct, 8:20{Nb}am",
            "Goals",
            "No goals set",
            "Common items",
            "No common items set",
            "Guided notes",
            "Written the next morning by the manager.",
        ];

        // S3.9: written against the wrong participant, then corrected. Version 1 shows only with earlier versions.
        string[] wrongParticipantCurrent =
        [
            $"Friday 2{Nb}October 2026",
            $"Written by Priya Nair · Submitted Fri 2{Nb}Oct, 7:00{Nb}am",
            $"Edited · 2 versions · last change by Priya Nair, Fri 2{Nb}Oct 2026, 8:00{Nb}am",
            "Goals",
            "No goals set",
            "Common items",
            "No common items set",
            "Guided notes",
            InErrorNarrative,
        ];
        string[] wrongParticipantEarlier =
        [
            "Earlier versions",
            $"Version 1: submitted by Priya Nair, Fri 2{Nb}Oct 2026, 7:00{Nb}am",
            "Goals",
            "No goals set",
            "Common items",
            "No common items set",
            "Guided notes",
            WrongPersonNarrative,
        ];
        string[] withoutHistory =
        [
            .. block,
            "Earlier versions: not included",
            .. nextDay,
            .. flaggedAndReviewed,
            .. editedCurrent,
            .. flagRemovedCurrent,
            .. pastDay,
            .. wrongParticipantCurrent,
        ];
        string[] withHistory =
        [
            .. block,
            "Earlier versions: included",
            .. nextDay,
            .. flaggedAndReviewed,
            .. editedCurrent,
            .. editedEarlier,
            .. flagRemovedCurrent,
            .. flagRemovedEarlier,
            .. pastDay,
            .. wrongParticipantCurrent,
            .. wrongParticipantEarlier,
        ];

        var plainDocx = await client.GetByteArrayAsync(
            Address(zoe.Id, "docx", includeHistory: false), TestContext.Current.CancellationToken);
        var plainPdf = await client.GetByteArrayAsync(
            Address(zoe.Id, "pdf", includeHistory: false), TestContext.Current.CancellationToken);
        var historyDocx = await client.GetByteArrayAsync(
            Address(zoe.Id, "docx", includeHistory: true), TestContext.Current.CancellationToken);
        var historyPdf = await client.GetByteArrayAsync(
            Address(zoe.Id, "pdf", includeHistory: true), TestContext.Current.CancellationToken);

        Assert.Equal(withoutHistory, ReportText.WordParagraphs(plainDocx));
        Assert.Equal(Words(withoutHistory), Words(ReportText.PdfWords(plainPdf)));
        Assert.DoesNotContain(ReportText.WordParagraphs(plainDocx), text => text is "Earlier versions");
        Assert.DoesNotContain(
            ReportText.WordParagraphs(plainDocx), text => VersionHeading().IsMatch(text));
        Assert.DoesNotContain(WrongPersonNarrative, ReportText.WordParagraphs(plainDocx));
        Assert.DoesNotContain("Sam Taylor", string.Join(' ', ReportText.PdfWords(plainPdf)));
        Assert.Equal(withHistory, ReportText.WordParagraphs(historyDocx));
        Assert.Equal(Words(withHistory), Words(ReportText.PdfWords(historyPdf)));
        AssertHeadingStyles(historyDocx);
        AssertFurniture(title, historyDocx, historyPdf);
        AssertFurniture(title, plainDocx, plainPdf);
        AssertNoNameInTheProperties(historyDocx, historyPdf);
        AssertNoNameInTheProperties(plainDocx, plainPdf);
        AssertEveryLetterIsDejaVu(historyPdf);
    }

    // The words of an earlier version's heading, which a narrative that starts with Version does not have.
    [GeneratedRegex(@"^Version \d+: (saved|submitted) by ")]
    private static partial Regex VersionHeading();

    private static string Address(Guid participantId, string format, bool includeHistory) =>
        $"/api/reports/participants/{participantId}/export?{Query}&format={format}"
        + $"&includeHistory={(includeHistory ? "true" : "false")}";

    private static string Words(IEnumerable<string> lines) => ReportText.Normalised(string.Join(' ', lines));

    // The date headings are the first level, Earlier versions the second, as the current version's sections are, and
    // each version's heading the third: Word's own navigation shows the note, its sections and its versions.
    private static void AssertHeadingStyles(byte[] docx)
    {
        var paragraphs = ReportText.WordStyledParagraphs(docx);

        Assert.Equal(
            [
                $"Sunday 27{Nb}September 2026",
                $"Monday 28{Nb}September 2026",
                $"Tuesday 29{Nb}September 2026",
                $"Wednesday 30{Nb}September 2026",
                $"Thursday 1{Nb}October 2026",
                $"Friday 2{Nb}October 2026",
            ],
            paragraphs.Where(paragraph => paragraph.Style == "Heading1").Select(paragraph => paragraph.Text));
        Assert.Equal(
            3,
            paragraphs.Count(paragraph => paragraph is { Style: "Heading2", Text: "Earlier versions" }));
        Assert.DoesNotContain(
            paragraphs, paragraph => paragraph.Text == "Earlier versions" && paragraph.Style != "Heading2");
        var versions = paragraphs.Where(paragraph => VersionHeading().IsMatch(paragraph.Text)).ToList();
        Assert.Equal(4, versions.Count);
        Assert.All(versions, paragraph => Assert.Equal("Heading3", paragraph.Style));
        Assert.Equal("Title", paragraphs[0].Style);
        Assert.All(paragraphs.Skip(1).Take(6), paragraph => Assert.Equal("Subtitle", paragraph.Style));
    }

    // The title is the running header from page 2, and the footer names the exporter and the time. Neither names the
    // participant, in Word or in the PDF.
    private static void AssertFurniture(string title, byte[] docx, byte[] pdf)
    {
        var footer = "Confidential: health information. Copy exported from Grow2Notes by Jo Smith, "
            + $"Fri 2{Nb}Oct 2026, 9:14{Nb}am. The record in Grow2Notes is authoritative.";
        var word = ReportText.WordHeadersAndFooters(docx);
        Assert.Equal(title, word.DefaultHeader);
        Assert.Equal(footer + " Page {PAGE} of {NUMPAGES}", word.DefaultFooter);
        using var pdfDocument = PdfDocument.Open(pdf);
        var pages = ReportText.PdfHeadersAndFooters(pdf);
        Assert.Equal(ReportText.Normalised($"{footer} Page 1 of {pdfDocument.NumberOfPages}"), pages[0].Footer);
        Assert.All(pages.Skip(1), page => Assert.Equal(ReportText.Normalised(title), page.Header));
        foreach (var name in new[] { "Zoë", "Adams" })
        {
            Assert.DoesNotContain(name, string.Join(' ', word.DefaultHeader, word.DefaultFooter, word.FirstFooter));
            Assert.All(pages, page => Assert.DoesNotContain(name, page.Header + page.Footer));
        }
    }

    // M4: every letter of the PDF, the title block included, is drawn by DejaVu Sans.
    private static void AssertEveryLetterIsDejaVu(byte[] pdf)
    {
        using var document = PdfDocument.Open(pdf);

        var letters = document.GetPages().SelectMany(page => page.Letters).ToList();

        Assert.NotEmpty(letters);
        Assert.All(letters, letter =>
        {
            Assert.All(letter.Value.EnumerateRunes(), rune => Assert.True(DejaVuSans.Draws(rune)));
            Assert.Contains("DejaVu Sans", letter.FontName);
        });
    }

    // The live group and items differ in wording from the ones that the notes' versions hold, so a file that shows the
    // versions' wording proves that it reads the versions alone.
    private async Task<CommonItemIds> AddLiveCommonItemsAsync(SeededOrganisation organisation)
    {
        var organisationId = organisation.OrganisationId;
        var every = await factory.Services.AddItemsAsync(
            organisationId, organisation.EveryNoteGroupId, new TestCommonItem("Live every note item", 1));
        var groups = await factory.Services.AddGroupsAsync(
            organisationId, new TestCommonItemGroup("Live outing group", 1), new TestCommonItemGroup("Live care", 2));
        var outing = await factory.Services.AddItemsAsync(
            organisationId, groups[0], new TestCommonItem("Live outing item", 1));
        var care = await factory.Services.AddItemsAsync(
            organisationId, groups[1], new TestCommonItem("Live care item", 1));
        return new(organisation.EveryNoteGroupId, every[0], groups[0], outing[0], groups[1], care[0]);
    }

    // Sunday 27 September, started that morning and submitted at 8:05 am on the Monday.
    private async Task AddNextDayNoteAsync(SeededOrganisation organisation, Guid participantId, Guid priya) =>
        await factory.Services.AddSubmittedAsync(
            organisation,
            new(participantId, new DateOnly(2026, 9, 27), priya, Melbourne(9, 27, 9, 0), Melbourne(9, 28, 8, 5),
                ZoeNarrative));

    // Monday 28 September, flagged and reviewed by Jo, whose comment has two lines.
    private async Task AddFlaggedAndReviewedNoteAsync(
        SeededOrganisation organisation, Guid participantId, Guid priya, Guid jo)
    {
        var noteId = await factory.Services.AddSubmittedAsync(
            organisation,
            new(
                participantId,
                new DateOnly(2026, 9, 28),
                priya,
                Melbourne(9, 28, 9, 0),
                Melbourne(9, 28, 15, 0),
                "Flagged and reviewed text.",
                IsFlagged: true,
                FlagReason: "Mentioned pain in her left knee.",
                FlagStatus: FlagStatus.Reviewed));
        await factory.Services.AddReviewAsync(
            organisation, noteId, 1, jo, Melbourne(9, 28, 18, 0), "Booked a physio visit.\nWill check on Monday.");
    }

    // Tuesday 29 September: three versions, each with a different Guided notes text and different picked groups, and
    // goals in version 1 alone, as the fixture adds them.
    private async Task AddEditedNoteAsync(
        SeededOrganisation organisation, Guid participantId, Guid priya, CommonItemIds items)
    {
        var goals = await factory.Services.AddGoalsAsync(
            organisation, participantId, new TestGoal("Catch the bus to the library", 1));
        var noteId = await factory.Services.AddSubmittedAsync(
            organisation,
            new(
                participantId,
                new DateOnly(2026, 9, 29),
                priya,
                Melbourne(9, 29, 9, 0),
                Melbourne(9, 29, 16, 42),
                "Version one text.",
                Goals: [new(goals[0], "Catch the bus to the library", true)],
                Groups:
                [
                    EveryNote(items, true), Outing(items, picked: true, true), Care(items, picked: false, false),
                ]));
        await factory.Services.AddVersionAsync(
            organisation,
            noteId,
            2,
            Melbourne(9, 29, 18, 0),
            flagReason: null,
            flaggedVersionNumber: 1,
            [EveryNote(items, false), Outing(items, picked: false, false), Care(items, picked: true, true)],
            priya,
            "Version two text.");
        await factory.Services.AddVersionAsync(
            organisation,
            noteId,
            3,
            Melbourne(9, 30, 8, 30),
            flagReason: null,
            flaggedVersionNumber: 1,
            [EveryNote(items, true), Outing(items, picked: true, false), Care(items, picked: true, false)],
            priya,
            "Version three text.");
    }

    // Wednesday 30 September: version 1 flagged and reviewed, and version 2 not flagged.
    private async Task AddNoteWhoseFlagWasRemovedAsync(
        SeededOrganisation organisation, Guid participantId, Guid priya, Guid jo)
    {
        var noteId = await factory.Services.AddSubmittedAsync(
            organisation,
            new(
                participantId,
                new DateOnly(2026, 9, 30),
                priya,
                Melbourne(9, 30, 9, 0),
                Melbourne(9, 30, 14, 0),
                "Tired version one text.",
                IsFlagged: true,
                FlagReason: "Seemed tired all day.",
                FlagStatus: FlagStatus.Reviewed));
        await factory.Services.AddReviewAsync(
            organisation, noteId, 1, jo, Melbourne(9, 30, 16, 0), "Rest day arranged.");
        await factory.Services.AddVersionAsync(
            organisation,
            noteId,
            2,
            Melbourne(9, 30, 17, 0),
            flagReason: null,
            flaggedVersionNumber: 1,
            groups: null,
            priya,
            "Rested version two text.");
    }

    // Thursday 1 October, which the manager Jo started at 8:10 am on the 2nd and submitted at 8:20 am.
    private async Task AddPastDayNoteAsync(SeededOrganisation organisation, Guid participantId, Guid jo) =>
        await factory.Services.AddSubmittedAsync(
            organisation,
            new(participantId, new DateOnly(2026, 10, 1), jo, Melbourne(10, 2, 8, 10), Melbourne(10, 2, 8, 20),
                "Written the next morning by the manager."));

    // Friday 2 October: version 1 was written about another person, and version 2 says so.
    private async Task AddWrongParticipantNoteAsync(SeededOrganisation organisation, Guid participantId, Guid priya)
    {
        var noteId = await factory.Services.AddSubmittedAsync(
            organisation,
            new(
                participantId,
                new DateOnly(2026, 10, 2),
                priya,
                Melbourne(10, 2, 6, 30),
                Melbourne(10, 2, 7, 0),
                WrongPersonNarrative));
        await factory.Services.AddVersionAsync(
            organisation,
            noteId,
            2,
            Melbourne(10, 2, 8, 0),
            flagReason: null,
            flaggedVersionNumber: 1,
            groups: null,
            priya,
            InErrorNarrative);
    }

    // A draft and a discarded draft in the range, a submitted note before it, and another participant's note on the
    // day of the edited one.
    private async Task AddNotesThatMustNotAppearAsync(SeededOrganisation organisation, Guid participantId, Guid priya)
    {
        await factory.Services.AddDraftAsync(
            organisation,
            new(
                participantId,
                new DateOnly(2026, 9, 26),
                priya,
                Melbourne(9, 26, 9, 0),
                Narrative: "Draft text that must not appear",
                IsFlagged: true,
                FlagReason: "Draft flag that must not appear"));
        var discarded = await factory.Services.AddDraftAsync(
            organisation,
            new(
                participantId,
                new DateOnly(2026, 9, 25),
                priya,
                Melbourne(9, 25, 9, 0),
                Narrative: "Discarded text that must not appear"));
        await factory.Services.DiscardAsync(organisation, discarded, Melbourne(9, 25, 10, 0));
        await factory.Services.AddSubmittedAsync(
            organisation,
            new(participantId, new DateOnly(2026, 8, 31), priya, Melbourne(8, 31, 9, 0), Melbourne(8, 31, 15, 0),
                "Text from before the range that must not appear"));
        var mia = await factory.Services.AddParticipantAsync(organisation, givenName: "Mia", familyName: "Brown");
        await factory.Services.AddSubmittedAsync(
            organisation,
            new(mia.Id, new DateOnly(2026, 9, 29), priya, Melbourne(9, 29, 9, 0), Melbourne(9, 29, 15, 0),
                "Another participant's text that must not appear"));
    }

    // No participant or staff name, and no date of birth, is in a property of either file.
    private static void AssertNoNameInTheProperties(byte[] docx, byte[] pdf)
    {
        string[] words =
        [
            "Zoë", "Adams", "Priya", "Nair", "Smith", "Mia", "Brown", "Sam", "Taylor", "Jane", "Citizen",
        ];
        using var stream = new MemoryStream(docx);
        using var word = WordprocessingDocument.Open(stream, false);
        var core = word.PackageProperties;
        var wordProperties = string.Join(
            ' ',
            core.Title,
            core.Creator,
            core.LastModifiedBy,
            core.Subject,
            core.Keywords,
            core.Description,
            core.Category,
            word.ExtendedFilePropertiesPart!.Properties!.InnerText);
        using var pdfDocument = PdfDocument.Open(pdf);
        var information = pdfDocument.Information;
        var pdfProperties = string.Join(
            ' ',
            information.Title,
            information.Subject,
            information.Author,
            information.Keywords,
            information.Creator,
            information.Producer);
        foreach (var name in words)
        {
            Assert.DoesNotContain(name, wordProperties, StringComparison.Ordinal);
            Assert.DoesNotContain(name, pdfProperties, StringComparison.Ordinal);
        }
    }

    // A Melbourne local time in September or October 2026 before the daylight-saving change on 4 October, in UTC.
    private static DateTime Melbourne(int month, int day, int hour, int minute) =>
        new DateTime(2026, month, day, hour, minute, 0, DateTimeKind.Utc).AddHours(-10);

    // The version's Every note group, which is never picked, with its one item.
    private static TestVersionGroup EveryNote(CommonItemIds items, bool isTicked) =>
        new(items.EveryNoteGroup, "Every note", true, false, [new(items.Every, "Medication prompted", isTicked)]);

    private static TestVersionGroup Outing(CommonItemIds items, bool picked, bool isTicked) =>
        new(items.OutingGroup, "Community outing", false, picked, [new(items.Outing, "Travelled by bus", isTicked)]);

    private static TestVersionGroup Care(CommonItemIds items, bool picked, bool isTicked) =>
        new(items.CareGroup, "Personal care", false, picked, [new(items.Care, "Showered with prompting", isTicked)]);

    // The organisation's name is made up by the fixture, so the test reads it.
    private async Task<string> OrganisationNameAsync(SeededOrganisation organisation)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        return await db.Organisations
            .Where(row => row.Id == organisation.OrganisationId)
            .Select(row => row.Name)
            .SingleAsync(TestContext.Current.CancellationToken);
    }

    // The IDs of the live groups and items that the notes' versions point at.
    private sealed record CommonItemIds(
        Guid EveryNoteGroup, Guid Every, Guid OutingGroup, Guid Outing, Guid CareGroup, Guid Care);
}
