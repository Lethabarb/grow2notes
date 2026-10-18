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
            ["id", "givenName", "familyName", "status"], header.EnumerateObject().Select(property => property.Name));
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
