using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Grow2Notes.Web.Platform.Audit;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests.Platform.Audit;

/// <summary>
/// The shared audit-events test (design.md §5.3, §5.4, §14 M5; S05.02.01): one table-driven test with a row for each
/// event type of the catalogue, which triggers the event through the real endpoint, as the app's callers do, and finds
/// exactly one new entry, with its <c>EventType</c>, <c>OrganisationId</c>, <c>ActorUserId</c>, <c>EntityType</c>,
/// <c>EntityId</c>, <c>ParticipantId</c> and the values in its <c>Details</c>. Each task that writes an event adds its
/// row here, and T24 completes the table. Rows act in seeded organisation A and add rows of their own to it, changing
/// no seeded row, but for a change to an organisation's own row, such as its guide prompts, which a row makes in an
/// organisation that it adds (tests/README.md).
/// </summary>
[Collection<SqlServerCollection>]
public sealed class AuditEventsTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    : IClassFixture<Grow2NotesFactory>
{
    /// <summary>
    /// Each event type's row: what triggers it, through the real endpoint, which gives the entry that it must write.
    /// </summary>
    private static readonly Dictionary<string, Func<AuditEventsTests, Task<WrittenEvent>>> Rows = new()
    {
        [AuditEventTypes.User.Invited] = test => test.InviteUserAsync(),
        [AuditEventTypes.User.SetupLinkSent] = test => test.ResendInviteAsync(),
        [AuditEventTypes.Participant.Created] = test => test.AddParticipantAsync(),
        [AuditEventTypes.Participant.Updated] = test => test.CorrectParticipantAsync(),
        [AuditEventTypes.Participant.Archived] = test => test.ArchiveParticipantAsync(),
        [AuditEventTypes.Participant.Restored] = test => test.RestoreParticipantAsync(),
        [AuditEventTypes.GuidePrompts.Updated] = test => test.SaveGuidePromptsAsync(),
    };

    private readonly SeededOrganisation a = sqlServer.Seeded.A;

    public static TheoryData<string> EventTypes { get; } = new(Rows.Keys);

    [Theory]
    [MemberData(nameof(EventTypes))]
    public async Task Its_trigger_writes_exactly_one_entry_with_its_fields_and_values(string eventType)
    {
        var lastEventId = await LastEventIdAsync();

        var expected = await Rows[eventType](this);

        var written = Assert.Single(await EventsAfterAsync(lastEventId));
        Assert.Equal(eventType, expected.EventType);
        Assert.Equal(expected with { Details = null }, written with { Details = null });
        Assert.True(
            JsonNode.DeepEquals(Parse(expected.Details), Parse(written.Details)),
            $"Details: expected {expected.Details}, written {written.Details}.");
    }

    private static JsonNode? Parse(string? details) => details is null ? null : JsonNode.Parse(details);

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
    /// <c>user.setup_link_sent</c>: A's manager resends the invite of a worker invited for the row through the invite
    /// core, which writes no event, with <c>POST /api/admin/users/{id}/setup-link</c> and the worker's ETag, which
    /// records the worker as the entity, with no participant and no <c>Details</c>, so no address (A28).
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

    // An organisation of the row's own, with its guide prompts and one Active manager with an address of their own, as
    // setup leaves them apart from the passkey or password, which the test-only sign-in does not need. An organisation
    // is the tenant, not tenant-owned, and AspNetUsers is not filtered, so this needs no tenant.
    private async Task<(Guid OrganisationId, Guid ManagerId)> AddOrganisationAsync(string guidePrompts)
    {
        var addedAtUtc = new DateTime(2026, 10, 18, 0, 0, 0, DateTimeKind.Utc);
        await using var scope = factory.Services.CreateAsyncScope();
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

    // The user who has the address, as Identity stores them.
    private async Task<ApplicationUser> StoredUserAsync(string email)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByEmailAsync(email);
        Assert.NotNull(user);
        return user;
    }

    // An archive or a restore by A's manager, with the participant's ETag as If-Match and no body, which answers 204.
    private async Task PostAsync(string path, string ifMatch)
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

    // The ID of the last event written, by any test. The tests of the collection run one at a time, so the events after
    // it are the row's alone.
    private async Task<long> LastEventIdAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Database
            .SqlQuery<long>($"SELECT COALESCE(MAX([Id]), 0) AS [Value] FROM [AuditEvent]")
            .SingleAsync(TestContext.Current.CancellationToken);
    }

    // Read in SQL, so the query filter, which would need a tenant, does not decide what is seen.
    private async Task<List<WrittenEvent>> EventsAfterAsync(long eventId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
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
