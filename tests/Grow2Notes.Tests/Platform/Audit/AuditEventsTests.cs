using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Features.Mcp.OAuth;
using Grow2Notes.Web.Features.Notes;
using Grow2Notes.Web.Platform;
using Grow2Notes.Web.Platform.Audit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;

namespace Grow2Notes.Tests.Platform.Audit;

/// <summary>
/// The shared audit-events test (design.md §5.3, §5.4, §14 M5; S05.02.01): one table-driven test with a row for each
/// event type of the catalogue, and for each trigger that §5.4 names for an event type that has more than one
/// (<c>user.setup_link_sent</c> by Resend invite and by Reset sign-in; <c>common_item.updated</c> by Reword and by
/// Move), which triggers the event through the real endpoint or operator command, as the app's callers do, and finds
/// exactly one new entry, with its <c>EventType</c>, <c>OrganisationId</c>, <c>ActorUserId</c>, <c>EntityType</c>,
/// <c>EntityId</c>, <c>ParticipantId</c> and the values in its <c>Details</c>. Each task that writes an event adds its
/// row here. The table is complete, and <see cref="AuditTypesTests"/> fails while a constant and the rows differ. Rows
/// act in seeded organisation A and add rows of their own to it, changing no seeded row, but for a change to an
/// organisation's own row, such as its guide prompts, or one whose values depend on every row of the organisation's
/// list, such as a new group's place among its groups, which a row makes in an organisation that it adds, and for a
/// change to every organisation, which a row makes on a database of its own (tests/README.md).
/// </summary>
[Collection<SqlServerCollection>]
public sealed class AuditEventsTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    : IClassFixture<Grow2NotesFactory>, IAsyncDisposable
{
    /// <summary>
    /// Each row: what triggers its event, through the real endpoint or operator command, which gives the entry that it
    /// must write. A row is named by its event type, followed, for an event type with more than one trigger, by " by "
    /// and the trigger.
    /// </summary>
    private static readonly Dictionary<string, Func<AuditEventsTests, Task<WrittenEvent>>> Rows = new()
    {
        [AuditEventTypes.Auth.SignInSucceeded] = test => test.SignInAsync(),
        [AuditEventTypes.Auth.SignInFailed] = test => test.FailPasswordAsync(),
        [AuditEventTypes.Auth.MfaFailed] = test => test.FailCodeAsync(),
        [AuditEventTypes.Auth.LockedOut] = test => test.GetLockedOutAsync(),
        [AuditEventTypes.Auth.SignedOut] = test => test.SignOutAsync(),
        [AuditEventTypes.Auth.SetupCompleted] = test => test.CompleteSetupAsync(),
        [AuditEventTypes.User.Invited] = test => test.InviteUserAsync(),
        [AuditEventTypes.User.Updated] = test => test.RenameUserAsync(),
        [$"{AuditEventTypes.User.SetupLinkSent} by Resend invite"] = test => test.ResendInviteAsync(),
        [$"{AuditEventTypes.User.SetupLinkSent} by Reset sign-in"] = test => test.ResetSignInAsync(),
        [AuditEventTypes.User.Deactivated] = test => test.DeactivateUserAsync(),
        [AuditEventTypes.User.Reactivated] = test => test.ReactivateUserAsync(),
        [AuditEventTypes.Participant.Created] = test => test.AddParticipantAsync(),
        [AuditEventTypes.Participant.Updated] = test => test.CorrectParticipantAsync(),
        [AuditEventTypes.Participant.Archived] = test => test.ArchiveParticipantAsync(),
        [AuditEventTypes.Participant.Restored] = test => test.RestoreParticipantAsync(),
        [AuditEventTypes.Goal.Created] = test => test.AddGoalAsync(),
        [AuditEventTypes.Goal.Updated] = test => test.RewordGoalAsync(),
        [AuditEventTypes.Goal.Reordered] = test => test.ReorderGoalsAsync(),
        [AuditEventTypes.Goal.Archived] = test => test.ArchiveGoalAsync(),
        [AuditEventTypes.Goal.Restored] = test => test.RestoreGoalAsync(),
        [AuditEventTypes.CommonItem.Created] = test => test.AddCommonItemAsync(),
        [$"{AuditEventTypes.CommonItem.Updated} by Reword"] = test => test.RewordCommonItemAsync(),
        [$"{AuditEventTypes.CommonItem.Updated} by Move"] = test => test.MoveCommonItemAsync(),
        [AuditEventTypes.CommonItem.Reordered] = test => test.ReorderCommonItemsAsync(),
        [AuditEventTypes.CommonItem.Archived] = test => test.ArchiveCommonItemAsync(),
        [AuditEventTypes.CommonItem.Restored] = test => test.RestoreCommonItemAsync(),
        [AuditEventTypes.CommonItemGroup.Created] = test => test.AddCommonItemGroupAsync(),
        [AuditEventTypes.CommonItemGroup.Updated] = test => test.RenameCommonItemGroupAsync(),
        [AuditEventTypes.CommonItemGroup.Reordered] = test => test.ReorderCommonItemGroupsAsync(),
        [AuditEventTypes.CommonItemGroup.Archived] = test => test.ArchiveCommonItemGroupAsync(),
        [AuditEventTypes.CommonItemGroup.Restored] = test => test.RestoreCommonItemGroupAsync(),
        [AuditEventTypes.GuidePrompts.Updated] = test => test.SaveGuidePromptsAsync(),
        [AuditEventTypes.Note.DraftStarted] = test => test.StartDraftAsync(),
        [AuditEventTypes.Note.Discarded] = test => test.DiscardDraftAsync(),
        [AuditEventTypes.Note.Submitted] = test => test.SubmitNoteAsync(),
        [AuditEventTypes.Note.Edited] = test => test.EditNoteAsync(),
        [AuditEventTypes.Note.Reviewed] = test => test.ReviewNoteAsync(),
        [AuditEventTypes.Report.Downloaded] = test => test.DownloadDailyReportAsync(),
        [AuditEventTypes.Participant.Exported] = test => test.ExportParticipantAsync(),
        [AuditEventTypes.Admin.Bootstrap] = test => test.RunBootstrapAsync(),
        [AuditEventTypes.Admin.SignInReset] = test => test.RunResetSignInAsync(),
        [AuditEventTypes.Admin.SignOutAll] = test => test.RunSignOutAllAsync(),
        [AuditEventTypes.Admin.McpClientAdded] = test => test.RunMcpClientAddAsync(),
        [AuditEventTypes.Admin.McpClientUpdated] = test => test.RunMcpClientAddAgainAsync(),
        [AuditEventTypes.Admin.McpClientRemoved] = test => test.RunMcpClientRemoveAsync(),
        [AuditEventTypes.Mcp.Authorised] = test => test.AllowAssistantAsync(),
        [AuditEventTypes.Mcp.AuthorisationRefused] = test => test.CancelAssistantAsync(),
    };

    /// <summary>
    /// The rows whose trigger changes every organisation, each with the name of the migrated database of its own, with
    /// nothing seeded, on which it runs, so that the one organisation it adds there is the only one.
    /// </summary>
    private static readonly Dictionary<string, string> DatabasesOfTheirOwn = new()
    {
        [AuditEventTypes.Admin.SignOutAll] = "AuditEventsSignOutAll",
        [AuditEventTypes.Admin.McpClientAdded] = "AuditEventsMcpClientAdded",
        [AuditEventTypes.Admin.McpClientUpdated] = "AuditEventsMcpClientUpdated",
        [AuditEventTypes.Admin.McpClientRemoved] = "AuditEventsMcpClientRemoved",
    };

    // The day of the report and export rows: a made-up one (A35), so that what they expect never depends on the real
    // clock crossing midnight in Melbourne.
    private static readonly DateOnly ReportDate = new(2026, 10, 1);

    private static readonly string ReportDay = ReportDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private const string McpClientId = "audit-assistant";
    private const string McpClientName = "Audit Assistant";

    private static readonly string[] McpClientAdd =
    [
        "mcp-client", "add", "--client-id", McpClientId, "--redirect-uri", "https://assistant.example/callback",
        "--name", McpClientName, "--company", "Example AI", "--data-location", "In the United States",
    ];

    private readonly SqlServerFixture sqlServer = sqlServer;
    private readonly SeededOrganisation a = sqlServer.Seeded.A;

    // The app on the database of the row's own, for a row in DatabasesOfTheirOwn.
    private WebApplicationFactory<Program>? appOnItsOwnDatabase;

    public static TheoryData<string> RowNames { get; } = new(Rows.Keys);

    /// <summary>The event types that the rows trigger, which <see cref="AuditTypesTests"/> compares with the constants.
    /// </summary>
    internal static IEnumerable<string> RowEventTypes => Rows.Keys.Select(EventTypeOf).Distinct();

    // The app that the row acts through, and on whose database the test finds the entry.
    private WebApplicationFactory<Program> App => appOnItsOwnDatabase ?? factory;

    [Theory]
    [MemberData(nameof(RowNames))]
    public async Task Its_trigger_writes_exactly_one_entry_with_its_fields_and_values(string row)
    {
        if (DatabasesOfTheirOwn.TryGetValue(row, out var database))
        {
            appOnItsOwnDatabase = await OnADatabaseOfItsOwnAsync(database);
        }

        var lastEventId = await LastEventIdAsync();

        var expected = await Rows[row](this);

        var written = Assert.Single(await EventsAfterAsync(lastEventId));
        Assert.Equal(EventTypeOf(row), expected.EventType);
        Assert.Equal(expected with { Details = null }, written with { Details = null });
        Assert.True(
            JsonNode.DeepEquals(Parse(expected.Details), Parse(written.Details)),
            $"Details: expected {expected.Details}, written {written.Details}.");
    }

    public ValueTask DisposeAsync() => appOnItsOwnDatabase?.DisposeAsync() ?? ValueTask.CompletedTask;

