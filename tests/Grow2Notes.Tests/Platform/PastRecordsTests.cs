using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Features.Notes;
using Grow2Notes.Web.Platform;
using Grow2Notes.Web.Platform.Audit;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Grow2Notes.Tests.Platform;

/// <summary>
/// What archiving and deactivating leave of the past (design.md §3.7, A3, A29; S05.03.01 and S05.03.02), proved across
/// the app by the real endpoints: a participant, a goal, a common item, a common item group or a worker is archived or
/// deactivated, and every note, report and export written before it reads as it did, while what the change must end
/// does end. Each test adds an organisation of its own, with its users and participants, and changes none of the
/// seeded rows. The tests that compare a record before and after run on a clock that stands still, so that the
/// report's "As at" line and the export's "Exported by" line are equal, and whole texts are compared. A new note for an
/// archived participant is refused first, whatever the date or role (§5.5 item 4).
/// </summary>
[Collection<SqlServerCollection>]
public sealed class PastRecordsTests : IClassFixture<Grow2NotesFactory>, IAsyncDisposable
{
    private const string GivenName = "Morgan";

    private const string Narrative = $"{GivenName} made toast.";

    private readonly FakeTimeProvider clock = new(TestClock.Start);
    private readonly WebApplicationFactory<Program> app;

    public PastRecordsTests(Grow2NotesFactory factory) =>
        app = factory.WithWebHostBuilder(builder => builder.MapCookieSignIn().UseClock(clock));

    // Past notes, the report and the export read the same, whatever the participant's status. The history is read
    // before the yesterday draft is submitted, as the history lists drafts too.
    [Fact]
    public async Task Archiving_a_participant_changes_none_of_their_records_and_restoring_brings_them_back()
    {
        var org = await app.Services.AddOrganisationAsync();
        var participant = await app.Services.AddParticipantAsync(org, givenName: GivenName, familyName: "Blake");
        var today = Today();
        var first = today.AddDays(-3);
        var second = today.AddDays(-2);
        var yesterday = today.AddDays(-1);
        await AddSubmittedAsync(org, participant.Id, first, org.WorkerId);
        await AddSubmittedAsync(org, participant.Id, second, org.WorkerId);
        await app.Services.AddDraftAsync(
            org, new(participant.Id, yesterday, org.ManagerId, clock.GetUtcNow().UtcDateTime));
        using var worker = app.CreateHttpsClient().SignInAs(org.WorkerId);
        using var manager = app.CreateHttpsClient().SignInAs(org.ManagerId);
        Assert.True(await OnTodayAsync(worker, participant.Id));
        var listsVersion = await ListsVersionAsync(worker, participant.Id, today);
        var history = await BodyAsync(manager, $"/api/participants/{participant.Id}/notes");
        var read = await BodyAsync(manager, NotePath(participant.Id, first));
        var versions = await BodyAsync(manager, $"{NotePath(participant.Id, first)}/versions");
        var report = await ReportTextAsync(manager, first);
        var export = await ExportTextAsync(manager, participant.Id, first, second);
        Assert.Contains(GivenName, report);
        Assert.Contains(GivenName, export);

        await PostNoContentAsync(manager, $"/api/admin/participants/{participant.Id}/archive", participant.ETag);

        Assert.Equal(history, await BodyAsync(manager, $"/api/participants/{participant.Id}/notes"));
        Assert.Equal(read, await BodyAsync(manager, NotePath(participant.Id, first)));
        Assert.Equal(versions, await BodyAsync(manager, $"{NotePath(participant.Id, first)}/versions"));
        Assert.Equal(report, await ReportTextAsync(manager, first));
        Assert.Equal(export, await ExportTextAsync(manager, participant.Id, first, second));
        Assert.False(await OnTodayAsync(worker, participant.Id));

        foreach (var caller in new[] { worker, manager })
        {
            foreach (var date in new[] { today.AddDays(-4), today, today.AddDays(1) })
            {
                using var refused = await FirstSaveAsync(caller, participant.Id, date, listsVersion);
                Assert.Equal(
                    "note.participant_archived", await CodeAsync(refused, HttpStatusCode.UnprocessableEntity));
            }
        }

        Assert.Equal(history, await BodyAsync(manager, $"/api/participants/{participant.Id}/notes"));

        // An existing draft is still the author's to submit: only the start of a note is refused.
        using (var submitted = await SubmitAsync(manager, participant.Id, yesterday))
        {
            Assert.Equal(HttpStatusCode.Created, submitted.StatusCode);
        }

        await PostNoContentAsync(
            manager,
            $"/api/admin/participants/{participant.Id}/restore",
            IfMatch.ETag(await RowVersionAsync(manager, participant.Id)));

        Assert.True(await OnTodayAsync(worker, participant.Id));
        using var restored = await FirstSaveAsync(worker, participant.Id, today, listsVersion);
        Assert.Equal(HttpStatusCode.Created, restored.StatusCode);
    }

