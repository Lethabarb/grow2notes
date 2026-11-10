using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Grow2Notes.Web.Platform.Audit;
using Grow2Notes.Web.Platform.Configuration;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Grow2Notes.Tests.Features.Participants;

/// <summary>
/// A participant's Medication switch on <c>PUT /api/admin/participants/{id}</c> and <c>POST /api/admin/participants</c>
/// (design.md §6.6, D104, A152), called with the test-only sign-in (<see cref="TestSignIn"/>): a manager turns the
/// switch on and then off with the participant's <c>ETag</c> as <c>If-Match</c>, the event <c>participant.updated</c>
/// records its old and new value, a request that leaves it out keeps it, one that sends the stored value writes nothing
/// and answers the same <c>ETag</c> (D84), a missing <c>If-Match</c> gets <c>428</c>, a stale one <c>412</c>, an
/// archived participant <c>422 participant.archived</c> before the <c>If-Match</c> (D93), and a new participant starts
/// with the switch off. Each test adds a participant of its own in A (tests/README.md). The other details are in
/// <see cref="UpdateParticipantEndpointTests"/>.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class MedicationLineEndpointTests : IClassFixture<Grow2NotesFactory>, IAsyncDisposable
{
    private readonly WebApplicationFactory<Program> app;

    private readonly SeededOrganisations seeded;

    public MedicationLineEndpointTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    {
        app = factory.WithWebHostBuilder(builder => builder.UseClock(new FakeTimeProvider(TestClock.Start)));
        seeded = sqlServer.Seeded;
    }

    [Fact]
    public async Task A_manager_turns_the_switch_on_and_then_off_and_the_events_record_both_values()
    {
        var participant = await app.Services.AddParticipantAsync(seeded.A);

        using var on = await PutAsync(participant.Id, Details(hasMedicationLine: true), participant.ETag);

        Assert.Equal(HttpStatusCode.OK, on.StatusCode);
        var afterOn = await StoredAsync(participant.Id);
        Assert.True(afterOn.HasMedicationLine);
        Assert.NotEqual(participant.ETag, RowVersionToken.ETag(afterOn));

        using var off = await PutAsync(
            participant.Id, Details(hasMedicationLine: false), RowVersionToken.ETag(afterOn));

        Assert.Equal(HttpStatusCode.OK, off.StatusCode);
        Assert.False((await StoredAsync(participant.Id)).HasMedicationLine);
        var details = await DetailsAsync(participant.Id);
        Assert.Equal(2, details.Count);
        Assert.True(JsonNode.DeepEquals(new JsonObject { ["hasMedicationLine"] = false }, details[0]["old"]));
        Assert.True(JsonNode.DeepEquals(new JsonObject { ["hasMedicationLine"] = true }, details[0]["new"]));
        Assert.True(JsonNode.DeepEquals(new JsonObject { ["hasMedicationLine"] = true }, details[1]["old"]));
        Assert.True(JsonNode.DeepEquals(new JsonObject { ["hasMedicationLine"] = false }, details[1]["new"]));
    }

    // A page that does not know the switch sends the three details alone, and must not turn it off.
    [Fact]
    public async Task A_request_that_leaves_the_switch_out_keeps_it()
    {
        var participant = await app.Services.AddParticipantAsync(seeded.A, hasMedicationLine: true);

        using var response = await PutAsync(
            participant.Id,
            new { givenName = "Janet", familyName = "Citizen", dateOfBirth = "1987-03-07" },
            participant.ETag);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var stored = await StoredAsync(participant.Id);
        Assert.Equal(("Janet", true), (stored.GivenName, stored.HasMedicationLine));
        var details = Assert.Single(await DetailsAsync(participant.Id));
        Assert.True(JsonNode.DeepEquals(new JsonObject { ["givenName"] = "Janet" }, details["new"]));
    }

    // D84: the switch is already as sent, so nothing is saved, and the version stays the one the If-Match matched.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task The_stored_switch_sent_again_answers_200_with_the_same_ETag_and_writes_nothing(bool stored)
    {
        var participant = await app.Services.AddParticipantAsync(seeded.A, hasMedicationLine: stored);

        using var response = await PutAsync(participant.Id, Details(stored), participant.ETag);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(participant.ETag, RowVersionToken.ETag(await StoredAsync(participant.Id)));
        Assert.Empty(await DetailsAsync(participant.Id));
    }

    [Fact]
    public async Task With_no_If_Match_it_answers_428_and_changes_nothing()
    {
        var participant = await app.Services.AddParticipantAsync(seeded.A);

        using var response = await PutAsync(participant.Id, Details(hasMedicationLine: true), null);

        await AssertProblemAsync(response, ErrorCode.PreconditionRequired);
        Assert.False((await StoredAsync(participant.Id)).HasMedicationLine);
        Assert.Empty(await DetailsAsync(participant.Id));
    }

    [Fact]
    public async Task With_a_stale_If_Match_it_answers_412_and_changes_nothing()
    {
        var participant = await app.Services.AddParticipantAsync(seeded.A);
        using (var first = await PutAsync(
            participant.Id,
            new { givenName = "Janet", familyName = "Citizen", dateOfBirth = "1987-03-07" },
            participant.ETag))
        {
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        }

        using var response = await PutAsync(participant.Id, Details(hasMedicationLine: true), participant.ETag);

        await AssertProblemAsync(response, ErrorCode.PreconditionFailed);
        Assert.False((await StoredAsync(participant.Id)).HasMedicationLine);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task An_archived_participant_answers_422_before_the_If_Match_and_the_switch_stays(bool sendsETag)
    {
        var participant = await app.Services.AddParticipantAsync(seeded.A, archived: true);

        using var response = await PutAsync(
            participant.Id, Details(hasMedicationLine: true), sendsETag ? participant.ETag : null);

        await AssertProblemAsync(response, ErrorCode.ParticipantArchived);
        Assert.False((await StoredAsync(participant.Id)).HasMedicationLine);
    }

    [Fact]
    public async Task A_new_participant_starts_with_the_switch_off_even_if_the_body_says_otherwise()
    {
        using var client = app.CreateHttpsClient().SignInAs(Caller.ManagerOfA, seeded);

        using var response = await client.PostWithTokenAsync(
            "/api/admin/participants", await client.FetchTokenAsync(), Details(hasMedicationLine: true));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var id = (await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken))
            .GetProperty("id").GetGuid();
        Assert.False((await StoredAsync(id)).HasMedicationLine);
    }

    [Fact]
    public async Task The_admin_list_gives_each_participant_s_switch()
    {
        var participant = await app.Services.AddParticipantAsync(
            seeded.A, givenName: "Medication", familyName: Guid.NewGuid().ToString("N"), hasMedicationLine: true);
        using var client = app.CreateHttpsClient().SignInAs(Caller.ManagerOfA, seeded);

        using var response = await client.GetAsync("/api/admin/participants", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var list = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        var listed = list.EnumerateArray().Single(entry => entry.GetProperty("id").GetGuid() == participant.Id);
        Assert.True(listed.GetProperty("hasMedicationLine").GetBoolean());
    }

    public ValueTask DisposeAsync() => app.DisposeAsync();

    private static object Details(bool hasMedicationLine) => new
    {
        givenName = "Jane",
        familyName = "Citizen",
        dateOfBirth = "1987-03-07",
        hasMedicationLine,
    };

    private static async Task AssertProblemAsync(HttpResponseMessage response, ErrorCode code)
    {
        using var problem = JsonDocument.Parse(await response.ReadProblemAsync((HttpStatusCode)code.Status));
        Assert.Equal(code.Code, problem.RootElement.GetProperty(ErrorCode.Extension).GetString());
    }

    private async Task<HttpResponseMessage> PutAsync(Guid participantId, object body, string? ifMatch)
    {
        using var client = app.CreateHttpsClient().SignInAs(Caller.ManagerOfA, seeded);
        return await client.PutWithTokenAsync(
            $"/api/admin/participants/{participantId}", await client.FetchTokenAsync(), body, ifMatch);
    }

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

    // The Details of the events about the participant, oldest first, read in SQL, with no tenant needed.
    private async Task<List<JsonNode>> DetailsAsync(Guid participantId)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var details = await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Database
            .SqlQuery<string>($"""
                SELECT [Details] AS [Value]
                FROM [AuditEvent]
                WHERE [ParticipantId] = {participantId}
                ORDER BY [Id]
                """)
            .ToListAsync(TestContext.Current.CancellationToken);
        return [.. details.Select(text => JsonNode.Parse(text)!)];
    }
}
