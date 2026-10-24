using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests.Features.Notes;

/// <summary>
/// <c>GET /api/participants/{participantId}</c> (design.md §6.3), called with the test-only sign-in
/// (<see cref="TestSignIn"/>): any signed-in user gets a participant of their own organisation, archived or not, with
/// their names as stored and their status, never their date of birth; a participant of another organisation, or none,
/// is not found (§9.2); the answer is never stored (§6.1), and no call writes an audit event (A28). The tests add their
/// own participants (tests/README.md). The endpoint matrix covers who may call it, and <see cref="SignedOutTests"/>
/// what a signed-out caller gets and an ID that is not a GUID.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class ParticipantHeaderEndpointTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    : IClassFixture<Grow2NotesFactory>
{
    private static readonly DateTime DiscardedAtUtc = new(2026, 8, 3, 0, 0, 0, DateTimeKind.Utc);

    private readonly SeededOrganisations seeded = sqlServer.Seeded;

    [Theory]
    [InlineData(Caller.WorkerOfA)]
    [InlineData(Caller.ManagerOfA)]
    public async Task A_s_worker_and_manager_get_the_participant_s_ID_names_and_status_and_never_their_birth_date(
        Caller caller)
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);

        using var response = await GetAsync(caller, participant.Id);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var header = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(
            ["id", "givenName", "familyName", "status", "firstNoteDate"],
            header.EnumerateObject().Select(property => property.Name));
        Assert.Equal(
            (participant.Id, "Jane", "Citizen", "Active"),
            (header.GetProperty("id").GetGuid(), header.GetProperty("givenName").GetString(),
                header.GetProperty("familyName").GetString(), header.GetProperty("status").GetString()));
    }

    // A draft started before the archive can still be finished (design.md §3.7), so the form still names them.
    [Fact]
    public async Task An_archived_participant_s_header_says_so()
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A, archived: true);

        using var response = await GetAsync(Caller.WorkerOfA, participant.Id);

        var header = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("Archived", header.GetProperty("status").GetString());
    }

    // The names exactly as typed (A5), accents, apostrophes and letter case and all.
    [Fact]
    public async Task The_names_are_as_stored()
    {
        var participantId = await AddAsync(seeded.A, "Nguyễn", "O’Brien-smith");

        using var response = await GetAsync(Caller.WorkerOfA, participantId);

        var header = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(
            ("Nguyễn", "O’Brien-smith"),
            (header.GetProperty("givenName").GetString(), header.GetProperty("familyName").GetString()));
    }

    [Fact]
    public async Task B_s_manager_does_not_find_A_s_participant_and_nobody_finds_one_that_does_not_exist()
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);

        using var other = await GetAsync(Caller.ManagerOfB, participant.Id);
        using var none = await GetAsync(Caller.WorkerOfA, Guid.NewGuid());

        Assert.Equal(HttpStatusCode.NotFound, other.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, none.StatusCode);
    }

    [Fact]
    public async Task The_answer_is_JSON_that_is_never_stored_and_no_call_writes_an_audit_event()
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var lastEventId = await LastEventIdAsync();

        using var response = await GetAsync(Caller.WorkerOfA, participant.Id);
        using var refused = await GetAsync(Caller.ManagerOfB, participant.Id);

        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.False(response.Headers.Contains("ETag"));
        Assert.Equal(HttpStatusCode.NotFound, refused.StatusCode);
        Assert.Equal(lastEventId, await LastEventIdAsync());
    }

    // D80: the export screen starts its From date here. A draft and a discarded draft are not notes to count, however
    // early, and the date is the earliest submitted note's, not the first one written.
    [Fact]
    public async Task The_first_note_date_is_the_earliest_submitted_note_s_and_ignores_a_draft_and_a_discarded_draft()
    {
        var organisation = await factory.Services.AddOrganisationAsync();
        var participant = await factory.Services.AddParticipantAsync(organisation);
        await AddDraftAsync(organisation, participant.Id, new DateOnly(2026, 8, 1));
        var discarded = await AddDraftAsync(organisation, participant.Id, new DateOnly(2026, 8, 2));
        await factory.Services.DiscardAsync(organisation, discarded, DiscardedAtUtc);
        await AddSubmittedAsync(organisation, participant.Id, new DateOnly(2026, 8, 20));
        await AddSubmittedAsync(organisation, participant.Id, new DateOnly(2026, 8, 10));

        var header = await GetHeaderAsync(organisation, participant.Id);

        Assert.Equal("2026-08-10", header.GetProperty("firstNoteDate").GetString());
    }

    // Always written, as null for a participant who has no submitted note.
    [Fact]
    public async Task The_first_note_date_is_null_without_a_submitted_note()
    {
        var organisation = await factory.Services.AddOrganisationAsync();
        var drafted = await factory.Services.AddParticipantAsync(organisation, givenName: "Dora");
        await AddDraftAsync(organisation, drafted.Id, new DateOnly(2026, 8, 1));
        var discarded = await factory.Services.AddParticipantAsync(organisation, givenName: "Dan");
        await factory.Services.DiscardAsync(
            organisation,
            await AddDraftAsync(organisation, discarded.Id, new DateOnly(2026, 8, 1)),
            DiscardedAtUtc);
        var none = await factory.Services.AddParticipantAsync(organisation, givenName: "Nora");

        foreach (var participant in new[] { drafted, discarded, none })
        {
            var header = await GetHeaderAsync(organisation, participant.Id);

            Assert.True(header.TryGetProperty("firstNoteDate", out var firstNoteDate));
            Assert.Equal(JsonValueKind.Null, firstNoteDate.ValueKind);
        }
    }

    // §5.9 item 7: another organisation's notes, of the same days, are not this participant's.
    [Fact]
    public async Task Another_organisation_s_notes_never_give_a_participant_a_first_note_date()
    {
        var organisation = await factory.Services.AddOrganisationAsync();
        var other = await factory.Services.AddOrganisationAsync();
        var participant = await factory.Services.AddParticipantAsync(organisation);
        var otherParticipant = await factory.Services.AddParticipantAsync(other);
        await AddSubmittedAsync(other, otherParticipant.Id, new DateOnly(2026, 8, 10));

        var header = await GetHeaderAsync(organisation, participant.Id);

        Assert.Equal(JsonValueKind.Null, header.GetProperty("firstNoteDate").ValueKind);
    }

    // The header is for any signed-in user, so it never carries the date of birth, with or without a note.
    [Fact]
    public async Task The_header_with_a_first_note_date_still_has_no_birth_date()
    {
        var organisation = await factory.Services.AddOrganisationAsync();
        var participant = await factory.Services.AddParticipantAsync(organisation);
        await AddSubmittedAsync(organisation, participant.Id, new DateOnly(2026, 8, 10));

        var header = await GetHeaderAsync(organisation, participant.Id);

        Assert.DoesNotContain(
            header.EnumerateObject(),
            property => property.Name.Contains("birth", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("1987", header.GetRawText(), StringComparison.Ordinal);
    }

    private async Task<JsonElement> GetHeaderAsync(SeededOrganisation organisation, Guid participantId)
    {
        using var client = factory.CreateHttpsClient().SignInAs(organisation.WorkerId);
        using var response = await client.GetAsync(
            $"/api/participants/{participantId}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
    }

    private Task<Guid> AddDraftAsync(SeededOrganisation organisation, Guid participantId, DateOnly noteDate) =>
        factory.Services.AddDraftAsync(
            organisation,
            new(participantId, noteDate, organisation.WorkerId, new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc)));

    private Task<Guid> AddSubmittedAsync(SeededOrganisation organisation, Guid participantId, DateOnly noteDate) =>
        factory.Services.AddSubmittedAsync(
            organisation,
            new(
                participantId,
                noteDate,
                organisation.WorkerId,
                new DateTime(2026, 10, 1, 0, 14, 0, DateTimeKind.Utc),
                new DateTime(2026, 10, 1, 6, 42, 0, DateTimeKind.Utc)));

    // Over HTTPS, as the SPA calls the /api group (tests/README.md).
    private async Task<HttpResponseMessage> GetAsync(Caller caller, Guid participantId)
    {
        using var client = factory.CreateHttpsClient().SignInAs(caller, seeded);
        return await client.GetAsync($"/api/participants/{participantId}", TestContext.Current.CancellationToken);
    }

    // A participant of the organisation's named as given, born on 7 March 1987, as the app adds one.
    private async Task<Guid> AddAsync(SeededOrganisation organisation, string givenName, string familyName)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        var participant = new Participant
        {
            OrganisationId = organisation.OrganisationId,
            GivenName = givenName,
            FamilyName = familyName,
            DateOfBirth = new DateOnly(1987, 3, 7),
            CreatedAtUtc = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
        };
        db.Participants.Add(participant);
        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(organisation.OrganisationId))
        {
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        return participant.Id;
    }

    // The ID of the last event written, by any test: the tests of the collection run one at a time, so none writes one
    // while a test reads it before and after its requests.
    private async Task<long?> LastEventIdAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Database
            .SqlQuery<long?>($"SELECT MAX([Id]) AS [Value] FROM [AuditEvent]")
            .SingleAsync(TestContext.Current.CancellationToken);
    }
}
