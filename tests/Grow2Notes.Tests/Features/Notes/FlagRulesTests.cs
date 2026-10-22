using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Features.Notes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests.Features.Notes;

/// <summary>
/// design.md §5.8's flag rules (D17, D18, A15), one table-driven theory whose rows are rows of those rules alone, each
/// a version saved through <c>POST {base}/versions</c> from the note's flag status before it, checking its flag status
/// and flagged version afterwards (F03.01's feature check). The tests add their own participants and drafts
/// (tests/README.md).
/// </summary>
[Collection<SqlServerCollection>]
public sealed class FlagRulesTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    : IClassFixture<Grow2NotesFactory>
{
    // Made up (A35): a draft for Friday 2 October 2026, started at 9:14 am in Melbourne.
    private static readonly DateOnly NoteDate = new(2026, 10, 2);

    private static readonly DateTime StartedAtUtc = new(2026, 10, 1, 23, 14, 0, DateTimeKind.Utc);

    private readonly SeededOrganisations seeded = sqlServer.Seeded;

    // Version 1, Submit, from None, the only start that it has, as a draft has no flag status (§3.6): flagged, the
    // first row, which puts the note in To review with this version its flagged version; and not flagged, the last,
    // which leaves both as they are. T16 adds versions 2 and later, from each start, with Save changes.
    [Theory]
    [InlineData(true, "None", "ToReview", 1)]
    [InlineData(false, "None", "None", null)]
    public async Task Version_1_leaves_the_flag_status_and_flagged_version_that_the_flag_rules_give(
        bool flagged, string from, string becomes, int? flaggedVersionNumber)
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var noteId = await factory.Services.AddDraftAsync(
            seeded.A, new(participant.Id, NoteDate, seeded.A.WorkerId, StartedAtUtc, Narrative: "Jane"));
        Assert.Equal((Enum.Parse<FlagStatus>(from), null), await StoredFlagAsync(noteId));
        using var client = factory.CreateHttpsClient().SignInAs(Caller.WorkerOfA, seeded);

        using var response = await SubmitAsync(
            client, participant.Id, flagged, flagged ? "Jane seemed unwell." : null);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal((Enum.Parse<FlagStatus>(becomes), flaggedVersionNumber), await StoredFlagAsync(noteId));
    }

    // Submit of the participant's note for NoteDate as the SPA sends it (tests/README.md): over HTTPS, with the
    // caller's antiforgery token and a key of its own, on base 0, with its text and its flag.
    private static async Task<HttpResponseMessage> SubmitAsync(
        HttpClient client, Guid participantId, bool isFlagged, string? flagReason)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            string.Create(
                CultureInfo.InvariantCulture,
                $"/api/participants/{participantId}/notes/{NoteDate:yyyy-MM-dd}/versions"))
        {
            Content = JsonContent.Create(new
            {
                baseVersion = 0,
                narrative = "Jane made toast.",
                tickedGoalIds = Array.Empty<Guid>(),
                pickedGroupIds = Array.Empty<Guid>(),
                tickedCommonItemIds = Array.Empty<Guid>(),
                isFlagged,
                flagReason,
            }),
        };
        request.Headers.Add(AntiforgeryTokens.Header, await client.FetchTokenAsync());
        request.Headers.Add(NoteVersionEndpoints.IdempotencyKeyHeader, Guid.NewGuid().ToString());
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    // The note's flag status and flagged version, read in SQL from a scope of its own, so that neither the query
    // filter, which would need a tenant, nor a context's tracked rows decide what is seen.
    private async Task<(FlagStatus FlagStatus, int? FlaggedVersionNumber)> StoredFlagAsync(Guid noteId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var flag = await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Database
            .SqlQuery<StoredFlag>($"""
                SELECT [FlagStatus], [FlaggedVersionNumber]
                FROM [Note]
                WHERE [Id] = {noteId}
                """)
            .SingleAsync(TestContext.Current.CancellationToken);
        return ((FlagStatus)flag.FlagStatus, flag.FlaggedVersionNumber);
    }

    /// <summary>A <c>Note</c> row's flag status, as its stored <c>tinyint</c>, and its flagged version.</summary>
    private sealed record StoredFlag(byte FlagStatus, int? FlaggedVersionNumber);
}
