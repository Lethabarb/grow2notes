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
/// design.md §5.8's flag rules (D17, D18, A15), two table-driven theories whose rows are rows of those rules alone,
/// one for version 1 and one for later versions, each a version saved through <c>POST {base}/versions</c> from the
/// note's flag status before it, checking its flag status and flagged version afterwards (F03.01's feature check). The
/// tests add their own participants, drafts and notes (tests/README.md).
/// </summary>
[Collection<SqlServerCollection>]
public sealed class FlagRulesTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    : IClassFixture<Grow2NotesFactory>
{
    // Made up (A35): a draft for Friday 2 October 2026, started at 9:14 am in Melbourne.
    private static readonly DateOnly NoteDate = new(2026, 10, 2);

    private static readonly DateTime StartedAtUtc = new(2026, 10, 1, 23, 14, 0, DateTimeKind.Utc);

    private static readonly DateTime SubmittedAtUtc = StartedAtUtc.AddHours(1);

    private readonly SeededOrganisations seeded = sqlServer.Seeded;

    // Version 1, Submit, from None, the only start that it has, as a draft has no flag status (§3.6): flagged, the
    // first row, which puts the note in To review with this version its flagged version; and not flagged, the last,
    // which leaves both as they are. Later versions have their own theory below.
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
            client, participant.Id, 0, flagged, flagged ? "Jane seemed unwell." : null);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal((Enum.Parse<FlagStatus>(becomes), flaggedVersionNumber), await StoredFlagAsync(noteId));
    }

    // A later version, Save changes, from each start (D17, D18, A15, S03.01.02): the note's flag status before it, the
    // reason of the base version (null when that is not flagged), whether the base is an unflagged version 2 over that
    // flagged version 1, the reason that it saves (null when not flagged), and the flag status and flagged version
    // after. A flagged version whose base was not flagged, or had another reason, puts the note in To review from any
    // start with this version its flagged version, and otherwise nothing changes, so an unflagged one never takes a
    // note out of To review. The reasons compare exactly, so 'A' and 'a' differ.
    [Theory]
    [InlineData("None", null, false, "A", "ToReview", 2)]
    [InlineData("None", null, false, null, "None", null)]
    [InlineData("ToReview", "A", false, "B", "ToReview", 2)]
    [InlineData("ToReview", "A", false, "A", "ToReview", 1)]
    [InlineData("ToReview", "A", false, null, "ToReview", 1)]
    [InlineData("ToReview", "A", true, "A", "ToReview", 3)]
    [InlineData("Reviewed", "A", false, "B", "ToReview", 2)]
    [InlineData("Reviewed", "A", false, "A", "Reviewed", 1)]
    [InlineData("Reviewed", "A", false, null, "Reviewed", 1)]
    [InlineData("Reviewed", "A", false, "a", "ToReview", 2)]
    [InlineData("Reviewed", "A", true, "A", "ToReview", 3)]
    public async Task A_later_version_leaves_the_flag_status_and_flagged_version_that_the_flag_rules_give(
        string from,
        string? baseReason,
        bool baseIsVersion2,
        string? savedReason,
        string becomes,
        int? flaggedVersionNumber)
    {
        var fromStatus = Enum.Parse<FlagStatus>(from);
        var (participantId, noteId) = await AddNoteAsync(fromStatus, baseReason, baseIsVersion2);
        var baseVersion = baseIsVersion2 ? 2 : 1;
        await factory.Services.AddPendingEditAsync(
            seeded.A, noteId, seeded.A.WorkerId, SubmittedAtUtc.AddHours(2), baseVersion);
        Assert.Equal((fromStatus, fromStatus == FlagStatus.None ? (int?)null : 1), await StoredFlagAsync(noteId));
        using var client = factory.CreateHttpsClient().SignInAs(Caller.WorkerOfA, seeded);

        using var response = await SubmitAsync(
            client, participantId, baseVersion, savedReason is not null, savedReason);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal((Enum.Parse<FlagStatus>(becomes), flaggedVersionNumber), await StoredFlagAsync(noteId));
    }

    // A Save changes whose update is refused because another editor saved first (S02.06.03) changes neither the flag
    // status nor the flagged version, though it would have flagged the note: its flag is set in the update that moves
    // the current version, or not at all. A stale base is refused before that update, so the manager's save is held
    // just before it while the worker's saves version 2.
    [Fact]
    public async Task A_save_refused_with_a_version_conflict_leaves_the_flag_status_and_flagged_version()
    {
        var (participantId, noteId) = await AddNoteAsync(FlagStatus.None, baseReason: null, baseIsVersion2: false);
        await factory.Services.AddPendingEditAsync(seeded.A, noteId, seeded.A.WorkerId, SubmittedAtUtc.AddHours(2));
        await factory.Services.AddPendingEditAsync(seeded.A, noteId, seeded.A.ManagerId, SubmittedAtUtc.AddHours(2));
        var gate = new HoldsTheHeaderUpdateOf(noteId);
        await using var gated = gate.On(factory);
        using var worker = gated.CreateHttpsClient().SignInAs(Caller.WorkerOfA, seeded);
        using var manager = gated.CreateHttpsClient().SignInAs(Caller.ManagerOfA, seeded);

        var held = SubmitAsync(manager, participantId, 1, true, "Jane seemed unwell.");
        HttpResponseMessage first;
        try
        {
            await gate.Reached.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            first = await SubmitAsync(worker, participantId, 1, false, null);
        }
        finally
        {
            gate.Release();
        }

        using var firstResponse = first;
        using var refused = await held;
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Contains(
            "note.version_conflict", await refused.ReadProblemAsync(HttpStatusCode.Conflict), StringComparison.Ordinal);
        Assert.Equal((FlagStatus.None, (int?)null), await StoredFlagAsync(noteId));
    }

    // A participant with a submitted note of A's worker for NoteDate, whose version 1 is flagged with the reason given
    // (not flagged when it is null) and whose flag status is the one given; with, when baseIsVersion2, an unflagged
    // version 2 on top. Their IDs.
    private async Task<(Guid ParticipantId, Guid NoteId)> AddNoteAsync(
        FlagStatus flagStatus, string? baseReason, bool baseIsVersion2)
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var noteId = await factory.Services.AddSubmittedAsync(seeded.A, new(
            participant.Id,
            NoteDate,
            seeded.A.WorkerId,
            StartedAtUtc,
            SubmittedAtUtc,
            IsFlagged: baseReason is not null,
            FlagReason: baseReason,
            FlagStatus: flagStatus));
        if (baseIsVersion2)
        {
            await factory.Services.AddVersion2Async(
                seeded.A, noteId, SubmittedAtUtc.AddHours(1), flagReason: null, flaggedVersionNumber: 1);
        }

        return (participant.Id, noteId);
    }

    // Submit or Save changes of the participant's note for NoteDate as the SPA sends it (tests/README.md): over HTTPS,
    // with the caller's antiforgery token and a key of its own, on the base version given, with its text and its flag.
    private static async Task<HttpResponseMessage> SubmitAsync(
        HttpClient client, Guid participantId, int baseVersion, bool isFlagged, string? flagReason)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            string.Create(
                CultureInfo.InvariantCulture,
                $"/api/participants/{participantId}/notes/{NoteDate:yyyy-MM-dd}/versions"))
        {
            Content = JsonContent.Create(new
            {
                baseVersion,
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
