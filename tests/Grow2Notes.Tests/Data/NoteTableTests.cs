using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests.Data;

/// <summary>
/// The <c>Note</c> and <c>NoteDraft</c> tables on SQL Server (design.md §5.3, §5.5, §5.8): the filtered unique index
/// that allows one live note per participant and day, the check constraint that ties each status to its columns, a
/// working copy's JSON columns, and the app's refusal to change a note's participant, date or author. The tests save
/// through the app's context, connected as <c>sa</c>, to which no deny applies, so only the database's own constraints
/// and the app's check stand between each save and the rows. Each test adds a participant of its own in A
/// (tests/README.md). <see cref="NoteModelTests"/> and <see cref="NoteDraftModelTests"/> check the model.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class NoteTableTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    : IClassFixture<Grow2NotesFactory>
{
    private static readonly DateOnly NoteDate = new(2026, 10, 4);

    // 9:14 am in Melbourne on the note's date, and 10:30 am.
    private static readonly DateTime StartedAtUtc = new(2026, 10, 3, 22, 14, 0, DateTimeKind.Utc);

    private static readonly DateTime LaterUtc = new(2026, 10, 3, 23, 30, 0, DateTimeKind.Utc);

    private SeededOrganisation A => sqlServer.Seeded.A;

    // As their stored values, as the statuses are internal to the app.
    public static TheoryData<byte, byte> TwoLiveStatuses { get; } = new()
    {
        { (byte)NoteStatus.Draft, (byte)NoteStatus.Draft },
        { (byte)NoteStatus.Draft, (byte)NoteStatus.Submitted },
        { (byte)NoteStatus.Submitted, (byte)NoteStatus.Draft },
        { (byte)NoteStatus.Submitted, (byte)NoteStatus.Submitted },
    };

    // Each row has the columns of its status's arm (design.md §5.3) but one, which breaks it: the status, the version
    // number, and whether the first submitted time, the discarded time and the discarder are set. The last has a status
    // that no arm allows.
    public static TheoryData<byte, int, bool, bool, bool> RowsBreakingTheCheckConstraint { get; } = new()
    {
        { 0, 1, false, false, false },
        { 0, 0, true, false, false },
        { 0, 0, false, true, false },
        { 1, 0, true, false, false },
        { 1, 1, false, false, false },
        { 1, 1, true, true, false },
        { 2, 1, false, true, true },
        { 2, 0, false, false, true },
        { 2, 0, false, true, false },
        { 3, 0, false, false, false },
    };

    public static TheoryData<string> FixedColumns { get; } =
        new(nameof(Note.ParticipantId), nameof(Note.NoteDate), nameof(Note.AuthorUserId));

    [Theory]
    [MemberData(nameof(TwoLiveStatuses))]
    public async Task A_second_live_note_for_a_participant_and_day_is_refused_by_the_unique_index_with_error_2601(
        byte first, byte second)
    {
        var participant = await factory.Services.AddParticipantAsync(A);
        var firstId = await AddNoteAsync(NewNote(participant.Id, (NoteStatus)first));

        var error = await Assert.ThrowsAsync<DbUpdateException>(() =>
            AddNoteAsync(NewNote(participant.Id, (NoteStatus)second)));

        // 2601: a duplicate key in a unique index.
        var refusal = Assert.IsType<SqlException>(error.InnerException);
        Assert.Equal(2601, refusal.Number);
        Assert.Contains("'IX_Note_OrganisationId_ParticipantId_NoteDate'", refusal.Message, StringComparison.Ordinal);
        Assert.Equal([firstId], await StoredNoteIdsAsync(participant.Id));
    }

    // Discarding frees the day (design.md §5.5 item 5), and the discarded drafts stay.
    [Fact]
    public async Task Discarded_notes_beside_a_live_one_for_the_participant_and_day_are_saved()
    {
        var participant = await factory.Services.AddParticipantAsync(A);

        Guid[] saved =
        [
            await AddNoteAsync(NewNote(participant.Id, NoteStatus.Discarded)),
            await AddNoteAsync(NewNote(participant.Id, NoteStatus.Draft)),
            await AddNoteAsync(NewNote(participant.Id, NoteStatus.Discarded)),
        ];

        Assert.Equal(saved.Order(), await StoredNoteIdsAsync(participant.Id));
    }

    [Fact]
    public async Task A_row_meeting_each_arm_of_the_check_constraint_is_saved()
    {
        var participant = await factory.Services.AddParticipantAsync(A);

        // A day each, as a draft and a submitted note are both live.
        Guid[] saved =
        [
            await AddNoteAsync(NewNote(participant.Id, NoteStatus.Draft, NoteDate)),
            await AddNoteAsync(NewNote(participant.Id, NoteStatus.Submitted, NoteDate.AddDays(1))),
            await AddNoteAsync(NewNote(participant.Id, NoteStatus.Submitted, NoteDate.AddDays(2)) with
            {
                CurrentVersionNumber = 3,
            }),
            await AddNoteAsync(NewNote(participant.Id, NoteStatus.Discarded, NoteDate)),
        ];

        Assert.Equal(saved.Order(), await StoredNoteIdsAsync(participant.Id));
    }

    [Theory]
    [MemberData(nameof(RowsBreakingTheCheckConstraint))]
    public async Task A_row_breaking_the_check_constraint_is_refused_with_error_547(
        byte status, int version, bool submitted, bool discarded, bool discarder)
    {
        var participant = await factory.Services.AddParticipantAsync(A);
        var note = NewNote(participant.Id, NoteStatus.Draft) with
        {
            Status = status,
            CurrentVersionNumber = version,
            FirstSubmittedAtUtc = submitted ? LaterUtc : null,
            DiscardedAtUtc = discarded ? LaterUtc : null,
            DiscardedByUserId = discarder ? A.WorkerId : null,
        };

        var error = await Assert.ThrowsAsync<DbUpdateException>(() => AddNoteAsync(note));

        // 547: a constraint refused the statement.
        var refusal = Assert.IsType<SqlException>(error.InnerException);
        Assert.Equal(547, refusal.Number);
        Assert.Contains("\"CK_Note_Status\"", refusal.Message, StringComparison.Ordinal);
        Assert.Empty(await StoredNoteIdsAsync(participant.Id));
    }

    // Saved, then saved again with a pick, a tick and the text changed, as every autosave rewrites them; and the JSON
    // holds the property names design.md §5.3 gives.
    [Fact]
    public async Task A_working_copy_s_groups_items_and_text_are_read_back_as_written()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var participant = await factory.Services.AddParticipantAsync(A);
        var noteId = await AddNoteAsync(NewNote(participant.Id, NoteStatus.Draft));
        Guid everyNote = Guid.NewGuid(), outing = Guid.NewGuid();
        var written = new NoteDraft
        {
            NoteId = noteId,
            EditorUserId = A.WorkerId,
            OrganisationId = A.OrganisationId,
            Groups = [new(everyNote, "Every note", true, false), new(outing, "Community outing", false, false)],
            Items =
            [
                new(NoteSection.Goal, Guid.NewGuid(), null, "Makes own breakfast", true),
                new(NoteSection.Goal, Guid.NewGuid(), null, "Catches the bus to Zoë’s — on time", false),
                new(NoteSection.CommonItem, Guid.NewGuid(), everyNote, "Medication given", false),
                new(NoteSection.CommonItem, Guid.NewGuid(), outing, "Bus fare paid", false),
            ],
            Narrative = "Jane made toast.\nShe said “good morning” to O’Brien.",
            IsFlagged = true,
            FlagReason = "Fell over at the bus stop",
            LastClientId = Guid.NewGuid(),
            LastSeq = 1,
            CreatedAtUtc = StartedAtUtc,
            UpdatedAtUtc = StartedAtUtc,
        };
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
            db.NoteDrafts.Add(written);
            using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(A.OrganisationId))
            {
                await db.SaveChangesAsync(cancellationToken);
            }
        }

        var first = await StoredDraftAsync(noteId);
        Assert.Equal(written.Groups, first.Groups);
        Assert.Equal(written.Items, first.Items);
        Assert.Equal(
            (A.OrganisationId, 0, written.Narrative, true, "Fell over at the bus stop", written.LastClientId, 1,
                StartedAtUtc, StartedAtUtc),
            (first.OrganisationId, first.BaseVersionNumber, first.Narrative, first.IsFlagged, first.FlagReason,
                first.LastClientId, first.LastSeq, first.CreatedAtUtc, first.UpdatedAtUtc));

        List<DraftGroup> picked = [written.Groups[0], written.Groups[1] with { IsPicked = true }];
        List<DraftItem> ticked = [written.Items[0] with { IsTicked = false }, .. written.Items.Skip(1)];
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
            using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(A.OrganisationId))
            {
                var draft = await db.NoteDrafts.SingleAsync(d => d.NoteId == noteId, cancellationToken);
                draft.Groups = picked;
                draft.Items = ticked;
                draft.Narrative += " Then a walk.";
                draft.LastSeq = 2;
                draft.UpdatedAtUtc = LaterUtc;
                await db.SaveChangesAsync(cancellationToken);
            }
        }

        var stored = await StoredDraftAsync(noteId);
        Assert.Equal(picked, stored.Groups);
        Assert.Equal(ticked, stored.Items);
        Assert.Equal(
            ("Jane made toast.\nShe said “good morning” to O’Brien. Then a walk.", 2, LaterUtc),
            (stored.Narrative, stored.LastSeq, stored.UpdatedAtUtc));

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
            var json = await db.Database.SqlQuery<StoredJson>($"""
                SELECT JSON_VALUE([Groups], '$[1].groupId') AS [GroupId],
                       JSON_VALUE([Groups], '$[1].name') AS [GroupName],
                       JSON_VALUE([Groups], '$[1].isEveryNote') AS [GroupIsEveryNote],
                       JSON_VALUE([Groups], '$[1].isPicked') AS [GroupIsPicked],
                       JSON_VALUE([Items], '$[2].section') AS [ItemSection],
                       JSON_VALUE([Items], '$[2].sourceId') AS [ItemSourceId],
                       JSON_VALUE([Items], '$[2].groupId') AS [ItemGroupId],
                       JSON_VALUE([Items], '$[2].text') AS [ItemText],
                       JSON_VALUE([Items], '$[2].isTicked') AS [ItemIsTicked]
                FROM [NoteDraft]
                WHERE [NoteId] = {noteId}
                """).SingleAsync(cancellationToken);
            Assert.Equal(
                new StoredJson(
                    outing.ToString(), "Community outing", "false", "true",
                    "2", ticked[2].SourceId.ToString(), everyNote.ToString(), "Medication given", "false"),
                json);
        }
    }

    [Theory]
    [MemberData(nameof(FixedColumns))]
    public async Task A_change_to_a_saved_note_s_fixed_column_is_refused_and_its_row_is_left_as_it_was(string column)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var participant = await factory.Services.AddParticipantAsync(A);
        var otherParticipant = await factory.Services.AddParticipantAsync(A);
        var noteId = await AddNoteAsync(NewNote(participant.Id, NoteStatus.Draft));
        var before = await StoredNoteAsync(noteId);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();

        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(A.OrganisationId))
        {
            // Each a value the database would take: another of A's participants, an earlier day, A's manager.
            var note = await db.Notes.SingleAsync(n => n.Id == noteId, cancellationToken);
            switch (column)
            {
                case nameof(Note.ParticipantId):
                    note.ParticipantId = otherParticipant.Id;
                    break;
                case nameof(Note.NoteDate):
                    note.NoteDate = NoteDate.AddDays(-1);
                    break;
                default:
                    note.AuthorUserId = A.ManagerId;
                    break;
            }

            // Changing the status too, which alone would be saved, leaves the save refused.
            note.Status = NoteStatus.Discarded;
            note.DiscardedAtUtc = LaterUtc;
            note.DiscardedByUserId = A.ManagerId;

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                db.SaveChangesAsync(cancellationToken));

            Assert.Equal(
                $"Nothing was saved: a Note row's {column} is set when the note is created, and is never changed.",
                ex.Message);
        }

        Assert.Equal(before, await StoredNoteAsync(noteId));
    }

    [Fact]
    public async Task A_change_to_a_saved_note_s_status_is_saved()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var participant = await factory.Services.AddParticipantAsync(A);
        var noteId = await AddNoteAsync(NewNote(participant.Id, NoteStatus.Draft));
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
            using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(A.OrganisationId))
            {
                var note = await db.Notes.SingleAsync(n => n.Id == noteId, cancellationToken);
                note.Status = NoteStatus.Discarded;
                note.DiscardedAtUtc = LaterUtc;
                note.DiscardedByUserId = A.ManagerId;
                await db.SaveChangesAsync(cancellationToken);
            }
        }

        Assert.Equal(
            new NoteRow(
                noteId, A.OrganisationId, participant.Id, NoteDate, A.WorkerId, (byte)NoteStatus.Discarded,
                StartedAtUtc, null, 0, (byte)FlagStatus.None, null, LaterUtc, A.ManagerId),
            await StoredNoteAsync(noteId));
    }

    /// <summary>
    /// A note of A's for <paramref name="participantId"/> on <paramref name="noteDate"/>, by default
    /// <see cref="NoteDate"/>, written by A's worker, with the columns that <paramref name="status"/> needs: version 1
    /// and a first submitted time once submitted, and A's worker and a time as the discarder once discarded.
    /// </summary>
    private NoteRow NewNote(Guid participantId, NoteStatus status, DateOnly? noteDate = null) => new(
        Guid.NewGuid(),
        A.OrganisationId,
        participantId,
        noteDate ?? NoteDate,
        A.WorkerId,
        (byte)status,
        StartedAtUtc,
        status == NoteStatus.Submitted ? LaterUtc : null,
        status == NoteStatus.Submitted ? 1 : 0,
        (byte)FlagStatus.None,
        null,
        status == NoteStatus.Discarded ? LaterUtc : null,
        status == NoteStatus.Discarded ? A.WorkerId : null);

    // Through the app's context under A's tenant, as the app adds a note, from a scope of its own, so that a refused
    // save leaves no tracked row behind for the next.
    private async Task<Guid> AddNoteAsync(NoteRow row)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        db.Notes.Add(new Note
        {
            Id = row.Id,
            OrganisationId = row.OrganisationId,
            ParticipantId = row.ParticipantId,
            NoteDate = row.NoteDate,
            AuthorUserId = row.AuthorUserId,
            Status = (NoteStatus)row.Status,
            CreatedAtUtc = row.CreatedAtUtc,
            FirstSubmittedAtUtc = row.FirstSubmittedAtUtc,
            CurrentVersionNumber = row.CurrentVersionNumber,
            FlagStatus = (FlagStatus)row.FlagStatus,
            FlaggedVersionNumber = row.FlaggedVersionNumber,
            DiscardedAtUtc = row.DiscardedAtUtc,
            DiscardedByUserId = row.DiscardedByUserId,
        });
        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(A.OrganisationId))
        {
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        return row.Id;
    }

    // Read in SQL from a scope of its own, so that neither the query filter nor a context's tracked rows decide what is
    // seen. SQL Server orders uniqueidentifiers by other bytes than .NET does, so the IDs are ordered here.
    private async Task<List<Guid>> StoredNoteIdsAsync(Guid participantId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        var ids = await db.Database
            .SqlQuery<Guid>($"SELECT [Id] AS [Value] FROM [Note] WHERE [ParticipantId] = {participantId}")
            .ToListAsync(TestContext.Current.CancellationToken);
        return [.. ids.Order()];
    }

    private async Task<NoteRow> StoredNoteAsync(Guid noteId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        return await db.Database.SqlQuery<NoteRow>($"""
            SELECT [Id], [OrganisationId], [ParticipantId], [NoteDate], [AuthorUserId], [Status], [CreatedAtUtc],
                   [FirstSubmittedAtUtc], [CurrentVersionNumber], [FlagStatus], [FlaggedVersionNumber],
                   [DiscardedAtUtc], [DiscardedByUserId]
            FROM [Note]
            WHERE [Id] = {noteId}
            """).SingleAsync(TestContext.Current.CancellationToken);
    }

    // Through the app's context, as the app reads a working copy, from a scope of its own.
    private async Task<NoteDraft> StoredDraftAsync(Guid noteId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(A.OrganisationId))
        {
            return await db.NoteDrafts.AsNoTracking()
                .SingleAsync(d => d.NoteId == noteId, TestContext.Current.CancellationToken);
        }
    }

    /// <summary>A <c>Note</c> row, its enums as their stored <c>tinyint</c>s.</summary>
    private sealed record NoteRow(
        Guid Id,
        Guid OrganisationId,
        Guid ParticipantId,
        DateOnly NoteDate,
        Guid AuthorUserId,
        byte Status,
        DateTime CreatedAtUtc,
        DateTime? FirstSubmittedAtUtc,
        int CurrentVersionNumber,
        byte FlagStatus,
        int? FlaggedVersionNumber,
        DateTime? DiscardedAtUtc,
        Guid? DiscardedByUserId);

    /// <summary>One group and one item of a working copy, read from its JSON by property name.</summary>
    private sealed record StoredJson(
        string? GroupId,
        string? GroupName,
        string? GroupIsEveryNote,
        string? GroupIsPicked,
        string? ItemSection,
        string? ItemSourceId,
        string? ItemGroupId,
        string? ItemText,
        string? ItemIsTicked);
}
