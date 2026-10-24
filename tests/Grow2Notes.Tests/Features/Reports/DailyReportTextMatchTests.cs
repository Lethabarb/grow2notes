using DocumentFormat.OpenXml.Packaging;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using UglyToad.PdfPig;

namespace Grow2Notes.Tests.Features.Reports;

/// <summary>
/// What the daily report's Word and PDF files say (S04.01.01 to S04.01.06, design.md §11.3 and §11.5), for the one
/// fixture day of F04.01 that the test adds to an organisation of its own (<see cref="TestOrganisations"/>): the Word
/// file's body paragraphs equal the expected list exactly, and the PDF's body words, white space aside, equal the same
/// list's. Equality proves the order, the archived participant, the current version alone, each marker, the common
/// items by group with the version's own wording, and the absence of drafts, pending edits, discarded drafts, other
/// days, participants with no note and counts. The PDF match is of word order only, as wrapping and spacing are the
/// renderer's. The footer and the file properties are checked on the same files.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class DailyReportTextMatchTests(Grow2NotesFactory factory) : IClassFixture<Grow2NotesFactory>
{
    // A no-break space, which the file puts between a day and its month and between a time and am or pm.
    private const string Nb = " ";

    private const string ZoeNarrative = "Zoë said “I’m ready” — then we left…\n\nShe paid at the café herself.";

    private const string EditedNarrative = "Jane made toast. Then a walk to the shops.";

    // 9:14 am on Friday 2 October 2026 in Melbourne, before the daylight-saving change on the 4th.
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 23, 14, 0, TimeSpan.Zero);

    private static readonly DateOnly NoteDate = new(2026, 10, 1);

    private static readonly DateTime StartedAtUtc = Melbourne(1, 10, 14);

    [Fact]
    public async Task The_files_say_the_days_submitted_notes_in_participant_order_and_nothing_else()
    {
        var organisation = await factory.Services.AddOrganisationAsync();
        var priya = await factory.Services.AddUserAsync(organisation, UserRole.Worker, "Priya Nair");
        var alex = await factory.Services.AddUserAsync(organisation, UserRole.Worker, "Alex Pham");
        var jo = await factory.Services.AddUserAsync(organisation, UserRole.Manager, "Jo Smith");
        var items = await AddLiveCommonItemsAsync(organisation);
        var zoe = await AddZoeAsync(organisation, priya, jo, items);
        await AddMiaAsync(organisation, priya, jo, items);
        await AddNoahAsync(organisation, alex);
        await AddAmelieAsync(organisation, alex);
        await AddLiamAsync(organisation, priya, items);
        await AddSamAsync(organisation, priya);
        await AddAvaAsync(organisation, jo, items);
        await AddNotesThatMustNotAppearAsync(organisation, priya, zoe);
        await using var app = factory.WithWebHostBuilder(builder => builder.UseClock(new FakeTimeProvider(Now)));
        using var client = app.CreateHttpsClient().SignInAs(jo);
        var footer = "Confidential: health information. Copy exported from Grow2Notes by Jo Smith, "
            + $"Fri 2{Nb}Oct 2026, 9:14{Nb}am. The record in Grow2Notes is authoritative.";
        string[] expected =
        [
            await OrganisationNameAsync(organisation),
            "Daily progress notes",
            $"Thursday 1{Nb}October 2026",
            $"As at Fri 2{Nb}Oct 2026, 9:14{Nb}am",
            "Exported by Jo Smith",

            // Every note and the two picked groups show; the unpicked group leaves no trace, and Jo's pending edit,
            // flagged, adds neither a flag line nor an Edited line.
            "Zoë Adams",
            $"Written by Priya Nair · Submitted Thu 1{Nb}Oct, 4:42{Nb}pm",
            "Goals",
            "☑ Catch the 903 bus to the library on her own",
            "☐ Make her own lunch",
            "Common items",
            "Every note",
            "☑ Medication prompted",
            "☐ Meal prepared",
            "Community outing",
            "☑ Travelled by bus or train",
            "☐ Paid for own purchases",
            "Personal care",
            "☑ Showered with prompting",
            "Guided notes",
            ZoeNarrative,

            // Version 2 is flagged again and reviewed again, so the reason is version 2's and the review the newest.
            "Mia Brown",
            $"Written by Priya Nair · Submitted Thu 1{Nb}Oct, 3:00{Nb}pm",
            $"Edited · 2 versions · last change by Priya Nair, Fri 2{Nb}Oct 2026, 8:30{Nb}am",
            "Flagged for manager: \"Knee still sore this morning.\"",
            $"Reviewed by Jo Smith on Fri 2{Nb}Oct 2026, 9:05{Nb}am",
            "Comment: Booked a physio visit.\nWill check on Monday.",
            "Goals",
            "No goals set",
            "Common items",
            "Every note",
            "☑ Medication prompted",
            "Guided notes",
            EditedNarrative,

            "Noah Clark",
            $"Written by Alex Pham · Submitted Thu 1{Nb}Oct, 2:00{Nb}pm",
            $"Edited · 2 versions · last change by Alex Pham, Thu 1{Nb}Oct 2026, 5:00{Nb}pm",
            "Flagged for manager: \"Seemed tired all day.\" (flag removed in a later edit)",
            "To review",
            "Goals",
            "No goals set",
            "Common items",
            "No common items set",
            "Guided notes",
            EditedNarrative,

            // The note's flag status is None, so there is no flag line, although version 1 is its flagged version.
            "Amélie Nguyen",
            $"Written by Alex Pham · Submitted Thu 1{Nb}Oct, 3:30{Nb}pm",
            $"Edited · 3 versions · last change by Alex Pham, Fri 2{Nb}Oct 2026, 8:45{Nb}am",
            "Goals",
            "No goals set",
            "Common items",
            "No common items set",
            "Guided notes",
            EditedNarrative,

            // Submitted the next morning, but started on the note's own day, so it is not a past-day note.
            "Liam Nguyen",
            $"Written by Priya Nair · Submitted Fri 2{Nb}Oct, 8:05{Nb}am",
            "Goals",
            "☑ Walk to the shops",
            "Common items",
            "Every note",
            "☐ Medication prompted",
            "☑ Meal prepared",
            "Guided notes",
            "Liam walked to the shops.\nHe bought bread.",

            "Sam Taylor",
            $"Written by Priya Nair · Submitted Thu 1{Nb}Oct, 1:20{Nb}pm",
            "Flagged for manager: \"Mentioned pain in his left knee after the walk.\"",
            "To review",
            "Goals",
            "No goals set",
            "Common items",
            "No common items set",
            "Guided notes",
            "Sam walked slowly.",

            // Started the next morning by the manager, so it is a past-day note. Its Every note group has no item, so
            // it is dropped, and the picked group shows.
            "Ava Wilson",
            $"Past-day note, written by Jo Smith (manager) on Fri 2{Nb}Oct 2026, 8:10{Nb}am · Submitted "
                + $"Fri 2{Nb}Oct, 8:20{Nb}am",
            "Goals",
            "No goals set",
            "Common items",
            "Personal care",
            "☐ Showered with prompting",
            "Guided notes",
            "Ava's note was written the next morning.",
        ];

        var docx = await client.GetByteArrayAsync(
            $"/api/reports/daily/{NoteDate:yyyy-MM-dd}/export?format=docx", TestContext.Current.CancellationToken);
        var pdf = await client.GetByteArrayAsync(
            $"/api/reports/daily/{NoteDate:yyyy-MM-dd}/export?format=pdf", TestContext.Current.CancellationToken);

        Assert.Equal(expected, ReportText.WordParagraphs(docx));
        Assert.Equal(
            ReportText.Normalised(string.Join(' ', expected)),
            ReportText.Normalised(string.Join(' ', ReportText.PdfWords(pdf))));
        Assert.Equal(footer + " Page {PAGE} of {NUMPAGES}", ReportText.WordHeadersAndFooters(docx).DefaultFooter);
        using (var pdfDocument = PdfDocument.Open(pdf))
        {
            var pages = ReportText.PdfHeadersAndFooters(pdf);
            Assert.Equal(ReportText.Normalised($"{footer} Page 1 of {pdfDocument.NumberOfPages}"), pages[0].Footer);
        }

        AssertNoNameInTheProperties(docx, pdf);
    }

    // The live groups and items differ in wording from the ones that the notes' versions hold, and one live group is
    // archived, so a file that shows the versions' wording and groups proves that it reads the versions alone.
    private async Task<CommonItemIds> AddLiveCommonItemsAsync(SeededOrganisation organisation)
    {
        var organisationId = organisation.OrganisationId;
        var every = await factory.Services.AddItemsAsync(
            organisationId,
            organisation.EveryNoteGroupId,
            new TestCommonItem("Live every note item one", 1),
            new TestCommonItem("Live every note item two", 2));
        var groups = await factory.Services.AddGroupsAsync(
            organisationId,
            new TestCommonItemGroup("Live outing group", 1),
            new TestCommonItemGroup("Live personal care group", 2, ArchivedAtUtc: Melbourne(1, 9, 0)),
            new TestCommonItemGroup("Live unpicked group", 3));
        var outing = await factory.Services.AddItemsAsync(
            organisationId,
            groups[0],
            new TestCommonItem("Live outing item one", 1),
            new TestCommonItem("Live outing item two", 2));
        var care = await factory.Services.AddItemsAsync(
            organisationId, groups[1], new TestCommonItem("Live personal care item", 1));
        var unpicked = await factory.Services.AddItemsAsync(
            organisationId, groups[2], new TestCommonItem("Live unpicked item", 1));
        return new(
            organisation.EveryNoteGroupId, groups[0], groups[1], groups[2], every, outing[0], outing[1], care[0],
            unpicked[0]);
    }

    // Zoë Adams, active, with two goals, one ticked, a narrative of curly quotes and a blank line, and Every note and
    // two picked groups, submitted at 4:42 pm in Melbourne by Priya. A fourth group is not picked. Jo has started an
    // edit, flagged, which must leave no trace.
    private async Task<Guid> AddZoeAsync(SeededOrganisation organisation, Guid priya, Guid jo, CommonItemIds items)
    {
        var zoe = await factory.Services.AddParticipantAsync(organisation, givenName: "Zoë", familyName: "Adams");
        var goals = await factory.Services.AddGoalsAsync(
            organisation,
            zoe.Id,
            new TestGoal("Catch the 903 bus to the library on her own", 1),
            new TestGoal("Make her own lunch", 2));
        var noteId = await factory.Services.AddSubmittedAsync(
            organisation,
            new(
                zoe.Id,
                NoteDate,
                priya,
                StartedAtUtc,
                Melbourne(1, 16, 42),
                ZoeNarrative,
                Goals:
                [
                    new(goals[0], "Catch the 903 bus to the library on her own", true),
                    new(goals[1], "Make her own lunch", false),
                ],
                Groups:
                [
                    EveryNote(items, ("Medication prompted", true), ("Meal prepared", false)),
                    new(
                        items.OutingGroup,
                        "Community outing",
                        false,
                        true,
                        [
                            new(items.Outing1, "Travelled by bus or train", true),
                            new(items.Outing2, "Paid for own purchases", false),
                        ]),
                    new(
                        items.CareGroup,
                        "Personal care",
                        false,
                        true,
                        [new(items.Care, "Showered with prompting", true)]),
                    new(
                        items.UnpickedGroup,
                        "Unpicked group that must not appear",
                        false,
                        false,
                        [new(items.Unpicked, "Unpicked item that must not appear", false)]),
                ]));
        await factory.Services.AddPendingEditAsync(
            organisation, noteId, jo, Melbourne(2, 9, 0), flagReason: "Pending flag that must not appear");
        return zoe.Id;
    }

    // Mia Brown, whose version 1 was flagged and reviewed on Thursday; Priya edited it on Friday morning and flagged it
    // again, and Jo reviewed it again with a comment of two lines.
    private async Task AddMiaAsync(SeededOrganisation organisation, Guid priya, Guid jo, CommonItemIds items)
    {
        var mia = await factory.Services.AddParticipantAsync(organisation, givenName: "Mia", familyName: "Brown");
        var noteId = await factory.Services.AddSubmittedAsync(
            organisation,
            new(
                mia.Id,
                NoteDate,
                priya,
                StartedAtUtc,
                Melbourne(1, 15, 0),
                "Version 1 text that must not appear",
                IsFlagged: true,
                FlagReason: "Version 1 reason that must not appear",
                FlagStatus: FlagStatus.Reviewed));
        await factory.Services.AddReviewAsync(
            organisation, noteId, 1, jo, Melbourne(1, 18, 0), "Review comment that must not appear");
        await factory.Services.AddVersionAsync(
            organisation,
            noteId,
            2,
            Melbourne(2, 8, 30),
            "Knee still sore this morning.",
            flaggedVersionNumber: 2,
            [EveryNote(items, ("Medication prompted", true))],
            priya);
        await factory.Services.AddReviewAsync(
            organisation, noteId, 2, jo, Melbourne(2, 9, 5), "Booked a physio visit.\nWill check on Monday.");
    }

    // Noah Clark, flagged in version 1, which is still to review, and not flagged in version 2.
    private async Task AddNoahAsync(SeededOrganisation organisation, Guid alex)
    {
        var noah = await factory.Services.AddParticipantAsync(organisation, givenName: "Noah", familyName: "Clark");
        var noteId = await factory.Services.AddSubmittedAsync(
            organisation,
            new(
                noah.Id,
                NoteDate,
                alex,
                StartedAtUtc,
                Melbourne(1, 14, 0),
                IsFlagged: true,
                FlagReason: "Seemed tired all day."));
        await factory.Services.AddVersionAsync(
            organisation,
            noteId,
            2,
            Melbourne(1, 17, 0),
            flagReason: null,
            flaggedVersionNumber: 1,
            groups: null,
            alex);
    }

    // Amélie Nguyen, archived, whose version 1 says what must not appear and whose version 3, the current one, has no
    // goals, so the file shows the current version alone. Her flagged version number is 1, as the fixture leaves it,
    // but her flag status is None, so the file has no flag line.
    private async Task AddAmelieAsync(SeededOrganisation organisation, Guid alex)
    {
        var amelie = await factory.Services.AddParticipantAsync(
            organisation, archived: true, givenName: "Amélie", familyName: "Nguyen");
        var goals = await factory.Services.AddGoalsAsync(
            organisation, amelie.Id, new TestGoal("Version 1 goal that must not appear", 1));
        var noteId = await factory.Services.AddSubmittedAsync(
            organisation,
            new(
                amelie.Id,
                NoteDate,
                alex,
                StartedAtUtc,
                Melbourne(1, 15, 30),
                "Version 1 text that must not appear",
                Goals: [new(goals[0], "Version 1 goal that must not appear", true)]));
        await factory.Services.AddVersion2Async(
            organisation, noteId, Melbourne(1, 18, 0), flagReason: null, flaggedVersionNumber: 1);
        await factory.Services.AddVersionAsync(
            organisation,
            noteId,
            3,
            Melbourne(2, 8, 45),
            flagReason: null,
            flaggedVersionNumber: 1,
            groups: null,
            alex);
    }

    // Liam Nguyen, started on the note's day and submitted at 8:05 am the next morning in Melbourne, so his line carries
    // the day and is not a past-day line. His version holds Every note and two groups that are not picked.
    private async Task AddLiamAsync(SeededOrganisation organisation, Guid priya, CommonItemIds items)
    {
        var liam = await factory.Services.AddParticipantAsync(organisation, givenName: "Liam", familyName: "Nguyen");
        var goals = await factory.Services.AddGoalsAsync(organisation, liam.Id, new TestGoal("Walk to the shops", 1));
        await factory.Services.AddSubmittedAsync(
            organisation,
            new(
                liam.Id,
                NoteDate,
                priya,
                StartedAtUtc,
                Melbourne(2, 8, 5),
                "Liam walked to the shops.\nHe bought bread.",
                Goals: [new(goals[0], "Walk to the shops", true)],
                Groups:
                [
                    EveryNote(items, ("Medication prompted", false), ("Meal prepared", true)),
                    new(
                        items.OutingGroup,
                        "Community outing",
                        false,
                        false,
                        [new(items.Outing1, "Travelled by bus or train", false)]),
                    new(
                        items.CareGroup,
                        "Personal care",
                        false,
                        false,
                        [new(items.Care, "Showered with prompting", false)]),
                ]));
    }

    // Sam Taylor, whose only version is flagged and not yet reviewed, with no groups.
    private async Task AddSamAsync(SeededOrganisation organisation, Guid priya)
    {
        var sam = await factory.Services.AddParticipantAsync(organisation, givenName: "Sam", familyName: "Taylor");
        await factory.Services.AddSubmittedAsync(
            organisation,
            new(
                sam.Id,
                NoteDate,
                priya,
                StartedAtUtc,
                Melbourne(1, 13, 20),
                "Sam walked slowly.",
                IsFlagged: true,
                FlagReason: "Mentioned pain in his left knee after the walk."));
    }

    // Ava Wilson, whose note for 1 October the manager Jo started at 8:10 am on the 2nd and submitted at 8:20 am: a
    // past-day note. Her Every note group has no item, and her picked Personal care group has one, unticked.
    private async Task AddAvaAsync(SeededOrganisation organisation, Guid jo, CommonItemIds items)
    {
        var ava = await factory.Services.AddParticipantAsync(organisation, givenName: "Ava", familyName: "Wilson");
        await factory.Services.AddSubmittedAsync(
            organisation,
            new(
                ava.Id,
                NoteDate,
                jo,
                Melbourne(2, 8, 10),
                Melbourne(2, 8, 20),
                "Ava's note was written the next morning.",
                Groups:
                [
                    new(items.EveryNoteGroup, "Every note", true, false, []),
                    new(
                        items.CareGroup,
                        "Personal care",
                        false,
                        true,
                        [new(items.Care, "Showered with prompting", false)]),
                ]));
    }

    // A draft with a flag, a discarded draft and a note of another day, each of a participant of its own or, for the
    // other day, Zoë's, whose participant ID is given. The organisation's own Jane Citizen has no note.
    private async Task AddNotesThatMustNotAppearAsync(SeededOrganisation organisation, Guid priya, Guid zoe)
    {
        var dora = await factory.Services.AddParticipantAsync(
            organisation, givenName: "Dora", familyName: "Draftley");
        await factory.Services.AddDraftAsync(
            organisation,
            new(
                dora.Id,
                NoteDate,
                priya,
                StartedAtUtc,
                Narrative: "Draft text that must not appear",
                IsFlagged: true,
                FlagReason: "Draft flag that must not appear"));
        var dan = await factory.Services.AddParticipantAsync(
            organisation, givenName: "Dan", familyName: "Discardson");
        var discarded = await factory.Services.AddDraftAsync(
            organisation,
            new(dan.Id, NoteDate, priya, StartedAtUtc, Narrative: "Discarded text that must not appear"));
        await factory.Services.DiscardAsync(organisation, discarded, StartedAtUtc.AddHours(1));

        await factory.Services.AddSubmittedAsync(
            organisation,
            new(
                zoe,
                new DateOnly(2026, 9, 30),
                priya,
                new DateTime(2026, 9, 30, 0, 14, 0, DateTimeKind.Utc),
                new DateTime(2026, 9, 30, 6, 42, 0, DateTimeKind.Utc),
                "Notes from 30 September that must not appear"));
    }

    // No participant or staff name is in a property of either file; the footer is the one place the file names a
    // person, and that is the exporter.
    private static void AssertNoNameInTheProperties(byte[] docx, byte[] pdf)
    {
        string[] names =
        [
            "Zoë", "Adams", "Mia", "Brown", "Noah", "Clark", "Amélie", "Nguyen", "Liam", "Sam", "Taylor", "Ava",
            "Wilson", "Dora", "Dan", "Jane", "Citizen", "Priya", "Nair", "Alex", "Pham", "Jo", "Smith",
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
        foreach (var name in names)
        {
            Assert.DoesNotContain(name, wordProperties, StringComparison.Ordinal);
            Assert.DoesNotContain(name, pdfProperties, StringComparison.Ordinal);
        }
    }

    // A Melbourne local time on the given day of October 2026, in UTC, before the daylight-saving change on the 4th.
    private static DateTime Melbourne(int day, int hour, int minute) =>
        new DateTime(2026, 10, day, hour, minute, 0, DateTimeKind.Utc).AddHours(-10);

    // The version's Every note group, which is never picked, with items of the given wording and ticks.
    private static TestVersionGroup EveryNote(CommonItemIds items, params (string Text, bool IsTicked)[] ticks) =>
        new(
            items.EveryNoteGroup,
            "Every note",
            true,
            false,
            [.. ticks.Select((tick, i) => new TestVersionItem(items.Every[i], tick.Text, tick.IsTicked))]);

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
        Guid EveryNoteGroup,
        Guid OutingGroup,
        Guid CareGroup,
        Guid UnpickedGroup,
        IReadOnlyList<Guid> Every,
        Guid Outing1,
        Guid Outing2,
        Guid Care,
        Guid Unpicked);
}
