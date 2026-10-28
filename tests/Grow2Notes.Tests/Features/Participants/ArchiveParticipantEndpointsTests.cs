using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Grow2Notes.Web.Platform.Audit;
using Grow2Notes.Web.Platform.Configuration;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Grow2Notes.Tests.Features.Participants;

/// <summary>
/// <c>POST /api/admin/participants/{id}/archive</c> and <c>/restore</c> (design.md §3.7, §6.6, S01.02.04), called
/// with the test-only sign-in (<see cref="TestSignIn"/>) on a fake clock: a manager archives or restores a participant
/// of their own organisation with the participant's <c>ETag</c> as <c>If-Match</c>, and gets <c>204</c>; with no
/// <c>If-Match</c>, <c>428</c>; with a stale one, <c>412</c>; and for another organisation's participant, <c>404</c>;
/// each of those changing nothing and writing no event. Archiving an archived participant, or restoring an active one,
/// answers <c>204</c> too, and saves nothing (D84). Each test acts on a participant that it adds to a seeded
/// organisation (tests/README.md). The endpoint matrix covers the statuses of every caller, and
/// <see cref="Platform.Audit.AuditEventsTests"/> the events that they write.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class ArchiveParticipantEndpointsTests : IClassFixture<Grow2NotesFactory>, IAsyncDisposable
{
    private const string Archive = "archive";
    private const string Restore = "restore";

    private readonly WebApplicationFactory<Program> app;

    private readonly SeededOrganisations seeded;

    public ArchiveParticipantEndpointsTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    {
        app = factory.WithWebHostBuilder(builder => builder.UseClock(new FakeTimeProvider(TestClock.Start)));
        seeded = sqlServer.Seeded;
    }

    // Each action, on a participant it changes: an active one to archive, an archived one to restore.
    public static TheoryData<string> Actions { get; } = new(Archive, Restore);

    [Fact]
    public async Task Archive_answers_204_and_archives_them_at_the_clocks_time_with_a_new_version()
    {
        var participant = await app.Services.AddParticipantAsync(seeded.A);

        using var response = await PostAsync(Caller.ManagerOfA, participant.Id, Archive, participant.ETag);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        var stored = await StoredAsync(participant.Id);
        Assert.Equal(
            (ParticipantStatus.Archived, (DateTime?)TestClock.Start.UtcDateTime),
            (stored.Status, stored.ArchivedAtUtc));
        Assert.NotEqual(participant.ETag, RowVersionToken.ETag(stored));
        Assert.Equal([AuditEventTypes.Participant.Archived], await EventTypesAsync(participant.Id));
    }

    [Fact]
    public async Task Restore_answers_204_and_makes_them_active_with_no_archive_time_and_a_new_version()
    {
        var participant = await app.Services.AddParticipantAsync(seeded.A, archived: true);

        using var response = await PostAsync(Caller.ManagerOfA, participant.Id, Restore, participant.ETag);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        var stored = await StoredAsync(participant.Id);
        Assert.Equal((ParticipantStatus.Active, (DateTime?)null), (stored.Status, stored.ArchivedAtUtc));
        Assert.NotEqual(participant.ETag, RowVersionToken.ETag(stored));
        Assert.Equal([AuditEventTypes.Participant.Restored], await EventTypesAsync(participant.Id));
    }

    // D84: a repeat finds nothing to change, so the archived participant keeps the time they were archived at.
    [Theory]
    [InlineData(Archive, true)]
    [InlineData(Restore, false)]
    public async Task A_participant_already_so_answers_204_and_nothing_is_saved(string action, bool archived)
    {
        var participant = await app.Services.AddParticipantAsync(seeded.A, archived);

        using var response = await PostAsync(Caller.ManagerOfA, participant.Id, action, participant.ETag);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await AssertUnchangedAsync(participant, archived);
    }

    [Theory]
    [MemberData(nameof(Actions))]
    public async Task With_no_If_Match_it_answers_428_and_changes_nothing(string action)
    {
        var participant = await AddToChangeAsync(action);

        using var response = await PostAsync(Caller.ManagerOfA, participant.Id, action, ifMatch: null);

        await AssertProblemAsync(response, ErrorCode.PreconditionRequired);
        await AssertUnchangedAsync(participant, archived: action == Restore);
    }

    // As when another manager changed the participant first: the action, sent with the version both loaded, changes
    // nothing.
    [Theory]
    [MemberData(nameof(Actions))]
    public async Task With_a_stale_If_Match_it_answers_412_and_writes_nothing(string action)
    {
        var participant = await AddToChangeAsync(action);
        var firstEvents = await ChangeFirstAsync(participant, action);

        var corrected = RowVersionToken.ETag(await StoredAsync(participant.Id));

        using var response = await PostAsync(Caller.ManagerOfA, participant.Id, action, participant.ETag);

        await AssertProblemAsync(response, ErrorCode.PreconditionFailed);
        var stored = await StoredAsync(participant.Id);
        Assert.Equal(
            (action == Restore ? ParticipantStatus.Archived : ParticipantStatus.Active, corrected),
            (stored.Status, RowVersionToken.ETag(stored)));
        Assert.Equal(firstEvents, await EventTypesAsync(participant.Id));
    }

    // B's manager, sending the participant's current ETag, as one who had it would: the tenant filter finds no such
    // participant in B, so the version is never checked (design.md §9.2).
    [Theory]
    [MemberData(nameof(Actions))]
    public async Task Another_organisation_s_participant_is_not_found_and_not_changed(string action)
    {
        var participant = await AddToChangeAsync(action);

        using var response = await PostAsync(Caller.ManagerOfB, participant.Id, action, participant.ETag);

        await response.ReadProblemAsync(HttpStatusCode.NotFound);
        await AssertUnchangedAsync(participant, archived: action == Restore);
    }

    [Theory]
    [MemberData(nameof(Actions))]
    public async Task An_ID_that_no_participant_has_is_not_found(string action)
    {
        using var response = await PostAsync(Caller.ManagerOfA, Guid.NewGuid(), action, IfMatch.ETag("AAAAAAAAB9E="));

        await response.ReadProblemAsync(HttpStatusCode.NotFound);
    }

    // The routes take a GUID alone, so another ID matches no endpoint, and gets the unknown API path's 404.
    [Theory]
    [MemberData(nameof(Actions))]
    public async Task An_ID_that_is_not_a_GUID_is_not_found(string action)
    {
        using var response = await PostAsync(
            Caller.ManagerOfA, $"/api/admin/participants/not-a-guid/{action}", IfMatch.ETag("AAAAAAAAB9E="));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // The two views of the Participants list (participants.md A4) are the list's active participants, and the archived
    // ones of the list with includeArchived=true, each with their status by name.
    [Fact]
    public async Task After_Archive_they_are_in_the_Archived_view_only_and_after_Restore_in_the_Active_view_only()
    {
        var participant = await app.Services.AddParticipantAsync(seeded.A);

        using (var archived = await PostAsync(Caller.ManagerOfA, participant.Id, Archive, participant.ETag))
        {
            Assert.Equal(HttpStatusCode.NoContent, archived.StatusCode);
        }

        Assert.Equal((false, "Archived"), await ListedAsync(participant.Id));

        // The version that the list gives them, as the screen sends it.
        var version = (await ListAsync(includeArchived: true))
            .Single(row => row.GetProperty("id").GetGuid() == participant.Id)
            .GetProperty("rowVersion").GetString()!;
        using (var restored = await PostAsync(Caller.ManagerOfA, participant.Id, Restore, IfMatch.ETag(version)))
        {
            Assert.Equal(HttpStatusCode.NoContent, restored.StatusCode);
        }

        Assert.Equal((true, "Active"), await ListedAsync(participant.Id));
    }

    public ValueTask DisposeAsync() => app.DisposeAsync();

    private static async Task AssertProblemAsync(HttpResponseMessage response, ErrorCode code)
    {
        using var problem = JsonDocument.Parse(await response.ReadProblemAsync((HttpStatusCode)code.Status));
        Assert.Equal(code.Code, problem.RootElement.GetProperty(ErrorCode.Extension).GetString());
    }

    // What another manager does first, which moves the version and leaves the participant's status as it was found: a
    // correction of an active participant; for an archived one, whom a correction no longer reaches (D93), a restore
    // and an archive again. Gives the events that it writes.
    private async Task<List<string>> ChangeFirstAsync(TestParticipant participant, string action)
    {
        if (action == Archive)
        {
            using var client = app.CreateHttpsClient().SignInAs(Caller.ManagerOfA, seeded);
            using var correction = await client.PutWithTokenAsync(
                $"/api/admin/participants/{participant.Id}",
                await client.FetchTokenAsync(),
                new { givenName = "Jane", familyName = "Smith", dateOfBirth = "1987-03-07" },
                participant.ETag);
            Assert.Equal(HttpStatusCode.OK, correction.StatusCode);
            return [AuditEventTypes.Participant.Updated];
        }

        using (var restored = await PostAsync(Caller.ManagerOfA, participant.Id, Restore, participant.ETag))
        {
            Assert.Equal(HttpStatusCode.NoContent, restored.StatusCode);
        }

        var restoredETag = RowVersionToken.ETag(await StoredAsync(participant.Id));
        using (var archived = await PostAsync(Caller.ManagerOfA, participant.Id, Archive, restoredETag))
        {
            Assert.Equal(HttpStatusCode.NoContent, archived.StatusCode);
        }

        return [AuditEventTypes.Participant.Restored, AuditEventTypes.Participant.Archived];
    }

    // A participant whom the action changes: an active one to archive, an archived one to restore.
    private Task<TestParticipant> AddToChangeAsync(string action) =>
        app.Services.AddParticipantAsync(seeded.A, archived: action == Restore);

    private Task<HttpResponseMessage> PostAsync(Caller caller, Guid participantId, string action, string? ifMatch) =>
        PostAsync(caller, $"/api/admin/participants/{participantId}/{action}", ifMatch);

    // As the SPA archives or restores one: over HTTPS, with a token fetched as the caller, and no body.
    private async Task<HttpResponseMessage> PostAsync(Caller caller, string path, string? ifMatch)
    {
        using var client = app.CreateHttpsClient().SignInAs(caller, seeded);
        return await client.PostWithTokenAsync(path, await client.FetchTokenAsync(), ifMatch: ifMatch);
    }

    // A's manager's list, the active participants, or with includeArchived the archived ones too.
    private async Task<List<JsonElement>> ListAsync(bool includeArchived)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = app.CreateHttpsClient().SignInAs(Caller.ManagerOfA, seeded);
        using var response = await client.GetAsync(
            includeArchived ? "/api/admin/participants?includeArchived=true" : "/api/admin/participants",
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return [.. (await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).EnumerateArray()];
    }

    // Whether the participant is in the active list, and their status in the list with the archived ones.
    private async Task<(bool InActiveList, string? Status)> ListedAsync(Guid participantId)
    {
        var active = (await ListAsync(includeArchived: false))
            .Any(row => row.GetProperty("id").GetGuid() == participantId);
        var status = (await ListAsync(includeArchived: true))
            .Single(row => row.GetProperty("id").GetGuid() == participantId)
            .GetProperty("status").GetString();
        return (active, status);
    }

    // The participant as AddParticipantAsync left them, at the same version, with no event written about them.
    private async Task AssertUnchangedAsync(TestParticipant participant, bool archived)
    {
        var stored = await StoredAsync(participant.Id);
        Assert.Equal(
            archived
                ? (ParticipantStatus.Archived, TestParticipants.ArchivedAtUtc, participant.ETag)
                : (ParticipantStatus.Active, (DateTime?)null, participant.ETag),
            (stored.Status, stored.ArchivedAtUtc, RowVersionToken.ETag(stored)));
        Assert.Empty(await EventTypesAsync(participant.Id));
    }

    // The participant as the row holds them in A, through the app's context under A's tenant.
    private async Task<Participant> StoredAsync(Guid participantId)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(seeded.A.OrganisationId))
        {
            return await db.Participants.AsNoTracking()
                .SingleAsync(p => p.Id == participantId, TestContext.Current.CancellationToken);
        }
    }

    // The events about the participant, oldest first, read in SQL, so the query filter, which would need a tenant,
    // does not decide what is seen.
    private async Task<List<string>> EventTypesAsync(Guid participantId)
    {
        await using var scope = app.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Database
            .SqlQuery<string>($"""
                SELECT [EventType] AS [Value]
                FROM [AuditEvent]
                WHERE [ParticipantId] = {participantId}
                ORDER BY [Id]
                """)
            .ToListAsync(TestContext.Current.CancellationToken);
    }
}
