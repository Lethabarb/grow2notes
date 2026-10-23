using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Grow2Notes.Tests.Features.Reports;

/// <summary>
/// What the daily report's Word and PDF files say (S04.01.01, S04.01.02, design.md §11.3), for a day that the test adds
/// to an organisation of its own (<see cref="TestOrganisations"/>): the Word file's paragraphs equal the expected list
/// exactly, and the PDF's words, white space aside, equal the same list's. Equality proves the order, the archived
/// participant, the current version alone, and the absence of drafts, discarded drafts, other days, participants with
/// no note and counts. The PDF match is of word order only, as wrapping and spacing are the renderer's.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class DailyReportTextMatchTests(Grow2NotesFactory factory) : IClassFixture<Grow2NotesFactory>
{
    // A no-break space, which the file puts between a day and its month and between a time and am or pm.
    private const string Nb = " ";

    private const string ZoeNarrative = "Zoë said “I’m ready” — then we left…\n\nShe paid at the café herself.";

    // 9:14 am on Friday 2 October 2026 in Melbourne, before the daylight-saving change on the 4th.
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 23, 14, 0, TimeSpan.Zero);

    private static readonly DateOnly NoteDate = new(2026, 10, 1);

    private static readonly DateTime StartedAtUtc = new(2026, 10, 1, 0, 14, 0, DateTimeKind.Utc);

    [Fact]
    public async Task The_files_say_the_days_submitted_notes_in_participant_order_and_nothing_else()
    {
        var organisation = await factory.Services.AddOrganisationAsync();
        var priya = await factory.Services.AddUserAsync(organisation, UserRole.Worker, "Priya Nair");
        var alex = await factory.Services.AddUserAsync(organisation, UserRole.Worker, "Alex Pham");
        var jo = await factory.Services.AddUserAsync(organisation, UserRole.Manager, "Jo Smith");
        var zoe = await AddZoeAsync(organisation, priya);
        await AddAmelieAsync(organisation, alex);
        await AddLiamAsync(organisation, priya);
        await AddNotesThatMustNotAppearAsync(organisation, priya, zoe);
        await using var app = factory.WithWebHostBuilder(builder => builder.UseClock(new FakeTimeProvider(Now)));
        using var client = app.CreateHttpsClient().SignInAs(jo);
        string[] expected =
        [
            await OrganisationNameAsync(organisation),
            "Daily progress notes",
            $"Thursday 1{Nb}October 2026",
            $"As at Fri 2{Nb}Oct 2026, 9:14{Nb}am",
            "Exported by Jo Smith",
            "Zoë Adams",
            $"Written by Priya Nair · Submitted Thu 1{Nb}Oct, 4:42{Nb}pm",
            "Goals",
            "☑ Catch the 903 bus to the library on her own",
            "☐ Make her own lunch",
            "Guided notes",
            ZoeNarrative,
            "Amélie Nguyen",
            $"Written by Alex Pham · Submitted Thu 1{Nb}Oct, 3:30{Nb}pm",
            "Goals",
            "No goals set",
            "Guided notes",
            "Jane made toast. Then a walk to the shops.",
            "Liam Nguyen",
            $"Written by Priya Nair · Submitted Fri 2{Nb}Oct, 8:05{Nb}am",
            "Goals",
            "☑ Walk to the shops",
            "Guided notes",
            "Liam walked to the shops.\nHe bought bread.",
        ];

        var docx = await client.GetByteArrayAsync(
            $"/api/reports/daily/{NoteDate:yyyy-MM-dd}/export?format=docx", TestContext.Current.CancellationToken);
        var pdf = await client.GetByteArrayAsync(
            $"/api/reports/daily/{NoteDate:yyyy-MM-dd}/export?format=pdf", TestContext.Current.CancellationToken);

        Assert.Equal(expected, ReportText.WordParagraphs(docx));
        Assert.Equal(
            ReportText.Normalised(string.Join(' ', expected)),
            ReportText.Normalised(string.Join(' ', ReportText.PdfWords(pdf))));
    }

    // Zoë Adams, active, with two goals, one ticked, and a narrative of curly quotes and a blank line, submitted at
    // 4:42 pm in Melbourne by Priya.
    private async Task<Guid> AddZoeAsync(SeededOrganisation organisation, Guid priya)
    {
        var zoe = await factory.Services.AddParticipantAsync(organisation, givenName: "Zoë", familyName: "Adams");
        var goals = await factory.Services.AddGoalsAsync(
            organisation,
            zoe.Id,
            new TestGoal("Catch the 903 bus to the library on her own", 1),
            new TestGoal("Make her own lunch", 2));
        await factory.Services.AddSubmittedAsync(
            organisation,
            new(
                zoe.Id,
                NoteDate,
                priya,
                StartedAtUtc,
                new DateTime(2026, 10, 1, 6, 42, 0, DateTimeKind.Utc),
                ZoeNarrative,
                Goals:
                [
                    new(goals[0], "Catch the 903 bus to the library on her own", true),
                    new(goals[1], "Make her own lunch", false),
                ]));
        return zoe.Id;
    }

    // Amélie Nguyen, archived, whose version 1 says what must not appear and whose version 2, the current one, has no
    // goals, so the file shows the current version alone.
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
                new DateTime(2026, 10, 1, 5, 30, 0, DateTimeKind.Utc),
                "Version 1 text that must not appear",
                Goals: [new(goals[0], "Version 1 goal that must not appear", true)]));
        await factory.Services.AddVersion2Async(
            organisation,
            noteId,
            new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc),
            flagReason: null,
            flaggedVersionNumber: 1);
    }

    // Liam Nguyen, submitted at 8:05 am the next morning in Melbourne, so his line carries the day.
    private async Task AddLiamAsync(SeededOrganisation organisation, Guid priya)
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
                new DateTime(2026, 10, 1, 22, 5, 0, DateTimeKind.Utc),
                "Liam walked to the shops.\nHe bought bread.",
                Goals: [new(goals[0], "Walk to the shops", true)]));
    }

    // A draft, a discarded draft and a note of another day, each of a participant of its own or, for the other day,
    // Zoë's, whose participant ID is given. The organisation's own Jane Citizen has no note.
    private async Task AddNotesThatMustNotAppearAsync(SeededOrganisation organisation, Guid priya, Guid zoe)
    {
        var dora = await factory.Services.AddParticipantAsync(
            organisation, givenName: "Dora", familyName: "Draftley");
        await factory.Services.AddDraftAsync(
            organisation, new(dora.Id, NoteDate, priya, StartedAtUtc, Narrative: "Draft text that must not appear"));
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
}