    // The note's snapshot holds its goal, group and item as they were, so that a note, its draft, its version, its
    // report and its export keep naming them, while a note started after the archive does not offer them.
    [Fact]
    public async Task Archiving_a_goal_an_item_and_a_group_keeps_them_in_earlier_notes_drafts_reports_and_exports()
    {
        var org = await app.Services.AddOrganisationAsync();
        var participant = await app.Services.AddParticipantAsync(org, givenName: GivenName, familyName: "Blake");
        var goal = await app.Services.AddGoalAsync(org, participant.Id, new("Cook a meal alone", 0));
        var group = await app.Services.AddGroupAsync(org.OrganisationId, new("Kitchen skills", 1));
        var item = await app.Services.AddItemAsync(org.OrganisationId, group.Id, new("Wash the dishes", 0));
        var today = Today();
        var first = today.AddDays(-3);
        var yesterday = today.AddDays(-1);
        var now = clock.GetUtcNow().UtcDateTime;
        await app.Services.AddSubmittedAsync(
            org,
            new(
                participant.Id,
                first,
                org.WorkerId,
                now.AddDays(-3),
                now.AddDays(-3).AddHours(1),
                Goals: [new(goal.Id, "Cook a meal alone", true)],
                Groups: [new(group.Id, "Kitchen skills", false, true, [new(item.Id, "Wash the dishes", true)])]));
        await app.Services.AddDraftAsync(
            org,
            new(
                participant.Id,
                today,
                org.WorkerId,
                now,
                Items:
                [
                    new(NoteSection.Goal, goal.Id, null, "Cook a meal alone", true),
                    new(NoteSection.CommonItem, item.Id, group.Id, "Wash the dishes", true),
                ],
                Groups: [new(group.Id, "Kitchen skills", false, true)]));
        using var worker = app.CreateHttpsClient().SignInAs(org.WorkerId);
        using var manager = app.CreateHttpsClient().SignInAs(org.ManagerId);
        var read = await BodyAsync(manager, NotePath(participant.Id, first));
        var version = await BodyAsync(manager, $"{NotePath(participant.Id, first)}/versions/1");
        var report = await ReportTextAsync(manager, first);
        var export = await ExportTextAsync(manager, participant.Id, first, first);
        var workingCopy = await BodyAsync(worker, $"{NotePath(participant.Id, today)}/draft");

        await PostNoContentAsync(manager, $"/api/admin/common-items/{item.Id}/archive", item.ETag);
        await PostNoContentAsync(manager, $"/api/admin/common-item-groups/{group.Id}/archive", group.ETag);
        await PostNoContentAsync(manager, $"/api/admin/goals/{goal.Id}/archive", goal.ETag);

        Assert.Equal(read, await BodyAsync(manager, NotePath(participant.Id, first)));
        Assert.Equal(version, await BodyAsync(manager, $"{NotePath(participant.Id, first)}/versions/1"));
        Assert.Equal(report, await ReportTextAsync(manager, first));
        Assert.Equal(export, await ExportTextAsync(manager, participant.Id, first, first));
        Assert.Equal(workingCopy, await BodyAsync(worker, $"{NotePath(participant.Id, today)}/draft"));
        foreach (var text in new[] { "Cook a meal alone", "Kitchen skills", "Wash the dishes" })
        {
            Assert.Contains(text, read);
            Assert.Contains(text, report);
            Assert.Contains(text, export);
            Assert.Contains(text, workingCopy);
        }

        var template = await BodyAsync(manager, $"{NotePath(participant.Id, yesterday)}/draft");
        foreach (var id in new[] { goal.Id, group.Id, item.Id })
        {
            Assert.DoesNotContain(id.ToString(), template);
        }

        using var created = await FirstSaveAsync(
            manager, participant.Id, yesterday, ListsVersionOf(template));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
    }

