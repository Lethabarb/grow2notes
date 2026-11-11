using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Features.Notes;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Grow2Notes.Tests.Features.Reports;

/// <summary>
/// From a note to the report (T23, D104): notes are made the way the screen makes them, with <c>GET</c> and
/// <c>PUT {base}/draft</c> and <c>POST {base}/versions</c>, and the day's Word and PDF files must say what was saved,
/// word for word, in the Word template's order. One note ticks and picks, with a detail on one item, an attendance
/// tick and a two-paragraph Medication, and has a narrative of several paragraphs with curly quotes and apostrophes;
/// one picks nothing and ticks nothing; one picks both groups and ticks only some. The groups that a note did not pick
/// are left out of the report, as in the Word documents (D106).
/// </summary>
[Collection<SqlServerCollection>]
public sealed class NoteToReportTests(Grow2NotesFactory factory) : IClassFixture<Grow2NotesFactory>
{
    private const string Nb = " ";

    private const string OtherEveryNote = "Other (staff to complete if different from above)";

    private const string Narrative =
        "Ann said “I’m ready” — then we left.\n\nIt wasn’t easy; she’s determined.\n\nShe thanked Alex’s “team”.";

    private const string Medication = "Took one tablet at 9 am.\n\nAnd “another” at 3 pm, as Ann’s plan says.";