    // A row's name is its event type, which has no space, then, if it has one, " by " and the trigger.
    private static string EventTypeOf(string row) => row.Split(' ')[0];

    private static JsonNode? Parse(string? details) => details is null ? null : JsonNode.Parse(details);

    /// <summary>
    /// <c>auth.signin_succeeded</c>: a worker added to A with a password and a code signs in with
    /// <c>POST /api/auth/login</c> and <c>POST /api/auth/login/totp</c>, whose code step writes the entry, which
    /// records the user as the actor and the entity, with no participant and no <c>Details</c>, so no address (A28).
    /// </summary>
    private async Task<WrittenEvent> SignInAsync()
    {
        var user = await factory.AddPasswordUserAsync(a);
        using var client = factory.CreateHttpsClient();

        using var response = await client.SignInWithPasswordAsync(
            user.Email, PasswordSignIn.Password, AuthenticatorCodes.Now(user.AuthenticatorKey));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return AuthEvent(AuditEventTypes.Auth.SignInSucceeded, user.Id);
    }

    /// <summary>
    /// <c>auth.signin_failed</c>: a worker added to A sends a wrong password to <c>POST /api/auth/login</c>, which
    /// answers 401 and records the user as the actor and the entity, as <c>auth.signin_succeeded</c> does.
    /// </summary>
    private async Task<WrittenEvent> FailPasswordAsync()
    {
        var user = await factory.AddPasswordUserAsync(a);
        using var client = factory.CreateHttpsClient();

        using var response = await client.SendPasswordAsync(user.Email, $"{PasswordSignIn.Password} (wrong)");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        return AuthEvent(AuditEventTypes.Auth.SignInFailed, user.Id);
    }

    /// <summary>
    /// <c>auth.mfa_failed</c>: a worker added to A sends the right password, which answers the code step, then a wrong
    /// code to <c>POST /api/auth/login/totp</c>, which answers 401 and records the user as the actor and the entity.
    /// The code is the right key's, for a step ten minutes away, which Identity counts as wrong.
    /// </summary>
    private async Task<WrittenEvent> FailCodeAsync()
    {
        var user = await factory.AddPasswordUserAsync(a);
        using var client = factory.CreateHttpsClient();
        using (var passwordStep = await client.SendPasswordAsync(user.Email, PasswordSignIn.Password))
        {
            Assert.Equal(HttpStatusCode.OK, passwordStep.StatusCode);
        }

        using var response = await client.SendCodeAsync(
            AuthenticatorCodes.At(user.AuthenticatorKey, DateTimeOffset.UtcNow.AddMinutes(10)));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        return AuthEvent(AuditEventTypes.Auth.MfaFailed, user.Id);
    }

    /// <summary>
    /// <c>auth.locked_out</c>: a worker added to A, whose failed count is raised to one short of Identity's limit,
    /// which writes no event, sends one wrong password, which answers 401 and locks them out, so records the user as
    /// the actor and the entity, and not <c>auth.signin_failed</c>.
    /// </summary>
    private async Task<WrittenEvent> GetLockedOutAsync()
    {
        var user = await factory.AddPasswordUserAsync(a);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var stored = await users.FindByIdAsync(user.Id.ToString());
            Assert.NotNull(stored);
            var limit = scope.ServiceProvider.GetRequiredService<IOptions<IdentityOptions>>()
                .Value.Lockout.MaxFailedAccessAttempts;
            for (var failed = 1; failed < limit; failed++)
            {
                Assert.True((await users.AccessFailedAsync(stored)).Succeeded);
            }
        }

