using System.Net;
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
/// <c>PUT /api/admin/participants/{id}</c> (design.md §6.6, S01.02.03), called with the test-only sign-in
/// (<see cref="TestSignIn"/>) on a fake clock: a manager corrects a participant of their own organisation with the
/// participant's <c>ETag</c> as <c>If-Match</c>, by the add's rules, and gets <c>200</c> with no body; with no
/// <c>If-Match</c>, <c>428</c>; with a stale one, <c>412</c>; for another organisation's participant, <c>404</c>; and
/// for a field that breaks a rule, <c>422 validation.failed</c>; each changing nothing and writing no event. A
/// correction that changes no value answers <c>200</c> too, and writes nothing (D84). Each test corrects a participant
/// that it adds to a seeded organisation (tests/README.md). The endpoint matrix covers the statuses of every caller,
/// and <see cref="Platform.Audit.AuditEventsTests"/> the <c>participant.updated</c> that a correction writes.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class UpdateParticipantEndpointTests : IClassFixture<Grow2NotesFactory>, IAsyncDisposable
{
    private readonly WebApplicationFactory<Program> app;

    private readonly SeededOrganisations seeded;

    public UpdateParticipantEndpointTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    {
        // 00:30 on 10 October 2026 in Melbourne, daylight saving time (UTC+11), while it is still 9 October in UTC, so
        // that the date of birth is checked against Melbourne's date, not UTC's or the machine's.
        app = factory.WithWebHostBuilder(builder => builder.UseClock(new FakeTimeProvider(TestClock.Start)));
        seeded = sqlServer.Seeded;
    }

    [Fact]
    public async Task A_correction_answers_200_with_no_body_and_stores_the_details_with_a_new_version()
    {
        var participant = await app.Services.AddParticipantAsync(seeded.A);

        using var response = await PutAsync(
            Caller.ManagerOfA,
            participant.Id,
            new { givenName = "Janet", familyName = "Citizen-Hall", dateOfBirth = "1986-03-07" },
            participant.ETag);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Empty(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var stored = await StoredAsync(participant.Id);
        Assert.Equal(
            ("Janet", "Citizen-Hall", new DateOnly(1986, 3, 7), ParticipantStatus.Active),
            (stored.GivenName, stored.FamilyName, stored.DateOfBirth, stored.Status));
        Assert.NotEqual(participant.ETag, RowVersionToken.ETag(stored));
        Assert.Equal([AuditEventTypes.Participant.Updated], await EventTypesAsync(participant.Id));
    }

    // Every character accepted and nothing re-cased (participants.md B5): only the ends are trimmed.
    [Theory]
    [InlineData(" Ravi ", "de Silva\t", "Ravi", "de Silva")]
    [InlineData("Seán", " O'Brien", "Seán", "O'Brien")]
    [InlineData("Lan", "Nguyễn\n", "Lan", "Nguyễn")]
    [InlineData("\tMary ", "Smith-Jones  ", "Mary", "Smith-Jones")]
    public async Task Names_are_stored_as_typed_with_only_their_ends_trimmed(
        string givenName, string familyName, string storedGivenName, string storedFamilyName)
    {
        var participant = await app.Services.AddParticipantAsync(seeded.A);

        using var response = await PutAsync(
            Caller.ManagerOfA, participant.Id, new { givenName, familyName, dateOfBirth = "1987-03-07" },
            participant.ETag);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var stored = await StoredAsync(participant.Id);
        Assert.Equal((storedGivenName, storedFamilyName), (stored.GivenName, stored.FamilyName));
    }

    // The add's rules, each field's first failing check in participants.md's words, the date of birth's against today
    // in Melbourne, 10 October, though it is 9 October in UTC.
    public static TheoryData<string, string?, string> RefusedFields { get; } = new()
    {
        { "givenName", " ", "Enter the given name" },
        { "familyName", null, "Enter the family name" },
        {
            "familyName", new string('a', Limits.ParticipantName + 1), "Family name must be 100 characters or less"
        },
        { "dateOfBirth", "2026-10-10", "Date of birth must be in the past" },
        { "dateOfBirth", "2030-01-01", "Date of birth must be in the past" },
        { "dateOfBirth", null, "Enter the date of birth" },
    };

    [Theory]
    [MemberData(nameof(RefusedFields))]
    public async Task A_field_that_breaks_a_rule_answers_422_and_changes_nothing(
        string field, string? value, string message)
    {
        var participant = await app.Services.AddParticipantAsync(seeded.A);
        Dictionary<string, string?> body = new()
        {
            ["givenName"] = "Jane",
            ["familyName"] = "Smith",
            ["dateOfBirth"] = "1987-03-07",
            [field] = value,
        };

        using var response = await PutAsync(Caller.ManagerOfA, participant.Id, body, participant.ETag);

        using var problem = JsonDocument.Parse(await response.ReadProblemAsync(HttpStatusCode.UnprocessableEntity));
        Assert.Equal(
            ErrorCode.ValidationFailed.Code, problem.RootElement.GetProperty(ErrorCode.Extension).GetString());
        var refused = Assert.Single(problem.RootElement.GetProperty("errors").EnumerateObject());
        Assert.Equal((field, message), (refused.Name, Assert.Single(refused.Value.EnumerateArray()).GetString()));
        await AssertUnchangedAsync(participant);
    }

    [Fact]
    public async Task With_no_If_Match_it_answers_428_and_changes_nothing()
    {
        var participant = await app.Services.AddParticipantAsync(seeded.A);

        using var response = await PutAsync(Caller.ManagerOfA, participant.Id, Corrected, ifMatch: null);

        await AssertProblemAsync(response, ErrorCode.PreconditionRequired);
        await AssertUnchangedAsync(participant);
    }

    // As when another manager saved first: the second correction, sent with the version both loaded, changes nothing.
    [Fact]
    public async Task With_a_stale_If_Match_it_answers_412_and_writes_nothing()
    {
        var participant = await app.Services.AddParticipantAsync(seeded.A);
        using (var first = await PutAsync(Caller.ManagerOfA, participant.Id, Corrected, participant.ETag))
        {
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        }

        var afterFirst = await StoredAsync(participant.Id);

        using var response = await PutAsync(
            Caller.ManagerOfA,
            participant.Id,
            new { givenName = "Jane", familyName = "Other", dateOfBirth = "1980-01-01" },
            participant.ETag);

        await AssertProblemAsync(response, ErrorCode.PreconditionFailed);
        var stored = await StoredAsync(participant.Id);
        Assert.Equal(
            (afterFirst.GivenName, afterFirst.FamilyName, afterFirst.DateOfBirth, RowVersionToken.ETag(afterFirst)),
            (stored.GivenName, stored.FamilyName, stored.DateOfBirth, RowVersionToken.ETag(stored)));
        Assert.Equal([AuditEventTypes.Participant.Updated], await EventTypesAsync(participant.Id));
    }

    // B's manager, sending the participant's current ETag, as one who had it would: the tenant filter finds no such
    // participant in B, so the version is never checked (design.md §9.2).
    [Fact]
    public async Task Another_organisation_s_participant_is_not_found_and_not_changed()
    {
        var participant = await app.Services.AddParticipantAsync(seeded.A);

        using var response = await PutAsync(Caller.ManagerOfB, participant.Id, Corrected, participant.ETag);

        await response.ReadProblemAsync(HttpStatusCode.NotFound);
        await AssertUnchangedAsync(participant);
    }

    [Fact]
    public async Task An_ID_that_no_participant_has_is_not_found()
    {
        using var response = await PutAsync(Caller.ManagerOfA, Guid.NewGuid(), Corrected, IfMatch.ETag("AAAAAAAAB9E="));

        await response.ReadProblemAsync(HttpStatusCode.NotFound);
    }

    // The route takes a GUID alone, so another ID matches no endpoint, and gets the unknown API path's 404.
    [Fact]
    public async Task An_ID_that_is_not_a_GUID_is_not_found()
    {
        using var response = await PutAsync(
            Caller.ManagerOfA, "/api/admin/participants/not-a-guid", Corrected, IfMatch.ETag("AAAAAAAAB9E="));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // D84: the save finds no value changed, so it saves nothing, and the version stays as it was.
    [Fact]
    public async Task A_correction_to_the_stored_values_answers_200_and_saves_nothing()
    {
        var participant = await app.Services.AddParticipantAsync(seeded.A);

        using var response = await PutAsync(
            Caller.ManagerOfA,
            participant.Id,
            new { givenName = " Jane", familyName = "Citizen ", dateOfBirth = "1987-03-07" },
            participant.ETag);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await AssertUnchangedAsync(participant);
    }

    // design.md §5.3's fields alone (A5): the body's other fields, its own ID, status and organisation among them, are
    // passed over, so a correction neither archives the participant nor moves them to another organisation.
    [Fact]
    public async Task Nothing_beyond_the_three_details_is_taken_from_the_body()
    {
        var participant = await app.Services.AddParticipantAsync(seeded.A);

        using var response = await PutAsync(
            Caller.ManagerOfA,
            participant.Id,
            new
            {
                id = Guid.NewGuid(),
                organisationId = seeded.B.OrganisationId,
                givenName = "Jane",
                familyName = "Citizen",
                dateOfBirth = "1987-03-07",
                status = "Archived",
                archivedAtUtc = "2026-10-01T00:00:00Z",
                ndisNumber = "430000001",
            },
            participant.ETag);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var stored = await StoredAsync(participant.Id);
        Assert.Equal((ParticipantStatus.Active, (DateTime?)null), (stored.Status, stored.ArchivedAtUtc));
        await AssertUnchangedAsync(participant);
    }

    public ValueTask DisposeAsync() => app.DisposeAsync();

    // A correction of the family name alone.
    private static object Corrected => new { givenName = "Jane", familyName = "Smith", dateOfBirth = "1987-03-07" };

    private static async Task AssertProblemAsync(HttpResponseMessage response, ErrorCode code)
    {
        using var problem = JsonDocument.Parse(await response.ReadProblemAsync((HttpStatusCode)code.Status));
        Assert.Equal(code.Code, problem.RootElement.GetProperty(ErrorCode.Extension).GetString());
    }

    private Task<HttpResponseMessage> PutAsync(Caller caller, Guid participantId, object body, string? ifMatch) =>
        PutAsync(caller, $"/api/admin/participants/{participantId}", body, ifMatch);

    // As the SPA corrects one: over HTTPS, with a token fetched as the caller.
    private async Task<HttpResponseMessage> PutAsync(Caller caller, string path, object body, string? ifMatch)
    {
        using var client = app.CreateHttpsClient().SignInAs(caller, seeded);
        return await client.PutWithTokenAsync(path, await client.FetchTokenAsync(), body, ifMatch);
    }

    // The participant as AddParticipantAsync left them, at the same version, with no event written about them.
    private async Task AssertUnchangedAsync(TestParticipant participant)
    {
        var stored = await StoredAsync(participant.Id);
        Assert.Equal(
            ("Jane", "Citizen", new DateOnly(1987, 3, 7), participant.ETag),
            (stored.GivenName, stored.FamilyName, stored.DateOfBirth, RowVersionToken.ETag(stored)));
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
