using System.Diagnostics;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Grow2Notes.Tests.Features.Reports;

/// <summary>
/// How long a manager waits for the daily report (S04.01.06, design.md §7.1): a made-up day (A35) of 20 notes, each
/// with three goals, Every note and two of three groups picked, and about 2,000 characters of guided notes, which is
/// well under <c>Limits.GuidedNotes</c>; with every 4th note flagged, half of those reviewed, and every 5th note
/// edited. Each format is downloaded once, Word first, through the real endpoint on a host that is already running.
/// The first call of each is the cold path: compiling the queries, loading the font and the libraries all count.
/// The ceiling is 10 seconds against a time that is expected to be well under a second, a margin that shared build
/// machines, which run other test collections at the same time, can be trusted to keep. Both timings go to the test
/// output so that they can be quoted in the task's notes.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class DailyReportTimingTests(Grow2NotesFactory factory) : IClassFixture<Grow2NotesFactory>
{
    private const int NoteCount = 20;

    private const string Sentence = "Jane had a quiet morning and ate her breakfast without help. ";

    private static readonly TimeSpan Ceiling = TimeSpan.FromSeconds(10);

    // 9:14 am on Friday 2 October 2026 in Melbourne, before the daylight-saving change on the 4th.
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 23, 14, 0, TimeSpan.Zero);

    private static readonly DateOnly NoteDate = new(2026, 10, 1);

    // About 2,000 characters.
    private static readonly string Narrative = string.Concat(Enumerable.Repeat(Sentence, 40))[..2_000];

    [Fact]
    public async Task A_day_of_20_notes_downloads_in_under_10_seconds_in_each_format()
    {
        var organisation = await factory.Services.AddOrganisationAsync();
        var jo = await factory.Services.AddUserAsync(organisation, UserRole.Manager, "Jo Smith");
        var groups = await AddCommonItemsAsync(organisation);
        for (var number = 1; number <= NoteCount; number++)
        {
            await AddNoteAsync(organisation, jo, groups, number);
        }

        await using var app = factory.WithWebHostBuilder(builder => builder.UseClock(new FakeTimeProvider(Now)));

        // The client starts the host, so that the time of the first download is the report's alone.
        using var client = app.CreateHttpsClient().SignInAs(jo);

        var docx = await TimeAsync(client, "docx");
        var pdf = await TimeAsync(client, "pdf");

        TestContext.Current.TestOutputHelper?.WriteLine(
            $"Report of {NoteCount} notes: docx {docx.TotalMilliseconds:N0} ms, pdf {pdf.TotalMilliseconds:N0} ms.");
        Assert.True(docx < Ceiling, $"The Word file took {docx.TotalSeconds:N1} s.");
        Assert.True(pdf < Ceiling, $"The PDF took {pdf.TotalSeconds:N1} s.");
    }

    private static async Task<TimeSpan> TimeAsync(HttpClient client, string format)
    {
        var stopwatch = Stopwatch.StartNew();
        var bytes = await client.GetByteArrayAsync(
            $"/api/reports/daily/{NoteDate:yyyy-MM-dd}/export?format={format}", TestContext.Current.CancellationToken);
        stopwatch.Stop();

        Assert.NotEmpty(bytes);
        return stopwatch.Elapsed;
    }

    // The organisation's Every note group gets two items, and two more groups get two each, a third group none that
    // a note picks, so that every note holds the same shape.
    private async Task<IReadOnlyList<TestVersionGroup>> AddCommonItemsAsync(SeededOrganisation organisation)
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
        return
        [
            Group(organisation.EveryNoteGroupId, "Every note", true, false, every),
            Group(groupIds[0], "Community outing", false, true, outing),
            Group(groupIds[1], "Personal care", false, true, care),
            Group(groupIds[2], "Household tasks", false, false, household),
        ];
    }

    // One participant with three goals and a note submitted by the worker. Every 4th note is flagged, and every 2nd
    // of those is reviewed by Jo; every 5th note is edited by the worker, a flagged one flagged again.
    private async Task AddNoteAsync(
        SeededOrganisation organisation, Guid jo, IReadOnlyList<TestVersionGroup> groups, int number)
    {
        var participant = await factory.Services.AddParticipantAsync(
            organisation, givenName: $"Participant{number}", familyName: "Citizen");
        var goals = await factory.Services.AddGoalsAsync(
            organisation,
            participant.Id,
            new TestGoal("Catch the bus to the library on her own", 1),
            new TestGoal("Make her own lunch", 2),
            new TestGoal("Call a friend each week", 3));
        var flagged = number % 4 == 0;
        var reviewed = flagged && number % 8 == 0;
        var edited = number % 5 == 0;
        var flagReason = $"Mentioned pain in her left knee after the walk, note {number}.";
        var noteId = await factory.Services.AddSubmittedAsync(
            organisation,
            new(
                participant.Id,
                NoteDate,
                organisation.WorkerId,
                Melbourne(1, 9, number),
                Melbourne(1, 15, number),
                Narrative,
                flagged,
                flagged ? flagReason : null,
                reviewed ? FlagStatus.Reviewed : null,
                [.. goals.Select((goal, i) => new TestVersionGoal(goal, $"Goal {i + 1} of {number}", i == 0))],
                groups));
        if (reviewed)
        {
            await factory.Services.AddReviewAsync(
                organisation, noteId, 1, jo, Melbourne(1, 18, number), "Booked a physio visit.");
        }

        if (edited)
        {
            await factory.Services.AddVersionAsync(
                organisation,
                noteId,
                2,
                Melbourne(2, 8, number),
                flagged ? flagReason : null,
                flagged ? 2 : 1,
                groups,
                organisation.WorkerId);
        }
    }

    private static TestVersionGroup Group(
        Guid groupId, string name, bool isEveryNote, bool isPicked, IReadOnlyList<Guid> itemIds) =>
        new(
            groupId,
            name,
            isEveryNote,
            isPicked,
            [.. itemIds.Select((itemId, i) => new TestVersionItem(itemId, $"{name} item {i + 1}", i == 0))]);

    // A Melbourne local time on the given day of October 2026, in UTC, before the daylight-saving change on the 4th.
    private static DateTime Melbourne(int day, int hour, int minute) =>
        new DateTime(2026, 10, day, hour, minute, 0, DateTimeKind.Utc).AddHours(-10);
}