        using var client = factory.CreateHttpsClient();
        using var response = await client.SendPasswordAsync(user.Email, $"{PasswordSignIn.Password} (wrong)");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        return AuthEvent(AuditEventTypes.Auth.LockedOut, user.Id);
    }

    /// <summary>
    /// <c>auth.signed_out</c>: a worker added to A, signed in with the cookie sign-in, which writes no event, signs
    /// out with <c>POST /api/auth/logout</c>, which answers 204 and records the user as the actor and the entity.
    /// </summary>
    private async Task<WrittenEvent> SignOutAsync()
    {
        var worker = await factory.Services.AddUserAsync(a, UserRole.Worker, PasswordSignIn.DisplayName);
        await using var app = factory.WithWebHostBuilder(builder => builder.MapCookieSignIn());
        using var client = app.CreateHttpsClient();
        await client.SignInWithCookieAsync(worker);

        using var response = await client.PostWithTokenAsync("/api/auth/logout", await client.FetchTokenAsync());

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        return AuthEvent(AuditEventTypes.Auth.SignedOut, worker);
    }

    /// <summary>
    /// <c>auth.setup_completed</c>: a worker invited to A, which writes no event, follows their setup link and sets a
    /// password and a code with <c>POST /api/auth/setup/password</c>, which answers 200 and records the user as the
    /// actor and the entity.
    /// </summary>
    private async Task<WrittenEvent> CompleteSetupAsync()
    {
        var link = await factory.InviteAsync(a);
        using var client = factory.CreateHttpsClient();
        var (token, _) = await client.StartSetupAsync(link);
        string key;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var stored = await users.FindByIdAsync(link.UserId);
            Assert.NotNull(stored);
            key = await users.GetAuthenticatorKeyAsync(stored) ?? throw new InvalidOperationException("No key.");
        }

        using var response = await client.PostWithTokenAsync(
            "/api/auth/setup/password",
            token,
            new { password = "applecrumble", totpCode = AuthenticatorCodes.Now(key) });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return AuthEvent(AuditEventTypes.Auth.SetupCompleted, Guid.Parse(link.UserId));
    }

    // What a sign-in event of a user of A writes: the user is the actor and the entity, with no participant, and no
    // Details.
    private WrittenEvent AuthEvent(string eventType, Guid userId) =>
        new(eventType, a.OrganisationId, userId, AuditEntityTypes.User, userId, null, null);

    /// <summary>
    /// <c>user.invited</c>: A's manager invites a worker with an address of the row's own with
    /// <c>POST /api/admin/users</c>, which records the new user as the entity, with no participant, and their address,
    /// name and role as <c>new</c> alone, as sent.
    /// </summary>
    private async Task<WrittenEvent> InviteUserAsync()
    {
        var email = $"avery.brooks.{Guid.NewGuid():N}@example.org";
        using var client = factory.CreateHttpsClient().SignInAs(a.ManagerId);
        using var response = await client.PostWithTokenAsync(
            "/api/admin/users",
            await client.FetchTokenAsync(),
            new { displayName = "Avery Brooks", email, role = "Worker" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var values = new JsonObject { ["email"] = email, ["displayName"] = "Avery Brooks", ["role"] = "Worker" };
        return new(
            AuditEventTypes.User.Invited,
            a.OrganisationId,
            a.ManagerId,
            AuditEntityTypes.User,
            (await StoredUserAsync(email)).Id,
            null,
            new JsonObject { ["new"] = values }.ToJsonString());
    }

    /// <summary>
    /// <c>user.updated</c>: A's manager renames an Active worker added for the row with
    /// <c>PUT /api/admin/users/{id}</c> and the worker's ETag, which records the worker as the entity, with no
    /// participant, and the old and new name alone, in <c>ChangeDetails.Of</c>'s shape, not the address or the role,
    /// sent as they were.
    /// </summary>
    private async Task<WrittenEvent> RenameUserAsync()
    {
        const string newName = "Sam Taylor-Jones";
        var worker = await AddWorkerAsync(UserStatus.Active);
        using var client = factory.CreateHttpsClient().SignInAs(a.ManagerId);
        using var response = await client.PutWithTokenAsync(
            $"/api/admin/users/{worker.Id}",
            await client.FetchTokenAsync(),
            new { displayName = newName, email = worker.Email, role = "Worker" },
            IfMatch.ETag(worker.ConcurrencyStamp!));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var details = new JsonObject
        {
            ["old"] = new JsonObject { ["displayName"] = PasswordSignIn.DisplayName },
            ["new"] = new JsonObject { ["displayName"] = newName },
        };
        return new(
            AuditEventTypes.User.Updated,
            a.OrganisationId,
            a.ManagerId,
            AuditEntityTypes.User,
            worker.Id,
            null,
            details.ToJsonString());
    }

    /// <summary>
    /// <c>user.setup_link_sent</c>, by Resend invite: A's manager resends the invite of a worker invited for the row
    /// through the invite core, which writes no event, with <c>POST /api/admin/users/{id}/setup-link</c> and the
    /// worker's ETag, which records the worker as the entity, with no participant and no <c>Details</c>, so no address
    /// (A28).
    /// </summary>
    private async Task<WrittenEvent> ResendInviteAsync()
    {
        var invited = await factory.InviteAsync(a);
        var user = await StoredUserAsync(invited.Email);
        using var client = factory.CreateHttpsClient().SignInAs(a.ManagerId);
        using var response = await client.PostWithTokenAsync(
            $"/api/admin/users/{user.Id}/setup-link",
            await client.FetchTokenAsync(),
            ifMatch: IfMatch.ETag(user.ConcurrencyStamp!));
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        return new(
            AuditEventTypes.User.SetupLinkSent,
            a.OrganisationId,
            a.ManagerId,
            AuditEntityTypes.User,
            user.Id,
            null,
            null);
    }

    /// <summary>
    /// <c>user.setup_link_sent</c>, by Reset sign-in: A's manager resets the sign-in of an Active worker added for the
    /// row with <c>POST /api/admin/users/{id}/setup-link</c> and the worker's ETag, which records the worker as the
    /// entity, with no participant and no <c>Details</c>, as a resend does.
    /// </summary>
    private async Task<WrittenEvent> ResetSignInAsync()
    {
        var worker = await AddWorkerAsync(UserStatus.Active);
        await PostAsync($"/api/admin/users/{worker.Id}/setup-link", IfMatch.ETag(worker.ConcurrencyStamp!));

        return new(
            AuditEventTypes.User.SetupLinkSent,
            a.OrganisationId,
            a.ManagerId,
            AuditEntityTypes.User,
            worker.Id,
            null,
            null);
    }

    /// <summary>
    /// <c>user.deactivated</c>: A's manager deactivates an Active worker added for the row with
    /// <c>POST /api/admin/users/{id}/deactivate</c> and the worker's ETag, which records the worker as the entity, with
    /// no participant and no <c>Details</c>, the event type saying what changed.
    /// </summary>
    private async Task<WrittenEvent> DeactivateUserAsync()
    {
        var worker = await AddWorkerAsync(UserStatus.Active);
        await PostAsync($"/api/admin/users/{worker.Id}/deactivate", IfMatch.ETag(worker.ConcurrencyStamp!));

        return new(
            AuditEventTypes.User.Deactivated,
            a.OrganisationId,
            a.ManagerId,
            AuditEntityTypes.User,
            worker.Id,
            null,
            null);
    }

    /// <summary>
    /// <c>user.reactivated</c>: A's manager reactivates a Deactivated worker added for the row with
    /// <c>POST /api/admin/users/{id}/reactivate</c> and the worker's ETag, which records the worker as the entity, with
    /// no participant and no <c>Details</c>, the event type saying what changed.
    /// </summary>
    private async Task<WrittenEvent> ReactivateUserAsync()
    {
        var worker = await AddWorkerAsync(UserStatus.Deactivated);
        await PostAsync($"/api/admin/users/{worker.Id}/reactivate", IfMatch.ETag(worker.ConcurrencyStamp!));

        return new(
            AuditEventTypes.User.Reactivated,
            a.OrganisationId,
            a.ManagerId,
            AuditEntityTypes.User,
            worker.Id,
            null,
            null);
    }

    /// <summary>
    /// <c>participant.created</c>: A's manager adds a participant with <c>POST /api/admin/participants</c>, which
    /// records the new participant as the entity and the participant, and their values as <c>new</c> alone, each named
    /// as design.md §6.6's list names it, the names as stored once their ends are trimmed.
    /// </summary>
    private async Task<WrittenEvent> AddParticipantAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = factory.CreateHttpsClient().SignInAs(a.ManagerId);
        using var response = await client.PostWithTokenAsync(
            "/api/admin/participants",
            await client.FetchTokenAsync(),
            new { givenName = " Jane", familyName = "Citizen ", dateOfBirth = "1987-03-07" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var id = (await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("id").GetGuid();

        var values = new JsonObject
        {
            ["givenName"] = "Jane",
            ["familyName"] = "Citizen",
            ["dateOfBirth"] = "1987-03-07",
            ["status"] = "Active",
        };
        return new(
            AuditEventTypes.Participant.Created,
            a.OrganisationId,
            a.ManagerId,
            AuditEntityTypes.Participant,
            id,
            id,
            new JsonObject { ["new"] = values }.ToJsonString());
    }

    /// <summary>
    /// <c>participant.updated</c>: A's manager corrects the family name and date of birth of a participant added for
    /// the row with <c>PUT /api/admin/participants/{id}</c>, which records the participant as the entity and the
    /// participant, and the old and new value of the two fields that changed alone, not the given name, sent as it was.
    /// </summary>
    private async Task<WrittenEvent> CorrectParticipantAsync()
    {
        var participant = await factory.Services.AddParticipantAsync(a);
        using var client = factory.CreateHttpsClient().SignInAs(a.ManagerId);
        using var response = await client.PutWithTokenAsync(
            $"/api/admin/participants/{participant.Id}",
            await client.FetchTokenAsync(),
            new { givenName = "Jane", familyName = " Smith", dateOfBirth = "1987-03-17" },
            participant.ETag);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var details = new JsonObject
        {
            ["old"] = new JsonObject { ["familyName"] = "Citizen", ["dateOfBirth"] = "1987-03-07" },
            ["new"] = new JsonObject { ["familyName"] = "Smith", ["dateOfBirth"] = "1987-03-17" },
        };
        return new(
            AuditEventTypes.Participant.Updated,
            a.OrganisationId,
            a.ManagerId,
            AuditEntityTypes.Participant,
            participant.Id,
            participant.Id,
            details.ToJsonString());
    }

    /// <summary>
    /// <c>participant.archived</c>: A's manager archives an active participant added for the row with
    /// <c>POST /api/admin/participants/{id}/archive</c>, which records the participant as the entity and the
    /// participant, and their status and the time they were archived at, before and after, the time as stored.
    /// </summary>
    private async Task<WrittenEvent> ArchiveParticipantAsync()
    {
        var participant = await factory.Services.AddParticipantAsync(a);
        await PostAsync($"/api/admin/participants/{participant.Id}/archive", participant.ETag);

        var details = new JsonObject
        {
            ["old"] = new JsonObject { ["status"] = "Active", ["archivedAtUtc"] = null },
            ["new"] = new JsonObject
            {
                ["status"] = "Archived", ["archivedAtUtc"] = await StoredArchivedAtUtcAsync(participant.Id),
            },
        };
        return new(
            AuditEventTypes.Participant.Archived,
            a.OrganisationId,
            a.ManagerId,
            AuditEntityTypes.Participant,
            participant.Id,
            participant.Id,
            details.ToJsonString());
    }

    /// <summary>
    /// <c>participant.restored</c>: A's manager restores an archived participant added for the row with
    /// <c>POST /api/admin/participants/{id}/restore</c>, which records the participant as the entity and the
    /// participant, and their status and the time they had been archived at, before and after, which is none.
    /// </summary>
    private async Task<WrittenEvent> RestoreParticipantAsync()
    {
        var participant = await factory.Services.AddParticipantAsync(a, archived: true);
        await PostAsync($"/api/admin/participants/{participant.Id}/restore", participant.ETag);

        var details = new JsonObject
        {
            ["old"] = new JsonObject { ["status"] = "Archived", ["archivedAtUtc"] = TestParticipants.ArchivedAtUtc },
            ["new"] = new JsonObject { ["status"] = "Active", ["archivedAtUtc"] = null },
        };
        return new(
            AuditEventTypes.Participant.Restored,
            a.OrganisationId,
            a.ManagerId,
            AuditEntityTypes.Participant,
            participant.Id,
            participant.Id,
            details.ToJsonString());
    }

    /// <summary>
    /// <c>goal.created</c>: A's manager adds a goal, typed over two lines, to a participant added for the row with
    /// <c>POST /api/admin/participants/{participantId}/goals</c>, which records the new goal as the entity and its
    /// participant as the participant, and the goal's values as <c>new</c> alone, named as design.md §6.6's list names
    /// them: its text as stored, on one line, its place at the end of a list that had no goal, and no time archived.
    /// </summary>
    private async Task<WrittenEvent> AddGoalAsync()
    {
        var participant = await factory.Services.AddParticipantAsync(a);
        using var client = factory.CreateHttpsClient().SignInAs(a.ManagerId);
        using var response = await client.PostWithTokenAsync(
            $"/api/admin/participants/{participant.Id}/goals",
            await client.FetchTokenAsync(),
            new { text = " Makes own\nbreakfast " });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var values = new JsonObject { ["text"] = "Makes own breakfast", ["sortOrder"] = 0, ["archivedAtUtc"] = null };
        return new(
            AuditEventTypes.Goal.Created,
            a.OrganisationId,
            a.ManagerId,
            AuditEntityTypes.Goal,
            await StoredGoalIdAsync(participant.Id),
            participant.Id,
            new JsonObject { ["new"] = values }.ToJsonString());
    }

    /// <summary>
    /// <c>goal.updated</c>: A's manager rewords a goal of a participant added for the row, typing it over two lines,
    /// with <c>PUT /api/admin/goals/{goalId}</c> and the goal's ETag, which records the goal as the entity and its
    /// participant as the participant, and the old and new text alone, the new as stored, on one line, not its place or
    /// time archived, which a reword leaves as they were.
    /// </summary>
    private async Task<WrittenEvent> RewordGoalAsync()
    {
        var participant = await factory.Services.AddParticipantAsync(a);
        var goal = await factory.Services.AddGoalAsync(a, participant.Id, new("Makes own breakfast", 3));
        using var client = factory.CreateHttpsClient().SignInAs(a.ManagerId);
        using var response = await client.PutWithTokenAsync(
            $"/api/admin/goals/{goal.Id}",
            await client.FetchTokenAsync(),
            new { text = " Makes own\nlunch " },
            goal.ETag);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var details = new JsonObject
        {
            ["old"] = new JsonObject { ["text"] = "Makes own breakfast" },
            ["new"] = new JsonObject { ["text"] = "Makes own lunch" },
        };
        return new(
            AuditEventTypes.Goal.Updated,
            a.OrganisationId,
            a.ManagerId,
            AuditEntityTypes.Goal,
            goal.Id,
            participant.Id,
            details.ToJsonString());
    }

    /// <summary>
    /// <c>goal.reordered</c>: A's manager moves the second of a participant's two active goals to the top with
    /// <c>PUT /api/admin/participants/{participantId}/goals/order</c>, which records no goal as the entity, as it is
    /// about the list, and the participant as the participant, and the active goals' IDs in their old and new order,
    /// the archived goal left out of both.
    /// </summary>
    private async Task<WrittenEvent> ReorderGoalsAsync()
    {
        var participant = await factory.Services.AddParticipantAsync(a);
        var goals = await factory.Services.AddGoalsAsync(
            a,
            participant.Id,
            new("Makes own breakfast", 0),
            new("Uses the washing machine", 1, ArchivedAtUtc: TestGoals.AddedAtUtc),
            new("Phones her sister", 2));
        var (breakfast, sister) = (goals[0], goals[2]);
        using var client = factory.CreateHttpsClient().SignInAs(a.ManagerId);
        using var response = await client.PutWithTokenAsync(
            $"/api/admin/participants/{participant.Id}/goals/order",
            await client.FetchTokenAsync(),
            new { goalIds = new[] { sister, breakfast } },
            ifMatch: null);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var details = new JsonObject
        {
            ["old"] = new JsonObject { ["order"] = new JsonArray(breakfast.ToString(), sister.ToString()) },
            ["new"] = new JsonObject { ["order"] = new JsonArray(sister.ToString(), breakfast.ToString()) },
        };
        return new(
            AuditEventTypes.Goal.Reordered,
            a.OrganisationId,
            a.ManagerId,
            AuditEntityTypes.Goal,
            null,
            participant.Id,
            details.ToJsonString());
    }

    /// <summary>
    /// <c>goal.archived</c>: A's manager archives an active goal of a participant added for the row with
    /// <c>POST /api/admin/goals/{goalId}/archive</c> and the goal's ETag, which records the goal as the entity and its
    /// participant as the participant, and the time it was archived at, before and after, the time as stored; not its
    /// <c>sortOrder</c>, which archiving leaves as it was.
    /// </summary>
    private async Task<WrittenEvent> ArchiveGoalAsync()
    {
        var participant = await factory.Services.AddParticipantAsync(a);
        var goal = await factory.Services.AddGoalAsync(a, participant.Id, new("Makes own breakfast", 3));
        await PostAsync($"/api/admin/goals/{goal.Id}/archive", goal.ETag);

        var details = new JsonObject
        {
            ["old"] = new JsonObject { ["archivedAtUtc"] = null },
            ["new"] = new JsonObject { ["archivedAtUtc"] = await StoredGoalArchivedAtUtcAsync(goal.Id) },
        };
        return new(
            AuditEventTypes.Goal.Archived,
            a.OrganisationId,
            a.ManagerId,
            AuditEntityTypes.Goal,
            goal.Id,
            participant.Id,
            details.ToJsonString());
    }

    /// <summary>
    /// <c>goal.restored</c>: A's manager restores an archived goal of a participant added for the row with
    /// <c>POST /api/admin/goals/{goalId}/restore</c> and the goal's ETag, which records the goal as the entity and its
    /// participant as the participant, and its place before and after: from where it was archived to one after the
    /// highest active <c>sortOrder</c>, and from the time it was archived at to none.
    /// </summary>
    private async Task<WrittenEvent> RestoreGoalAsync()
    {
        var participant = await factory.Services.AddParticipantAsync(a);
        await factory.Services.AddGoalAsync(a, participant.Id, new("Phones her sister", 4));
        var goal = await factory.Services.AddGoalAsync(
            a, participant.Id, new("Makes own breakfast", 1, ArchivedAtUtc: TestGoals.AddedAtUtc));
        await PostAsync($"/api/admin/goals/{goal.Id}/restore", goal.ETag);

        var details = new JsonObject
        {
            ["old"] = new JsonObject { ["sortOrder"] = 1, ["archivedAtUtc"] = TestGoals.AddedAtUtc },
            ["new"] = new JsonObject { ["sortOrder"] = 5, ["archivedAtUtc"] = null },
        };
        return new(
            AuditEventTypes.Goal.Restored,
            a.OrganisationId,
            a.ManagerId,
            AuditEntityTypes.Goal,
            goal.Id,
            participant.Id,
            details.ToJsonString());
    }

    /// <summary>
    /// <c>common_item.created</c>: A's manager adds an item, typed over two lines, to a group added for the row with
    /// <c>POST /api/admin/common-item-groups/{groupId}/items</c>, which records the new item as the entity, with no
    /// participant, and its group and values as <c>new</c> alone, named as design.md §6.6's list names them: its text
    /// as stored, on one line, its place at the end of a group that had no item, and no time archived.
    /// </summary>
    private async Task<WrittenEvent> AddCommonItemAsync()
    {
        var groupId = Assert.Single(
            await factory.Services.AddGroupsAsync(a.OrganisationId, new TestCommonItemGroup("Community outing", 0)));
        using var client = factory.CreateHttpsClient().SignInAs(a.ManagerId);
        using var response = await client.PostWithTokenAsync(
            $"/api/admin/common-item-groups/{groupId}/items",
            await client.FetchTokenAsync(),
            new { text = " Travelled by\nbus or train " });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var values = new JsonObject
        {
            ["groupId"] = groupId,
            ["text"] = "Travelled by bus or train",
            ["sortOrder"] = 0,
            ["archivedAtUtc"] = null,
        };
        return new(
            AuditEventTypes.CommonItem.Created,
            a.OrganisationId,
            a.ManagerId,
            AuditEntityTypes.CommonItem,
            await StoredItemIdAsync(groupId),
            null,
            new JsonObject { ["new"] = values }.ToJsonString());
    }

    /// <summary>
    /// <c>common_item.updated</c>, by Reword: A's manager rewords an item of a group added for the row, typing the new
    /// text over two lines, with <c>PUT /api/admin/common-items/{id}</c> and the item's <c>ETag</c>, sending the item's
    /// own group as the screen does, which records the item as the entity, with no participant, and the old and new
    /// text, as stored, alone: the reword keeps the item in its place in its group, so no other value changes.
    /// </summary>
    private async Task<WrittenEvent> RewordCommonItemAsync()
    {
        var groupId = Assert.Single(
            await factory.Services.AddGroupsAsync(a.OrganisationId, new TestCommonItemGroup("Community outing", 0)));
        var item = await factory.Services.AddItemAsync(
            a.OrganisationId, groupId, new TestCommonItem("Travelled by bus or train", 3));
        using var client = factory.CreateHttpsClient().SignInAs(a.ManagerId);
        using var response = await client.PutWithTokenAsync(
            $"/api/admin/common-items/{item.Id}",
            await client.FetchTokenAsync(),
            new { text = " Travelled by\ntram ", groupId },
            item.ETag);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var details = new JsonObject
        {
            ["old"] = new JsonObject { ["text"] = "Travelled by bus or train" },
            ["new"] = new JsonObject { ["text"] = "Travelled by tram" },
        };
        return new(
            AuditEventTypes.CommonItem.Updated,
            a.OrganisationId,
            a.ManagerId,
            AuditEntityTypes.CommonItem,
            item.Id,
            null,
            details.ToJsonString());
    }

    /// <summary>
    /// <c>common_item.updated</c>, by Move: A's manager moves an item of a group added for the row to another group
    /// added for it, which holds an active item and an archived one after it, with
    /// <c>PUT /api/admin/common-items/{id}</c> and the item's <c>ETag</c>, sending the text as it was, which records
    /// the item as the entity, with no participant, and its old and new group and place alone: the item goes after the
    /// chosen group's highest active item (A42), whatever its archived one holds, and its text is unchanged
    /// (S01.03.08's Note).
    /// </summary>
    private async Task<WrittenEvent> MoveCommonItemAsync()
    {
        var groups = await factory.Services.AddGroupsAsync(
            a.OrganisationId, new("Community outing", 0), new("Personal care", 1));
        var (groupId, chosenId) = (groups[0], groups[1]);
        var item = await factory.Services.AddItemAsync(
            a.OrganisationId, groupId, new TestCommonItem("Travelled by bus or train", 3));
        await factory.Services.AddItemsAsync(
            a.OrganisationId,
            chosenId,
            new("Showered", 1),
            new("Packed an overnight bag", 5, ArchivedAtUtc: TestCommonItems.AddedAtUtc));
        using var client = factory.CreateHttpsClient().SignInAs(a.ManagerId);
        using var response = await client.PutWithTokenAsync(
            $"/api/admin/common-items/{item.Id}",
            await client.FetchTokenAsync(),
            new { text = "Travelled by bus or train", groupId = chosenId },
            item.ETag);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var details = new JsonObject
        {
            ["old"] = new JsonObject { ["groupId"] = groupId, ["sortOrder"] = 3 },
            ["new"] = new JsonObject { ["groupId"] = chosenId, ["sortOrder"] = 2 },
        };
        return new(
            AuditEventTypes.CommonItem.Updated,
            a.OrganisationId,
            a.ManagerId,
            AuditEntityTypes.CommonItem,
            item.Id,
            null,
            details.ToJsonString());
    }

    /// <summary>
    /// <c>common_item.reordered</c>: A's manager moves the second of the two active items of a group added for the row
    /// to the top with <c>PUT /api/admin/common-item-groups/{groupId}/items/order</c>, which records no item as the
    /// entity, as it is about the list, and no participant, and the active items' IDs in their old and new order, the
    /// archived item left out of both, with the group's ID beside them (S01.03.03's Note).
    /// </summary>
    private async Task<WrittenEvent> ReorderCommonItemsAsync()
    {
        var groupId = Assert.Single(
            await factory.Services.AddGroupsAsync(a.OrganisationId, new TestCommonItemGroup("Community outing", 0)));
        var items = await factory.Services.AddItemsAsync(
            a.OrganisationId,
            groupId,
            new("Travelled by bus or train", 0),
            new("Packed a day bag", 1, ArchivedAtUtc: TestCommonItems.AddedAtUtc),
            new("Bought lunch", 2));
        var (bus, lunch) = (items[0], items[2]);
        using var client = factory.CreateHttpsClient().SignInAs(a.ManagerId);
        using var response = await client.PutWithTokenAsync(
            $"/api/admin/common-item-groups/{groupId}/items/order",
            await client.FetchTokenAsync(),
            new { commonItemIds = new[] { lunch, bus } },
            ifMatch: null);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var details = new JsonObject
        {
            ["old"] = new JsonObject { ["order"] = new JsonArray(bus.ToString(), lunch.ToString()) },
            ["new"] = new JsonObject { ["order"] = new JsonArray(lunch.ToString(), bus.ToString()) },
            ["groupId"] = groupId,
        };
        return new(
            AuditEventTypes.CommonItem.Reordered,
            a.OrganisationId,
            a.ManagerId,
            AuditEntityTypes.CommonItem,
            null,
            null,
            details.ToJsonString());
    }

    /// <summary>
    /// <c>common_item.archived</c>: A's manager archives an active item of a group added for the row with
    /// <c>POST /api/admin/common-items/{id}/archive</c> and the item's <c>ETag</c>, which records the item as the
    /// entity, with no participant, and the time it was archived at, before and after, the time as stored; not its
    /// <c>sortOrder</c>, which archiving leaves as it was.
    /// </summary>
    private async Task<WrittenEvent> ArchiveCommonItemAsync()
    {
        var groupId = Assert.Single(
            await factory.Services.AddGroupsAsync(a.OrganisationId, new TestCommonItemGroup("Community outing", 0)));
        var item = await factory.Services.AddItemAsync(
            a.OrganisationId, groupId, new TestCommonItem("Travelled by bus or train", 3));
        await PostAsync($"/api/admin/common-items/{item.Id}/archive", item.ETag);

        var details = new JsonObject
        {
            ["old"] = new JsonObject { ["archivedAtUtc"] = null },
            ["new"] = new JsonObject { ["archivedAtUtc"] = await StoredItemArchivedAtUtcAsync(item.Id) },
        };
        return new(
            AuditEventTypes.CommonItem.Archived,
            a.OrganisationId,
            a.ManagerId,
            AuditEntityTypes.CommonItem,
            item.Id,
            null,
            details.ToJsonString());
    }

    /// <summary>
    /// <c>common_item.restored</c>: A's manager restores an archived item of a group added for the row with
    /// <c>POST /api/admin/common-items/{id}/restore</c> and the item's <c>ETag</c>, which records the item as the
    /// entity, with no participant, and its place before and after: from where it was archived to one after the
    /// highest active <c>sortOrder</c> of its group, and from the time it was archived at to none.
    /// </summary>
    private async Task<WrittenEvent> RestoreCommonItemAsync()
    {
        var groupId = Assert.Single(
            await factory.Services.AddGroupsAsync(a.OrganisationId, new TestCommonItemGroup("Community outing", 0)));
        await factory.Services.AddItemAsync(a.OrganisationId, groupId, new TestCommonItem("Bought lunch", 4));
        var item = await factory.Services.AddItemAsync(
            a.OrganisationId,
            groupId,
            new TestCommonItem("Travelled by bus or train", 1, ArchivedAtUtc: TestCommonItems.AddedAtUtc));
        await PostAsync($"/api/admin/common-items/{item.Id}/restore", item.ETag);

        var details = new JsonObject
        {
            ["old"] = new JsonObject { ["sortOrder"] = 1, ["archivedAtUtc"] = TestCommonItems.AddedAtUtc },
            ["new"] = new JsonObject { ["sortOrder"] = 5, ["archivedAtUtc"] = null },
        };
        return new(
            AuditEventTypes.CommonItem.Restored,
            a.OrganisationId,
            a.ManagerId,
            AuditEntityTypes.CommonItem,
            item.Id,
            null,
            details.ToJsonString());
    }

    /// <summary>
    /// <c>common_item_group.created</c>: the manager of an organisation that the row adds, whose groups are Every note
    /// alone, so that the new group's place is known, adds a group, typed over two lines, with
    /// <c>POST /api/admin/common-item-groups</c>, which records the new group as the entity, with no participant, and
    /// its values as <c>new</c> alone, named as design.md §6.6's list names them: its name as stored, on one line, its
    /// place, the first after Every note, and no time archived.
    /// </summary>
    private async Task<WrittenEvent> AddCommonItemGroupAsync()
    {
        var (organisationId, managerId) = await AddOrganisationAsync();
        await AddEveryNoteAsync(organisationId);
        using var client = factory.CreateHttpsClient().SignInAs(managerId);
        using var response = await client.PostWithTokenAsync(
            "/api/admin/common-item-groups", await client.FetchTokenAsync(), new { name = " Community\nouting " });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var added = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        var values = new JsonObject { ["name"] = "Community outing", ["sortOrder"] = 0, ["archivedAtUtc"] = null };
        return new(
            AuditEventTypes.CommonItemGroup.Created,
            organisationId,
            managerId,
            AuditEntityTypes.CommonItemGroup,
            added.GetProperty("id").GetGuid(),
            null,
            new JsonObject { ["new"] = values }.ToJsonString());
    }

    /// <summary>
    /// <c>common_item_group.updated</c>: A's manager renames a group added for the row, typing the new name over two
    /// lines, with <c>PUT /api/admin/common-item-groups/{groupId}</c> and the group's <c>ETag</c>, which records the
    /// group as the entity, with no participant, and the old and new name, as stored, alone: a rename changes no other
    /// value.
    /// </summary>
    private async Task<WrittenEvent> RenameCommonItemGroupAsync()
    {
        var group = await factory.Services.AddGroupAsync(
            a.OrganisationId, new TestCommonItemGroup("Community outing", 3));
        using var client = factory.CreateHttpsClient().SignInAs(a.ManagerId);
        using var response = await client.PutWithTokenAsync(
            $"/api/admin/common-item-groups/{group.Id}",
            await client.FetchTokenAsync(),
            new { name = " Outings and\ntrips " },
            group.ETag);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var details = new JsonObject
        {
            ["old"] = new JsonObject { ["name"] = "Community outing" },
            ["new"] = new JsonObject { ["name"] = "Outings and trips" },
        };
        return new(
            AuditEventTypes.CommonItemGroup.Updated,
            a.OrganisationId,
            a.ManagerId,
            AuditEntityTypes.CommonItemGroup,
            group.Id,
            null,
            details.ToJsonString());
    }

    /// <summary>
    /// <c>common_item_group.reordered</c>: the manager of an organisation that the row adds, so that its groups are
    /// known, whose groups are Every note and two active groups with an archived one between them, moves the second
    /// active group to the top with <c>PUT /api/admin/common-item-groups/order</c>, which records no group as the
    /// entity, as it is about the list, and no participant, and the active groups' IDs in their old and new order,
    /// Every note and the archived group left out of both.
    /// </summary>
    private async Task<WrittenEvent> ReorderCommonItemGroupsAsync()
    {
        var (organisationId, managerId) = await AddOrganisationAsync();
        await AddEveryNoteAsync(organisationId);
        var groups = await factory.Services.AddGroupsAsync(
            organisationId,
            new TestCommonItemGroup("Community outing", 0),
            new TestCommonItemGroup("Overnight respite", 1, ArchivedAtUtc: TestCommonItems.AddedAtUtc),
            new TestCommonItemGroup("Personal care", 2));
        var (outing, care) = (groups[0], groups[2]);
        using var client = factory.CreateHttpsClient().SignInAs(managerId);
        using var response = await client.PutWithTokenAsync(
            "/api/admin/common-item-groups/order",
            await client.FetchTokenAsync(),
            new { groupIds = new[] { care, outing } },
            ifMatch: null);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var details = new JsonObject
        {
            ["old"] = new JsonObject { ["order"] = new JsonArray(outing.ToString(), care.ToString()) },
            ["new"] = new JsonObject { ["order"] = new JsonArray(care.ToString(), outing.ToString()) },
        };
        return new(
            AuditEventTypes.CommonItemGroup.Reordered,
            organisationId,
            managerId,
            AuditEntityTypes.CommonItemGroup,
            null,
            null,
            details.ToJsonString());
    }

    /// <summary>
    /// <c>common_item_group.archived</c>: A's manager archives an active group added for the row with
    /// <c>POST /api/admin/common-item-groups/{groupId}/archive</c> and the group's <c>ETag</c>, which records the group
    /// as the entity, with no participant, and the time it was archived at, before and after, the time as stored; not
    /// its <c>sortOrder</c>, which archiving leaves as it was.
    /// </summary>
    private async Task<WrittenEvent> ArchiveCommonItemGroupAsync()
    {
        var group = await factory.Services.AddGroupAsync(
            a.OrganisationId, new TestCommonItemGroup("Community outing", 3));
        await PostAsync($"/api/admin/common-item-groups/{group.Id}/archive", group.ETag);

        var details = new JsonObject
        {
            ["old"] = new JsonObject { ["archivedAtUtc"] = null },
            ["new"] = new JsonObject { ["archivedAtUtc"] = await StoredGroupArchivedAtUtcAsync(group.Id) },
        };
        return new(
            AuditEventTypes.CommonItemGroup.Archived,
            a.OrganisationId,
            a.ManagerId,
            AuditEntityTypes.CommonItemGroup,
            group.Id,
            null,
            details.ToJsonString());
    }

    /// <summary>
    /// <c>common_item_group.restored</c>: the manager of an organisation that the row adds, so that its groups are
    /// known, whose groups are Every note, an active group and an archived one, restores the archived one with
    /// <c>POST /api/admin/common-item-groups/{groupId}/restore</c> and its <c>ETag</c>, which records the group as the
    /// entity, with no participant, and its place before and after: from where it was archived to one after the
    /// highest active <c>sortOrder</c> of the groups other than Every note, and from the time it was archived at to
    /// none.
    /// </summary>
    private async Task<WrittenEvent> RestoreCommonItemGroupAsync()
    {
        var (organisationId, managerId) = await AddOrganisationAsync();
        await AddEveryNoteAsync(organisationId);
        await factory.Services.AddGroupsAsync(organisationId, new TestCommonItemGroup("Personal care", 2));
        var group = await factory.Services.AddGroupAsync(
            organisationId, new TestCommonItemGroup("Community outing", 0, ArchivedAtUtc: TestCommonItems.AddedAtUtc));
        using var client = factory.CreateHttpsClient().SignInAs(managerId);
        using var response = await client.PostWithTokenAsync(
            $"/api/admin/common-item-groups/{group.Id}/restore", await client.FetchTokenAsync(), ifMatch: group.ETag);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var details = new JsonObject
        {
            ["old"] = new JsonObject { ["sortOrder"] = 0, ["archivedAtUtc"] = TestCommonItems.AddedAtUtc },
            ["new"] = new JsonObject { ["sortOrder"] = 3, ["archivedAtUtc"] = null },
        };
        return new(
            AuditEventTypes.CommonItemGroup.Restored,
            organisationId,
            managerId,
            AuditEntityTypes.CommonItemGroup,
            group.Id,
            null,
            details.ToJsonString());
    }

    /// <summary>
    /// <c>guide_prompts.updated</c>: the manager of an organisation that the row adds, so that no seeded row changes,
    /// loads its guide prompts with <c>GET /api/admin/guide-prompts</c> and saves new ones with <c>PUT</c> and the
    /// <c>ETag</c> loaded, which records the organisation as the entity, with no participant, and the text before and
    /// after.
    /// </summary>
    private async Task<WrittenEvent> SaveGuidePromptsAsync()
    {
        const string path = "/api/admin/guide-prompts";
        const string oldText = "What did they enjoy today?";
        const string newText = "What did they enjoy today?\nWho did they see?";
        var (organisationId, managerId) = await AddOrganisationAsync(oldText);
        using var client = factory.CreateHttpsClient().SignInAs(managerId);
        string etag;
        using (var loaded = await client.GetAsync(path, TestContext.Current.CancellationToken))
        {
            Assert.Equal(HttpStatusCode.OK, loaded.StatusCode);
            Assert.NotNull(loaded.Headers.ETag);
            etag = loaded.Headers.ETag.ToString();
        }

        using var saved = await client.PutWithTokenAsync(
            path, await client.FetchTokenAsync(), new { text = newText }, etag);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);

        var details = new JsonObject
        {
            ["old"] = new JsonObject { ["text"] = oldText },
            ["new"] = new JsonObject { ["text"] = newText },
        };
        return new(
            AuditEventTypes.GuidePrompts.Updated,
            organisationId,
            managerId,
            AuditEntityTypes.Organisation,
            organisationId,
            null,
            details.ToJsonString());
    }

    /// <summary>
    /// <c>note.draft_started</c>: A's worker makes the first change to today's note of a participant added for the row,
    /// a goal ticked and a sentence written, with <c>PUT {base}/draft</c> and the template's lists' version, which
    /// records the new note as the entity and its participant as the participant, and no <c>Details</c>, so no note
    /// text or goal wording (design.md §5.3, §9.5).
    /// </summary>
    private async Task<WrittenEvent> StartDraftAsync()
    {
        var participant = await factory.Services.AddParticipantAsync(a);
        var goal = await factory.Services.AddGoalAsync(a, participant.Id, new("Makes own breakfast", 0));
        var today = factory.Services.GetRequiredService<MelbourneClock>().Today();
        using var client = factory.CreateHttpsClient().SignInAs(a.WorkerId);
        using var response = await client.PutWithTokenAsync(
            string.Create(
                CultureInfo.InvariantCulture, $"/api/participants/{participant.Id}/notes/{today:yyyy-MM-dd}/draft"),
            await client.FetchTokenAsync(),
            new
            {
                clientId = Guid.NewGuid(),
                seq = 1,
                listsVersion = await ListsVersionOfTodaysFormAsync(client, participant.Id, today),
                narrative = "Jane made toast.",
                tickedGoalIds = new[] { goal.Id },
                pickedGroupIds = Array.Empty<Guid>(),
                tickedCommonItemIds = Array.Empty<Guid>(),
            },
            ifMatch: null);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return new(
            AuditEventTypes.Note.DraftStarted,
            a.OrganisationId,
            a.WorkerId,
            AuditEntityTypes.Note,
            await StoredNoteIdAsync(participant.Id),
            participant.Id,
            null);
    }

    /// <summary>
    /// <c>note.discarded</c>: A's manager discards a draft of today's note of a participant added for the row, which
    /// the row adds for A's worker as a first save leaves it, with text written, with <c>POST {base}/discard</c>, which
    /// records the manager, not the draft's author, as the actor, the note as the entity and its participant as the
    /// participant, and no <c>Details</c>, so no note text, as the note row holds who discarded it and when (design.md
    /// §5.3, §9.5).
    /// </summary>
    private async Task<WrittenEvent> DiscardDraftAsync()
    {
        var participant = await factory.Services.AddParticipantAsync(a);
        var today = factory.Services.GetRequiredService<MelbourneClock>().Today();
        var noteId = await factory.Services.AddDraftAsync(a, new(
            participant.Id,
            today,
            a.WorkerId,
            new DateTime(2026, 10, 18, 0, 0, 0, DateTimeKind.Utc),
            Narrative: "Jane made toast."));
        await PostAsync(
            string.Create(
                CultureInfo.InvariantCulture, $"/api/participants/{participant.Id}/notes/{today:yyyy-MM-dd}/discard"),
            ifMatch: null);

        return new(
            AuditEventTypes.Note.Discarded,
            a.OrganisationId,
            a.ManagerId,
            AuditEntityTypes.Note,
            noteId,
            participant.Id,
            null);
    }

    /// <summary>
    /// <c>note.submitted</c>: A's worker submits their draft of today's note of a participant added for the row, which
    /// the row adds as a first save leaves it, with a goal ticked and flagged with a reason, with
    /// <c>POST {base}/versions</c>, which records the note as the entity and its participant as the participant, and
    /// the version number alone in <c>Details</c>, so no note text, reason or goal wording (design.md §5.3, §9.5).
    /// </summary>
    private async Task<WrittenEvent> SubmitNoteAsync()
    {
        var participant = await factory.Services.AddParticipantAsync(a);
        var goal = await factory.Services.AddGoalAsync(a, participant.Id, new("Makes own breakfast", 0));
        var today = factory.Services.GetRequiredService<MelbourneClock>().Today();
        var noteId = await factory.Services.AddDraftAsync(a, new(
            participant.Id,
            today,
            a.WorkerId,
            new DateTime(2026, 10, 18, 0, 0, 0, DateTimeKind.Utc),
            [new(NoteSection.Goal, goal.Id, null, "Makes own breakfast", false)]));
        using var client = factory.CreateHttpsClient().SignInAs(a.WorkerId);
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            string.Create(
                CultureInfo.InvariantCulture, $"/api/participants/{participant.Id}/notes/{today:yyyy-MM-dd}/versions"))
        {
            Content = JsonContent.Create(new
            {
                baseVersion = 0,
                narrative = "Jane made toast.",
                tickedGoalIds = new[] { goal.Id },
                pickedGroupIds = Array.Empty<Guid>(),
                tickedCommonItemIds = Array.Empty<Guid>(),
                isFlagged = true,
                flagReason = "Jane seemed unwell.",
            }),
        };
        request.Headers.Add(AntiforgeryTokens.Header, await client.FetchTokenAsync());
        request.Headers.Add(NoteVersionEndpoints.IdempotencyKeyHeader, Guid.NewGuid().ToString());
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return new(
            AuditEventTypes.Note.Submitted,
            a.OrganisationId,
            a.WorkerId,
            AuditEntityTypes.Note,
            noteId,
            participant.Id,
            new JsonObject { ["versionNumber"] = 1 }.ToJsonString());
    }

    /// <summary>
    /// <c>note.edited</c>: A's manager saves their changes to today's note of a participant added for the row, which
    /// the row adds as Submit leaves it, with version 1 written by A's worker, with <c>POST {base}/versions</c> at base
    /// 1 after their first save of the edit, which writes no event of its own, which records the note as the entity and
    /// its participant as the participant, and the new version's number alone in <c>Details</c>, so no note text
    /// (design.md §5.3, §9.5).
    /// </summary>
    private async Task<WrittenEvent> EditNoteAsync()
    {
        var participant = await factory.Services.AddParticipantAsync(a);
        var today = factory.Services.GetRequiredService<MelbourneClock>().Today();
        var noteId = await factory.Services.AddSubmittedAsync(a, new(
            participant.Id,
            today,
            a.WorkerId,
            new DateTime(2026, 10, 18, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 10, 18, 6, 0, 0, DateTimeKind.Utc)));
        using var client = factory.CreateHttpsClient().SignInAs(a.ManagerId);
        var path = string.Create(
            CultureInfo.InvariantCulture, $"/api/participants/{participant.Id}/notes/{today:yyyy-MM-dd}");
        using (var edit = await client.PutWithTokenAsync(
                   $"{path}/draft",
                   await client.FetchTokenAsync(),
                   new
                   {
                       baseVersion = 1,
                       clientId = Guid.NewGuid(),
                       seq = 1,
                       narrative = "Jane made toast and tea.",
                       tickedGoalIds = Array.Empty<Guid>(),
                       pickedGroupIds = Array.Empty<Guid>(),
                       tickedCommonItemIds = Array.Empty<Guid>(),
                       isFlagged = false,
                       flagReason = (string?)null,
                   },
                   ifMatch: null))
        {
            Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{path}/versions")
        {
            Content = JsonContent.Create(new
            {
                baseVersion = 1,
                narrative = "Jane made toast and tea.",
                tickedGoalIds = Array.Empty<Guid>(),
                pickedGroupIds = Array.Empty<Guid>(),
                tickedCommonItemIds = Array.Empty<Guid>(),
                isFlagged = false,
                flagReason = (string?)null,
            }),
        };
        request.Headers.Add(AntiforgeryTokens.Header, await client.FetchTokenAsync());
        request.Headers.Add(NoteVersionEndpoints.IdempotencyKeyHeader, Guid.NewGuid().ToString());
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return new(
            AuditEventTypes.Note.Edited,
            a.OrganisationId,
            a.ManagerId,
            AuditEntityTypes.Note,
            noteId,
            participant.Id,
            new JsonObject { ["versionNumber"] = 2 }.ToJsonString());
    }

    /// <summary>
    /// <c>note.reviewed</c>: A's manager marks reviewed today's note of a participant added for the row, which the row
    /// adds as Submit leaves it, with version 1 written by A's worker and flagged with a reason, with
    /// <c>POST {base}/reviews</c>, which records the manager as the actor, the note as the entity and its participant
    /// as the participant, and the version number alone in <c>Details</c>, so no note text and no comment (design.md
    /// §5.3, §9.5, A28).
    /// </summary>
    private async Task<WrittenEvent> ReviewNoteAsync()
    {
        var participant = await factory.Services.AddParticipantAsync(a);
        var today = factory.Services.GetRequiredService<MelbourneClock>().Today();
        var noteId = await factory.Services.AddSubmittedAsync(a, new(
            participant.Id,
            today,
            a.WorkerId,
            new DateTime(2026, 10, 18, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 10, 18, 6, 0, 0, DateTimeKind.Utc),
            IsFlagged: true,
            FlagReason: "Jane seemed unwell."));
        using var client = factory.CreateHttpsClient().SignInAs(a.ManagerId);
        using var response = await client.PostWithTokenAsync(
            string.Create(
                CultureInfo.InvariantCulture, $"/api/participants/{participant.Id}/notes/{today:yyyy-MM-dd}/reviews"),
            await client.FetchTokenAsync(),
            new { versionNumber = 1, comment = "Called Sam's mum." });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return new(
            AuditEventTypes.Note.Reviewed,
            a.OrganisationId,
            a.ManagerId,
            AuditEntityTypes.Note,
            noteId,
            participant.Id,
            new JsonObject { ["versionNumber"] = 1 }.ToJsonString());
    }

    /// <summary>
    /// <c>report.downloaded</c>: A's manager downloads the daily report of <see cref="ReportDate"/>, a day on which a
    /// participant added for the row has a submitted note, with <c>GET /api/reports/daily/{date}/export</c> as a PDF,
    /// which records the manager as the actor and the report as the entity, with no entity ID and no participant,
    /// and the date and format alone in <c>Details</c>.
    /// </summary>
    private async Task<WrittenEvent> DownloadDailyReportAsync()
    {
        await AddParticipantWithANoteOnReportDateAsync();
        using var client = factory.CreateHttpsClient().SignInAs(a.ManagerId);

        using var response = await client.GetAsync(
            $"/api/reports/daily/{ReportDay}/export?format=pdf", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return new(
            AuditEventTypes.Report.Downloaded,
            a.OrganisationId,
            a.ManagerId,
            AuditEntityTypes.Report,
            null,
            null,
            new JsonObject { ["date"] = ReportDay, ["format"] = "pdf" }.ToJsonString());
    }

    /// <summary>
    /// <c>participant.exported</c>: A's manager exports the record of a participant added for the row, who has a
    /// submitted note on <see cref="ReportDate"/>, for that day, as Word without earlier versions, with
    /// <c>GET /api/reports/participants/{id}/export</c>, which records the manager as the actor and the participant
    /// as the entity and the participant, and the range, format and whether earlier versions are included alone in
    /// <c>Details</c>.
    /// </summary>
    private async Task<WrittenEvent> ExportParticipantAsync()
    {
        var participant = await AddParticipantWithANoteOnReportDateAsync();
        using var client = factory.CreateHttpsClient().SignInAs(a.ManagerId);

        using var response = await client.GetAsync(
            $"/api/reports/participants/{participant.Id}/export?from={ReportDay}&to={ReportDay}" +
            "&format=docx&includeHistory=false",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var details = new JsonObject
        {
            ["from"] = ReportDay,
            ["to"] = ReportDay,
            ["format"] = "docx",
            ["includeHistory"] = false,
        };
        return new(
            AuditEventTypes.Participant.Exported,
            a.OrganisationId,
            a.ManagerId,
            AuditEntityTypes.Participant,
            participant.Id,
            participant.Id,
            details.ToJsonString());
    }

    // A participant added to A with a note that A's worker submitted on the report date, as a report asks for one: both
    // endpoints answer 404 report.no_notes for a day with none.
    private async Task<TestParticipant> AddParticipantWithANoteOnReportDateAsync()
    {
        var participant = await factory.Services.AddParticipantAsync(a);
        await factory.Services.AddSubmittedAsync(a, new(
            participant.Id,
            ReportDate,
            a.WorkerId,
            new DateTime(2026, 10, 1, 0, 14, 0, DateTimeKind.Utc),
            new DateTime(2026, 10, 1, 6, 42, 0, DateTimeKind.Utc)));
        return participant;
    }

    /// <summary>
    /// <c>admin.bootstrap</c>: the operator runs <c>admin bootstrap</c>, on the collection's database, which adds an
    /// organisation of the row's own and records it as the entity, with no actor, as the operator is no user, no
    /// participant, and its first manager's ID alone in <c>Details</c>, so no name or address (A28).
    /// </summary>
    private async Task<WrittenEvent> RunBootstrapAsync()
    {
        var email = $"quinn.harper.{Guid.NewGuid():N}@example.org";

        var run = await factory.RunAdminCommandAsync([
            "bootstrap",
            "--organisation", "Audit events test",
            "--manager-name", "Quinn Harper",
            "--manager-email", email,
        ]);

        Assert.Equal((0, ""), (run.ExitCode, run.Error));
        var manager = await StoredUserAsync(email);
        return new(
            AuditEventTypes.Admin.Bootstrap,
            manager.OrganisationId,
            null,
            AuditEntityTypes.Organisation,
            manager.OrganisationId,
            null,
            new JsonObject { ["userId"] = manager.Id }.ToJsonString());
    }

    /// <summary>
    /// <c>admin.signin_reset</c>: the operator runs <c>admin reset-signin</c> with the address of an Active worker
    /// added for the row, which records the worker as the entity, with no actor, as the operator is no user, no
    /// participant and no <c>Details</c>, so no address (A28).
    /// </summary>
    private async Task<WrittenEvent> RunResetSignInAsync()
    {
        var worker = await AddWorkerAsync(UserStatus.Active);

        var run = await factory.RunAdminCommandAsync(["reset-signin", worker.Email!]);

        Assert.Equal((0, ""), (run.ExitCode, run.Error));
        return new(
            AuditEventTypes.Admin.SignInReset,
            a.OrganisationId,
            null,
            AuditEntityTypes.User,
            worker.Id,
            null,
            null);
    }

    /// <summary>
    /// <c>admin.signout_all</c>: the operator runs <c>admin signout-all</c>, on a database of the row's own, where an
    /// organisation that the row adds is the only one, which records that organisation as the entity, with no actor,
    /// no participant and no <c>Details</c>. It writes one entry for each organisation and gives every user a new
    /// security stamp, the seeded users' too, so it cannot run on the collection's database (tests/README.md).
    /// </summary>
    private async Task<WrittenEvent> RunSignOutAllAsync()
    {
        var (organisationId, _) = await AddOrganisationAsync();

        var run = await App.RunAdminCommandAsync(["signout-all"]);

        Assert.Equal((0, "", ""), (run.ExitCode, run.Output, run.Error));
        return new(
            AuditEventTypes.Admin.SignOutAll,
            organisationId,
            null,
            AuditEntityTypes.Organisation,
            organisationId,
            null,
            null);
    }

    /// <summary>
    /// <c>admin.mcp_client_added</c>: the operator runs <c>admin mcp-client add</c> for a client ID that is not yet
    /// approved, on a database of the row's own, where an organisation that the row adds is the only one. It records
    /// no actor, no entity and no participant, and only the client ID and the name in <c>Details</c>, never a redirect
    /// URI, and writes one entry for each organisation, because an approved assistant is for all of them.
    /// </summary>
    private async Task<WrittenEvent> RunMcpClientAddAsync()
    {
        var (organisationId, _) = await AddOrganisationAsync();

        var run = await App.RunAdminCommandAsync(McpClientAdd);

        Assert.Equal((0, ""), (run.ExitCode, run.Error));
        return McpClientEvent(AuditEventTypes.Admin.McpClientAdded, organisationId);
    }

    /// <summary>
    /// <c>admin.mcp_client_updated</c>: the operator runs <c>admin mcp-client add</c> again for a client ID that is
    /// approved already, which the row registers through OpenIddict's manager, so that the row's one entry is the
    /// update's; it records what <c>admin.mcp_client_added</c> does.
    /// </summary>
    private async Task<WrittenEvent> RunMcpClientAddAgainAsync()
    {
        var (organisationId, _) = await AddOrganisationAsync();
        await AddApprovedAssistantAsync();

        var run = await App.RunAdminCommandAsync(McpClientAdd);

        Assert.Equal((0, ""), (run.ExitCode, run.Error));
        return McpClientEvent(AuditEventTypes.Admin.McpClientUpdated, organisationId);
    }

    /// <summary>
    /// <c>admin.mcp_client_removed</c>: the operator runs <c>admin mcp-client remove</c> for a client ID that is
    /// approved, which the row registers through OpenIddict's manager; it records what
    /// <c>admin.mcp_client_added</c> does, with the name that the registration holds.
    /// </summary>
    private async Task<WrittenEvent> RunMcpClientRemoveAsync()
    {
        var (organisationId, _) = await AddOrganisationAsync();
        await AddApprovedAssistantAsync();

        var run = await App.RunAdminCommandAsync(["mcp-client", "remove", McpClientId]);

        Assert.Equal((0, ""), (run.ExitCode, run.Error));
        return McpClientEvent(AuditEventTypes.Admin.McpClientRemoved, organisationId);
    }

    /// <summary>
    /// <c>mcp.authorised</c>: A's manager allows an assistant, which <c>ConsentDecisions.TakeAsync</c> records, as
    /// the authorise endpoint does just before it issues the code. It records the manager as the actor, with no entity
    /// and no participant, and the client ID and the scope granted alone in <c>Details</c>. The access is off on the
    /// collection's app, so the row calls the handler, which is registered either way, and the sequence is covered
    /// through the endpoints in <c>ConnectTests</c>.
    /// </summary>
    private async Task<WrittenEvent> AllowAssistantAsync()
    {
        var details = new JsonObject { ["client"] = McpClientId, ["scope"] = "grow2notes.admin offline_access" };

        return await TakeConsentAsync(allow: true, AuditEventTypes.Mcp.Authorised, details);
    }

    /// <summary>
    /// <c>mcp.authorisation_refused</c>: A's manager cancels, which records them as the actor, with no entity and no
    /// participant, and the client ID and why, <c>cancelled</c>, alone in <c>Details</c>.
    /// </summary>
    private async Task<WrittenEvent> CancelAssistantAsync()
    {
        var details = new JsonObject { ["client"] = McpClientId, ["why"] = "cancelled" };

        return await TakeConsentAsync(allow: false, AuditEventTypes.Mcp.AuthorisationRefused, details);
    }

    private async Task<WrittenEvent> TakeConsentAsync(bool allow, string eventType, JsonObject details)
    {
        await using var scope = App.Services.CreateAsyncScope();

        var principal = await scope.ServiceProvider.GetRequiredService<ConsentDecisions>().TakeAsync(
            new Actor(a.ManagerId, Channel.App),
            allow,
            McpClientId,
            authTime: "1790000000",
            TestContext.Current.CancellationToken);

        Assert.Equal(allow, principal is not null);
        return new(eventType, a.OrganisationId, a.ManagerId, null, null, null, details.ToJsonString());
    }

    private static WrittenEvent McpClientEvent(string eventType, Guid organisationId) =>
        new(
            eventType,
            organisationId,
            null,
            null,
            null,
            null,
            new JsonObject { ["client"] = McpClientId, ["name"] = McpClientName }.ToJsonString());

    // An approved assistant, registered as the command does but writing no event.
    private async Task AddApprovedAssistantAsync()
    {
        await using var scope = App.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>().CreateAsync(
            new OpenIddictApplicationDescriptor
            {
                ClientId = McpClientId,
                ClientType = OpenIddictConstants.ClientTypes.Public,
                DisplayName = McpClientName,
                RedirectUris = { new("https://assistant.example/callback") },
            },
            TestContext.Current.CancellationToken);
    }

    // An app on a migrated database of the row's own, with nothing seeded, named database.
    private async Task<WebApplicationFactory<Program>> OnADatabaseOfItsOwnAsync(string database)
    {
        var connectionString = await sqlServer.CreateDatabaseAsync(database, TestContext.Current.CancellationToken);
        return factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration(configuration =>
            configuration.AddInMemoryCollection([new("ConnectionStrings:Grow2Notes", connectionString)])));
    }

    // An organisation of the row's own, on the database of the app that the row acts through, with any guide prompts
    // given and one Active manager with an address of their own, as setup leaves them apart from the passkey or
    // password, which the test-only sign-in does not need. An organisation is the tenant, not tenant-owned, and
    // AspNetUsers is not filtered, so this needs no tenant.
    private async Task<(Guid OrganisationId, Guid ManagerId)> AddOrganisationAsync(string guidePrompts = "")
    {
        var addedAtUtc = new DateTime(2026, 10, 18, 0, 0, 0, DateTimeKind.Utc);
        await using var scope = App.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        var organisation = new Organisation
        {
            Name = "Audit events test",
            GuidePrompts = guidePrompts,
            CreatedAtUtc = addedAtUtc,
        };
        db.Organisations.Add(organisation);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var email = $"{Guid.NewGuid():N}@example.org";
        var manager = new ApplicationUser
        {
            OrganisationId = organisation.Id,
            DisplayName = "Quinn Harper",
            Role = UserRole.Manager,
            Status = UserStatus.Active,
            InvitedAtUtc = addedAtUtc,
            ActivatedAtUtc = addedAtUtc,
            UserName = email,
            Email = email,
            EmailConfirmed = true,
        };
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var created = await users.CreateAsync(manager);
        Assert.True(created.Succeeded, string.Join(" ", created.Errors.Select(error => error.Code)));
        return (organisation.Id, manager.Id);
    }

    // The Every note group of an organisation that a row adds, as admin bootstrap adds it (design.md §7.4), saved under
    // the organisation's tenant, as a tenant-owned row is.
    private async Task AddEveryNoteAsync(Guid organisationId)
    {
        await using var scope = App.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        db.CommonItemGroups.Add(CommonItemGroup.EveryNote(organisationId, TestCommonItems.AddedAtUtc));
        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(organisationId))
        {
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
    }

    // The user who has the address, as Identity stores them.
    private async Task<ApplicationUser> StoredUserAsync(string email)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByEmailAsync(email);
        Assert.NotNull(user);
        return user;
    }

    // A worker added to A with a password and a code, as setup leaves one (PasswordSignIn), in status, as Identity
    // stores them, which writes no event.
    private async Task<ApplicationUser> AddWorkerAsync(UserStatus status) =>
        await StoredUserAsync((await factory.AddPasswordUserAsync(a, status)).Email);

    // A change with no body by A's manager, such as an archive, a deactivation or a discard, with the ETag of what it
    // changes, if it has one, as If-Match, which answers 204.
    private async Task PostAsync(string path, string? ifMatch)
    {
        using var client = factory.CreateHttpsClient().SignInAs(a.ManagerId);
        using var response = await client.PostWithTokenAsync(path, await client.FetchTokenAsync(), ifMatch: ifMatch);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    // The time that the participant was archived at, as their row holds it, through the app's context under A's tenant.
    private async Task<DateTime?> StoredArchivedAtUtcAsync(Guid participantId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(a.OrganisationId))
        {
            return await db.Participants.Where(p => p.Id == participantId)
                .Select(p => p.ArchivedAtUtc)
                .SingleAsync(TestContext.Current.CancellationToken);
        }
    }

    // The time that the goal was archived at, as its row holds it, through the app's context under A's tenant.
    private async Task<DateTime?> StoredGoalArchivedAtUtcAsync(Guid goalId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(a.OrganisationId))
        {
            return await db.Goals.Where(g => g.Id == goalId)
                .Select(g => g.ArchivedAtUtc)
                .SingleAsync(TestContext.Current.CancellationToken);
        }
    }

    // The lists' version that the participant's form opens with, as the page reads it: A's lists are not the seeded
    // ones alone, since other tests add groups and items to A for good.
    private static async Task<string?> ListsVersionOfTodaysFormAsync(
        HttpClient client, Guid participantId, DateOnly today)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var path = string.Create(
            CultureInfo.InvariantCulture, $"/api/participants/{participantId}/notes/{today:yyyy-MM-dd}/draft");
        using var response = await client.GetAsync(path, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var form = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return form.GetProperty("listsVersion").GetString();
    }

    // The ID of the one note of a participant added for a row, through the app's context under A's tenant.
    private async Task<Guid> StoredNoteIdAsync(Guid participantId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(a.OrganisationId))
        {
            return await db.Notes.Where(n => n.ParticipantId == participantId)
                .Select(n => n.Id)
                .SingleAsync(TestContext.Current.CancellationToken);
        }
    }

    // The ID of the one goal of a participant added for a row, through the app's context under A's tenant.
    private async Task<Guid> StoredGoalIdAsync(Guid participantId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(a.OrganisationId))
        {
            return await db.Goals.Where(g => g.ParticipantId == participantId)
                .Select(g => g.Id)
                .SingleAsync(TestContext.Current.CancellationToken);
        }
    }

    // The time that the item was archived at, as its row holds it, through the app's context under A's tenant.
    private async Task<DateTime?> StoredItemArchivedAtUtcAsync(Guid itemId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(a.OrganisationId))
        {
            return await db.CommonItems.Where(item => item.Id == itemId)
                .Select(item => item.ArchivedAtUtc)
                .SingleAsync(TestContext.Current.CancellationToken);
        }
    }

    // The time that the group was archived at, as its row holds it, through the app's context under A's tenant.
    private async Task<DateTime?> StoredGroupArchivedAtUtcAsync(Guid groupId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(a.OrganisationId))
        {
            return await db.CommonItemGroups.Where(group => group.Id == groupId)
                .Select(group => group.ArchivedAtUtc)
                .SingleAsync(TestContext.Current.CancellationToken);
        }
    }

    // The ID of the one item of a group added for a row, through the app's context under A's tenant.
    private async Task<Guid> StoredItemIdAsync(Guid groupId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(a.OrganisationId))
        {
            return await db.CommonItems.Where(item => item.GroupId == groupId)
                .Select(item => item.Id)
                .SingleAsync(TestContext.Current.CancellationToken);
        }
    }

    // The ID of the last event written on the row's database, by any test. The tests of the collection run one at a
    // time, so the events after it are the row's alone.
    private async Task<long> LastEventIdAsync()
    {
        await using var scope = App.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Database
            .SqlQuery<long>($"SELECT COALESCE(MAX([Id]), 0) AS [Value] FROM [AuditEvent]")
            .SingleAsync(TestContext.Current.CancellationToken);
    }

    // Read in SQL, so the query filter, which would need a tenant, does not decide what is seen.
    private async Task<List<WrittenEvent>> EventsAfterAsync(long eventId)
    {
        await using var scope = App.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        return await db.Database.SqlQuery<WrittenEvent>($"""
            SELECT [EventType], [OrganisationId], [ActorUserId], [EntityType], [EntityId], [ParticipantId], [Details]
            FROM [AuditEvent]
            WHERE [Id] > {eventId}
            ORDER BY [Id]
            """).ToListAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>An audit entry's fields that a row checks, its <c>Details</c> as the JSON stored.</summary>
    private sealed record WrittenEvent(
        string EventType,
        Guid OrganisationId,
        Guid? ActorUserId,
        string? EntityType,
        Guid? EntityId,
        Guid? ParticipantId,
        string? Details);
}