    // Deactivation ends the worker's sessions and sign-in, and nothing they wrote or are named in.
    [Fact]
    public async Task
        A_deactivated_worker_is_signed_out_everywhere_keeps_their_notes_and_name_and_signs_in_again_once_reactivated()
    {
        var org = await app.Services.AddOrganisationAsync();
        var participant = await app.Services.AddParticipantAsync(org, givenName: GivenName, familyName: "Blake");
        var author = await app.AddPasswordUserAsync(org);
        using var authenticator = await TestAuthenticator.RegisterAsync(app.Services, author.Id);
        var today = Today();
        var first = today.AddDays(-3);
        var now = clock.GetUtcNow().UtcDateTime;
        await AddSubmittedAsync(org, participant.Id, first, author.Id);
        await app.Services.AddDraftAsync(org, new(participant.Id, today, author.Id, now));
        using var devices = await SignedInDevices.SignInAsync(app, clock, author.Id);
        using var manager = app.CreateHttpsClient().SignInAs(org.ManagerId);
        using var wrongPassword = app.CreateHttpsClient();
        using var baselineResponse = await wrongPassword.SendPasswordAsync(author.Email, "Another-passw0rd");
        var baseline = await Refusal.OfAsync(baselineResponse);
        Assert.Equal(HttpStatusCode.Unauthorized, baseline.Status);

        await PostNoContentAsync(
            manager, $"/api/admin/users/{author.Id}/deactivate", await UserETagAsync(author.Id));

        await devices.AssertNextRequestsAsync(
            HttpStatusCode.Unauthorized,
            NextRequest.Get($"{NotePath(participant.Id, today)}/draft"),
            SignedInDevices.Ping);
        using var password = app.CreateHttpsClient();
        using var passwordResponse = await password.SendPasswordAsync(author.Email, PasswordSignIn.Password);
        Assert.Equal(baseline, await Refusal.OfAsync(passwordResponse));
        using var passkey = app.CreateHttpsClient();
        using var passkeyResponse = await passkey.SignInWithPasskeyAsync(authenticator);
        Assert.Equal(baseline, await Refusal.OfAsync(passkeyResponse));

        using (var read = JsonDocument.Parse(await BodyAsync(manager, NotePath(participant.Id, first))))
        {
            var name = read.RootElement.GetProperty("author").GetProperty("displayName").GetString();
            Assert.Equal(PasswordSignIn.DisplayName, name);
        }

        using (var history = JsonDocument.Parse(await BodyAsync(manager, $"/api/participants/{participant.Id}/notes")))
        {
            var row = Assert.Single(
                history.RootElement.GetProperty("notes").EnumerateArray(),
                note => note.GetProperty("noteDate").GetString() == Iso(first));
            Assert.Equal(PasswordSignIn.DisplayName, row.GetProperty("authorDisplayName").GetString());
        }

        Assert.Contains(PasswordSignIn.DisplayName, await ReportTextAsync(manager, first));
        Assert.Contains(PasswordSignIn.DisplayName, await ExportTextAsync(manager, participant.Id, first, first));

        using (var draft = JsonDocument.Parse(await BodyAsync(manager, NotePath(participant.Id, today))))
        {
            Assert.Equal(
                PasswordSignIn.DisplayName, draft.RootElement.GetProperty("authorDisplayName").GetString());
        }

        await PostNoContentAsync(manager, $"{NotePath(participant.Id, today)}/discard", etag: null);
        using var worker = app.CreateHttpsClient().SignInAs(org.WorkerId);
        using var created = await FirstSaveAsync(
            worker, participant.Id, today, await ListsVersionAsync(worker, participant.Id, today));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        await PostNoContentAsync(
            manager, $"/api/admin/users/{author.Id}/reactivate", await UserETagAsync(author.Id));

        using var again = app.CreateHttpsClient();
        using var signedIn = await again.SignInWithPasswordAsync(
            author.Email, PasswordSignIn.Password, AuthenticatorCodes.Now(author.AuthenticatorKey));
        Assert.Equal(HttpStatusCode.OK, signedIn.StatusCode);
        var events = await EventTypesAboutAsync(author.Id);
        Assert.Single(events, type => type == AuditEventTypes.User.Deactivated);
        Assert.Single(events, type => type == AuditEventTypes.User.Reactivated);
    }