    // 9:14 am on Friday 2 October 2026 in Melbourne, before the daylight-saving change on the 4th.
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 23, 14, 0, TimeSpan.Zero);

    private static readonly DateOnly NoteDate = new(2026, 10, 2);

    [Fact]
    public async Task The_files_say_what_the_notes_saved_with_only_the_picked_groups()
    {
        var organisation = await factory.Services.AddOrganisationAsync();
        var priya = await factory.Services.AddUserAsync(organisation, UserRole.Worker, "Priya Nair");
        var alex = await factory.Services.AddUserAsync(organisation, UserRole.Worker, "Alex Pham");
        var every = await factory.Services.AddItemsAsync(
            organisation.OrganisationId,
            organisation.EveryNoteGroupId,
            new TestCommonItem("Medication prompted", 0),
            new TestCommonItem(OtherEveryNote, 1, NeedsDetail: true));
        var groups = await factory.Services.AddGroupsAsync(
            organisation.OrganisationId,
            new TestCommonItemGroup("Community outing", 1),
            new TestCommonItemGroup("Personal care", 2));
        var outing = await factory.Services.AddItemsAsync(
            organisation.OrganisationId,
            groups[0],
            new TestCommonItem("Travelled by bus", 0),
            new TestCommonItem("Paid for own purchases", 1),
            new TestCommonItem("Other (outing)", 2, NeedsDetail: true));
        var care = await factory.Services.AddItemsAsync(
            organisation.OrganisationId, groups[1], new TestCommonItem("Showered with prompting", 0));
        var ann = await factory.Services.AddParticipantAsync(
            organisation, givenName: "Ann", familyName: "Alpha", hasMedicationLine: true);
        var annGoals = await factory.Services.AddGoalsAsync(
            organisation,
            ann.Id,
            new TestGoal("Make her own lunch", 0),
            new TestGoal("Plan a holiday", 1, IsMediumOrLongTerm: true));
        var bob = await factory.Services.AddParticipantAsync(organisation, givenName: "Bob", familyName: "Beta");
        var cy = await factory.Services.AddParticipantAsync(
            organisation, givenName: "Cy", familyName: "Gamma", hasMedicationLine: true);
        var cyGoals = await factory.Services.AddGoalsAsync(
            organisation, cy.Id, new TestGoal("Live alone", 0, IsMediumOrLongTerm: true));
        await using var app = factory.WithWebHostBuilder(builder => builder.UseClock(new FakeTimeProvider(Now)));
        using var priyaClient = app.CreateHttpsClient().SignInAs(priya);
        using var alexClient = app.CreateHttpsClient().SignInAs(alex);
        using var manager = app.CreateHttpsClient().SignInAs(organisation.ManagerId);

        // Different picks: Ann picks the outing group alone, with a detail on two items; Bob picks nothing; Cy both.
        await SubmitAsync(priyaClient, ann.Id, body =>
        {
            body["isLateNotice"] = true;
            body["medication"] = Medication;
            body["narrative"] = Narrative;
            body["tickedGoalIds"] = Ids(annGoals[0]);
            body["pickedGroupIds"] = Ids(groups[0]);
            body["tickedCommonItemIds"] = Ids(every[0], every[1], outing[0], outing[2]);
            body["commonItemDetails"] = Details((every[1], "Used a ‘taxi’"), (outing[2], "Tram “86”"));
        });
        await SubmitAsync(alexClient, bob.Id, body => body["narrative"] = "Bob's day was quiet.");
        await SubmitAsync(priyaClient, cy.Id, body =>
        {
            body["isNotIn"] = true;
            body["isSick"] = true;
            body["narrative"] = "Cy was away sick.";
            body["tickedGoalIds"] = Ids(cyGoals[0]);
            body["pickedGroupIds"] = Ids(groups[0], groups[1]);
            body["tickedCommonItemIds"] = Ids(care[0]);
        });
        var submitted = $"Submitted Fri 2{Nb}Oct, 9:14{Nb}am";
        string[] head =
        [
            "SUPPORT NOTES",
            "DATE: Friday 02.10.2026",
            "STAFF: Alex Pham, Priya Nair",
            $"As at Fri 2{Nb}Oct 2026, 9:14{Nb}am",
            "Exported by Manager",
            "Participants",
        ];
        string[] names = ["ANN ALPHA", "BOB BETA", "CY GAMMA"];
        string[] body =
        [
            "NAME: ANN ALPHA",
            "☐ Not In  ☑ Late Notice  ☐ Sick",
            "Short term goals",
            "☑ Make her own lunch",
            "Medium or Long term goal",
            "☐ Plan a holiday",
            "Medication",
            Medication,
            "Every note",
            "☑ Medication prompted",
            $"☑ {OtherEveryNote}",
            "Details: Used a ‘taxi’",
            "Community outing",
            "☑ Travelled by bus",
            "☐ Paid for own purchases",
            "☑ Other (outing)",
            "Details: Tram “86”",
            "Daily Support Notes",
            Narrative,
            $"Note written by: Priya Nair · {submitted}",

            // Nothing picked and nothing ticked: Every note alone, unticked, and there is no Medication box.
            "NAME: BOB BETA",
            "☐ Not In  ☐ Late Notice  ☐ Sick",
            "No goals set",
            "Every note",
            "☐ Medication prompted",
            $"☐ {OtherEveryNote}",
            "Daily Support Notes",
            "Bob's day was quiet.",
            $"Note written by: Alex Pham · {submitted}",

            // The Medication box was on and nothing was written: its heading stands alone.
            "NAME: CY GAMMA",
            "☑ Not In  ☐ Late Notice  ☑ Sick",
            "Medium or Long term goal",
            "☑ Live alone",
            "Medication",
            "Every note",
            "☐ Medication prompted",
            $"☐ {OtherEveryNote}",
            "Community outing",
            "☐ Travelled by bus",
            "☐ Paid for own purchases",
            "☐ Other (outing)",
            "Personal care",
            "☑ Showered with prompting",
            "Daily Support Notes",
            "Cy was away sick.",
            $"Note written by: Priya Nair · {submitted}",
        ];

        var docx = await manager.GetByteArrayAsync(
            "/api/reports/daily/2026-10-02/export?format=docx", TestContext.Current.CancellationToken);
        var pdf = await manager.GetByteArrayAsync(
            "/api/reports/daily/2026-10-02/export?format=pdf", TestContext.Current.CancellationToken);

        string[] wordIndex = [.. names.Select((name, i) => $"{name}\t{{PAGEREF p{i + 1} \\h}}–")];
        string[] pdfIndex = [.. names.Select((name, i) => $"{name} {i + 2}")];
        Assert.Equal([.. head, .. wordIndex, .. body], ReportText.WordParagraphs(docx));
        Assert.Equal(
            ReportText.Normalised(string.Join(' ', [.. head, .. pdfIndex, .. body])),
            ReportText.Normalised(ReportText.WithoutLeaders(string.Join(' ', ReportText.PdfWords(pdf)))));
    }

    // The note as the screen sends it: the template, the first save of the draft, then Submit with the same content.
    private async Task SubmitAsync(HttpClient client, Guid participantId, Action<JsonObject> fill)
    {
        var path = $"/api/participants/{participantId}/notes/{NoteDate:yyyy-MM-dd}";
        var form = await client.GetFromJsonAsync<JsonElement>($"{path}/draft", TestContext.Current.CancellationToken);
        var draft = Content(fill);
        draft["clientId"] = Guid.NewGuid();
        draft["seq"] = 1;
        draft["listsVersion"] = form.GetProperty("listsVersion").GetString();

        using var started = await client.PutWithTokenAsync(
            $"{path}/draft", await client.FetchTokenAsync(), draft, ifMatch: null);
        Assert.Equal(HttpStatusCode.Created, started.StatusCode);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{path}/versions")
        {
            Content = JsonContent.Create(Content(fill)),
        };
        request.Headers.Add(AntiforgeryTokens.Header, await client.FetchTokenAsync());
        request.Headers.Add(NoteVersionEndpoints.IdempotencyKeyHeader, Guid.NewGuid().ToString());
        using var submitted = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, submitted.StatusCode);
    }

    // A note's content, as the draft and Submit both carry it, with nothing ticked or picked unless
    // <paramref name="fill"/> says so.
    private static JsonObject Content(Action<JsonObject> fill)
    {
        var body = new JsonObject
        {
            ["baseVersion"] = 0,
            ["narrative"] = "Text.",
            ["tickedGoalIds"] = Ids(),
            ["pickedGroupIds"] = Ids(),
            ["tickedCommonItemIds"] = Ids(),
            ["isFlagged"] = false,
            ["flagReason"] = null,
        };
        fill(body);
        return body;
    }

    private static JsonArray Ids(params Guid[] ids) => new([.. ids.Select(id => (JsonNode?)JsonValue.Create(id))]);

    private static JsonObject Details(params (Guid Id, string Text)[] entries)
    {
        var details = new JsonObject();
        foreach (var (id, text) in entries)
        {
            details[id.ToString()] = text;
        }

        return details;
    }
}