    public ValueTask DisposeAsync() => app.DisposeAsync();

    private static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string NotePath(Guid participantId, DateOnly noteDate) =>
        $"/api/participants/{participantId}/notes/{Iso(noteDate)}";

    private static string ListsVersionOf(string form)
    {
        using var document = JsonDocument.Parse(form);
        return document.RootElement.GetProperty("listsVersion").GetString()!;
    }

    private DateOnly Today() => app.Services.GetRequiredService<MelbourneClock>().Today();

    private Task<Guid> AddSubmittedAsync(SeededOrganisation org, Guid participantId, DateOnly noteDate, Guid authorId)
    {
        var started = clock.GetUtcNow().UtcDateTime.AddDays(-3);
        return app.Services.AddSubmittedAsync(
            org, new(participantId, noteDate, authorId, started, started.AddHours(1)));
    }

    // A read that must answer 200, as the raw JSON, so that bodies are compared as the SPA is sent them.
    private static async Task<string> BodyAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<string> CodeAsync(HttpResponseMessage response, HttpStatusCode status)
    {
        using var problem = JsonDocument.Parse(await response.ReadProblemAsync(status));
        return problem.RootElement.GetProperty(ErrorCode.Extension).GetString()!;
    }

    private static async Task<string> ReportTextAsync(HttpClient manager, DateOnly date) =>
        TextOf(await manager.GetByteArrayAsync(
            $"/api/reports/daily/{Iso(date)}/export?format=docx", TestContext.Current.CancellationToken));

    private static async Task<string> ExportTextAsync(
        HttpClient manager, Guid participantId, DateOnly from, DateOnly to) =>
        TextOf(await manager.GetByteArrayAsync(
            $"/api/reports/participants/{participantId}/export?from={Iso(from)}&to={Iso(to)}"
            + "&format=docx&includeHistory=true",
            TestContext.Current.CancellationToken));

    private static string TextOf(byte[] docx) => string.Join('\n', ReportText.WordParagraphs(docx));

    private static async Task<string> ListsVersionAsync(HttpClient client, Guid participantId, DateOnly noteDate) =>
        ListsVersionOf(await BodyAsync(client, $"{NotePath(participantId, noteDate)}/draft"));

    // A note's first save, as the form sends it: with the lists' version of the template that it opened with.
    private static async Task<HttpResponseMessage> FirstSaveAsync(
        HttpClient client, Guid participantId, DateOnly noteDate, string listsVersion) =>
        await client.PutWithTokenAsync(
            $"{NotePath(participantId, noteDate)}/draft",
            await client.FetchTokenAsync(),
            new
            {
                clientId = Guid.NewGuid(),
                seq = 1,
                listsVersion,
                narrative = Narrative,
                tickedGoalIds = Array.Empty<Guid>(),
                pickedGroupIds = Array.Empty<Guid>(),
                tickedCommonItemIds = Array.Empty<Guid>(),
                isFlagged = false,
                flagReason = (string?)null,
            },
            ifMatch: null);

    // Submit, as the screen sends it, on base 0 with nothing ticked and the flag unticked.
    private static async Task<HttpResponseMessage> SubmitAsync(HttpClient client, Guid participantId, DateOnly noteDate)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{NotePath(participantId, noteDate)}/versions")
        {
            Content = JsonContent.Create(new
            {
                baseVersion = 0,
                narrative = Narrative,
                tickedGoalIds = Array.Empty<Guid>(),
                pickedGroupIds = Array.Empty<Guid>(),
                tickedCommonItemIds = Array.Empty<Guid>(),
                isFlagged = false,
                flagReason = (string?)null,
            }),
        };
        request.Headers.Add(AntiforgeryTokens.Header, await client.FetchTokenAsync());
        request.Headers.Add(NoteVersionEndpoints.IdempotencyKeyHeader, Guid.NewGuid().ToString());
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static async Task PostNoContentAsync(HttpClient client, string path, string? etag)
    {
        using var response = await client.PostWithTokenAsync(path, await client.FetchTokenAsync(), ifMatch: etag);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    // Whether the participant is in the day's list. TodayPage filters exactly this list in memory for Find a
    // participant (matchesName over list.participants) and no search endpoint exists, so a participant missing here
    // is missing from Today and from its search.
    private static async Task<bool> OnTodayAsync(HttpClient client, Guid participantId)
    {
        using var today = JsonDocument.Parse(await BodyAsync(client, "/api/today"));
        return today.RootElement.GetProperty("participants").EnumerateArray()
            .Any(row => row.GetProperty("id").GetGuid() == participantId);
    }

    // The version that the participants list gives the screen, for the restore.
    private static async Task<string> RowVersionAsync(HttpClient manager, Guid participantId)
    {
        using var list = JsonDocument.Parse(await BodyAsync(manager, "/api/admin/participants?includeArchived=true"));
        return list.RootElement.EnumerateArray()
            .Single(row => row.GetProperty("id").GetGuid() == participantId)
            .GetProperty("rowVersion").GetString()!;
    }

    private async Task<string> UserETagAsync(Guid userId)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByIdAsync(userId.ToString());
        Assert.NotNull(user?.ConcurrencyStamp);
        return IfMatch.ETag(user.ConcurrencyStamp);
    }

    // Read in SQL, so that the tenant filter decides nothing.
    private async Task<List<string>> EventTypesAboutAsync(Guid entityId)
    {
        await using var scope = app.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Database
            .SqlQuery<string>($"SELECT [EventType] AS [Value] FROM [AuditEvent] WHERE [EntityId] = {entityId}")
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    // What a refused sign-in shows a person, so that two refusals can be told apart or not. A refusal may set other
    // cookies (the passkey ceremony's is cleared); only a session cookie would sign the person in.
    private sealed record Refusal(HttpStatusCode Status, string? ContentType, string Body, bool SetsASession)
    {
        // A problem's traceId names its request, so that it is the one member that two refusals never share.
        public static async Task<Refusal> OfAsync(HttpResponseMessage response)
        {
            var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            var contentType = response.Content.Headers.ContentType?.MediaType;
            if (contentType == "application/problem+json")
            {
                var problem = JsonNode.Parse(body)!.AsObject();
                problem.Remove("traceId");
                body = problem.ToJsonString();
            }

            var setsASession = response.SetCookies().Any(cookie => cookie.Name.Value == CookieSignIn.SessionCookie);
            return new(response.StatusCode, contentType, body, setsASession);
        }
    }
}
