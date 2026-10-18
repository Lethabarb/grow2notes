using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests.Data;

/// <summary>
/// The <c>NoteVersion</c> and <c>NoteVersionItem</c> tables on SQL Server (design.md §5.3, §5.8): the unique indexes
/// that allow one version of each number per note and one use of a save's key per organisation, the check constraint
/// that ties each section of a version's rows to its columns, and the app's refusal to change or delete either. The
/// tests save through the app's context, connected as <c>sa</c>, to which no deny applies, so only the database's own
/// constraints and the app's check stand between each save and the rows; <see cref="RuntimeRoleTests"/> shows the
/// database's denies. Each test adds a participant of its own and, where its rows need them, a goal of theirs and an
/// archived common item in an archived group, so that no list that another test reads changes (tests/README.md).
/// <see cref="NoteVersionModelTests"/> and <see cref="NoteVersionItemModelTests"/> check the model.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class NoteVersionTableTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    : IClassFixture<Grow2NotesFactory>
{
    private static readonly DateOnly NoteDate = new(2026, 10, 4);

    // 9:14 am in Melbourne on the note's date, and 4:42 pm.
    private static readonly DateTime StartedAtUtc = new(2026, 10, 3, 22, 14, 0, DateTimeKind.Utc);

    private static readonly DateTime SubmittedAtUtc = new(2026, 10, 4, 5, 42, 0, DateTimeKind.Utc);

    private SeededOrganisation A => sqlServer.Seeded.A;

    private SeededOrganisation B => sqlServer.Seeded.B;

    // Each row has the columns of its section's arm (design.md §5.3) but one, which breaks it: the section, and whether
    // the goal, the common item and the group's place are set. The last has a section that no arm allows.
    public static TheoryData<byte, bool, bool, bool> RowsBreakingTheCheckConstraint { get; } = new()
    {
        { 1, false, false, false },
        { 1, true, true, false },
        { 1, true, false, true },
        { 2, false, false, true },
        { 2, true, true, true },
        { 2, false, true, false },
        { 3, true, false, false },
    };

    public static TheoryData<string, string> ChangesToSavedRows { get; } = new()
    {
        { nameof(NoteVersion), "changed" },
        { nameof(NoteVersion), "deleted" },
        { nameof(NoteVersionItem), "changed" },
        { nameof(NoteVersionItem), "deleted" },
    };

    // The full Guided notes text, longer than an nvarchar(n) can hold, with a reason of the full length; and a goal row
    // and a common item row, each meeting its arm of the check constraint.
    [Fact]
    public async Task A_version_and_a_row_meeting_each_arm_of_the_check_constraint_are_saved_as_written()
    {
        var narrative = string.Concat(Enumerable.Repeat("Zoë walked to the shops. ", 800));
        var reason = string.Concat(Enumerable.Repeat("Hurt knee.", 20));
        Assert.Equal((Limits.GuidedNotes, Limits.FlagReason), (narrative.Length, reason.Length));
        var sources = await AddSourcesAsync();
        var version = NewVersion(A, sources.NoteId);
        version.Narrative = narrative;
        version.IsFlagged = true;
        version.FlagReason = reason;

        await AddAsync(A, [version, GoalRow(version.Id, sources), CommonItemRow(version.Id, sources)]);

        Assert.Equal(
            [
                new VersionRow(
                    version.Id, A.OrganisationId, sources.NoteId, 1, narrative, true, reason, A.WorkerId,
                    SubmittedAtUtc, version.IdempotencyKey),
            ],
            await StoredVersionsAsync(sources.NoteId));
        Assert.Equal(
            [
                new ItemRow(
                    version.Id, (byte)NoteSection.Goal, 0, A.OrganisationId, sources.GoalId, null, null,
                    "Makes own breakfast", true),
                new ItemRow(
                    version.Id, (byte)NoteSection.CommonItem, 0, A.OrganisationId, null, sources.CommonItemId, 0,
                    "Medication given", false),
            ],
            await StoredItemsAsync(version.Id));
    }

    [Fact]
    public async Task A_second_version_1_of_a_note_is_refused_by_the_unique_index_with_error_2601()
    {
        var participant = await factory.Services.AddParticipantAsync(A);
        var noteId = await AddSubmittedNoteAsync(A, participant.Id, NoteDate);
        var first = NewVersion(A, noteId);
        await AddAsync(A, [first]);

        var error = await Assert.ThrowsAsync<DbUpdateException>(() => AddAsync(A, [NewVersion(A, noteId)]));

        // 2601: a duplicate key in a unique index.
        var refusal = Assert.IsType<SqlException>(error.InnerException);
        Assert.Equal(2601, refusal.Number);
        Assert.Contains("'IX_NoteVersion_NoteId_VersionNumber'", refusal.Message, StringComparison.Ordinal);
        Assert.Equal([first.Id], (await StoredVersionsAsync(noteId)).Select(row => row.Id));
    }

    // On another of the organisation's notes, as a retried save that reached the wrong note would be.
    [Fact]
    public async Task A_second_use_of_a_key_in_one_organisation_is_refused_by_the_unique_index_with_error_2601()
    {
        var participant = await factory.Services.AddParticipantAsync(A);
        var firstNoteId = await AddSubmittedNoteAsync(A, participant.Id, NoteDate);
        var secondNoteId = await AddSubmittedNoteAsync(A, participant.Id, NoteDate.AddDays(1));
        var first = NewVersion(A, firstNoteId);
        await AddAsync(A, [first]);

        var error = await Assert.ThrowsAsync<DbUpdateException>(() =>
            AddAsync(A, [NewVersion(A, secondNoteId, first.IdempotencyKey)]));

        var refusal = Assert.IsType<SqlException>(error.InnerException);
        Assert.Equal(2601, refusal.Number);
        Assert.Contains("'IX_NoteVersion_OrganisationId_IdempotencyKey'", refusal.Message, StringComparison.Ordinal);
        Assert.Equal([first.Id], (await StoredVersionsAsync(firstNoteId)).Select(row => row.Id));
        Assert.Empty(await StoredVersionsAsync(secondNoteId));
    }

    // A key is unique within its organisation alone, as the save looks for it under the tenant (design.md §5.3).
    [Fact]
    public async Task The_same_key_in_another_organisation_is_saved()
    {
        var participantOfA = await factory.Services.AddParticipantAsync(A);
        var participantOfB = await factory.Services.AddParticipantAsync(B);
        var noteOfA = await AddSubmittedNoteAsync(A, participantOfA.Id, NoteDate);
        var noteOfB = await AddSubmittedNoteAsync(B, participantOfB.Id, NoteDate);
        var key = Guid.NewGuid();
        var versionOfA = NewVersion(A, noteOfA, key);
        var versionOfB = NewVersion(B, noteOfB, key);

        await AddAsync(A, [versionOfA]);
        await AddAsync(B, [versionOfB]);

        var storedOfA = Assert.Single(await StoredVersionsAsync(noteOfA));
        var storedOfB = Assert.Single(await StoredVersionsAsync(noteOfB));
        Assert.Equal((versionOfA.Id, key), (storedOfA.Id, storedOfA.IdempotencyKey));
        Assert.Equal((versionOfB.Id, key), (storedOfB.Id, storedOfB.IdempotencyKey));
    }

    [Theory]
    [MemberData(nameof(RowsBreakingTheCheckConstraint))]
    public async Task A_row_breaking_the_check_constraint_is_refused_with_error_547(
        byte section, bool goal, bool commonItem, bool groupPosition)
    {
        var sources = await AddSourcesAsync();
        var version = NewVersion(A, sources.NoteId);
        await AddAsync(A, [version]);
        var row = new NoteVersionItem
        {
            NoteVersionId = version.Id,
            Section = (NoteSection)section,
            Position = 0,
            OrganisationId = A.OrganisationId,
            GoalId = goal ? sources.GoalId : null,
            CommonItemId = commonItem ? sources.CommonItemId : null,
            GroupPosition = groupPosition ? 0 : null,
            Text = "Makes own breakfast",
        };

        var error = await Assert.ThrowsAsync<DbUpdateException>(() => AddAsync(A, [row]));

        // 547: a constraint refused the statement.
        var refusal = Assert.IsType<SqlException>(error.InnerException);
        Assert.Equal(547, refusal.Number);
        Assert.Contains("\"CK_NoteVersionItem_Section\"", refusal.Message, StringComparison.Ordinal);
        Assert.Empty(await StoredItemsAsync(version.Id));
    }

    [Theory]
    [MemberData(nameof(ChangesToSavedRows))]
    public async Task A_saved_row_that_is_changed_or_deleted_is_refused_and_both_tables_are_left_as_they_were(
        string table, string verb)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var sources = await AddSourcesAsync();
        var version = NewVersion(A, sources.NoteId);
        await AddAsync(A, [version, GoalRow(version.Id, sources)]);
        var versionsBefore = await StoredVersionsAsync(sources.NoteId);
        var itemsBefore = await StoredItemsAsync(version.Id);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();

        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(A.OrganisationId))
        {
            object row = table == nameof(NoteVersion)
                ? await db.NoteVersions.SingleAsync(v => v.Id == version.Id, cancellationToken)
                : await db.NoteVersionItems.SingleAsync(i => i.NoteVersionId == version.Id, cancellationToken);
            switch (row, verb)
            {
                case (_, "deleted"):
                    db.Remove(row);
                    break;
                case (NoteVersion saved, _):
                    saved.Narrative = "Changed";
                    break;
                case (NoteVersionItem saved, _):
                    saved.Text = "Changed";
                    break;
            }

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                db.SaveChangesAsync(cancellationToken));

            Assert.StartsWith(
                $"Nothing was saved: {table} row to be {verb} is append-only.", ex.Message, StringComparison.Ordinal);
        }

        Assert.Equal(versionsBefore, await StoredVersionsAsync(sources.NoteId));
        Assert.Equal(itemsBefore, await StoredItemsAsync(version.Id));
    }

    /// <summary>
    /// Adds a participant of A's own, a goal of theirs, an archived group of A's with an archived common item, and a
    /// submitted note for the participant on <see cref="NoteDate"/>, the rows a version's rows point at.
    /// </summary>
    private async Task<Sources> AddSourcesAsync()
    {
        var participant = await factory.Services.AddParticipantAsync(A);
        var goalId = Assert.Single(
            await factory.Services.AddGoalsAsync(A, participant.Id, new TestGoal("Makes own breakfast", 0)));
        var groupId = Assert.Single(await factory.Services.AddGroupsAsync(
            A.OrganisationId, new TestCommonItemGroup("Morning routine", 0, ArchivedAtUtc: SubmittedAtUtc)));
        var commonItemId = Assert.Single(await factory.Services.AddItemsAsync(
            A.OrganisationId, groupId, new TestCommonItem("Medication given", 0, ArchivedAtUtc: SubmittedAtUtc)));
        var noteId = await AddSubmittedNoteAsync(A, participant.Id, NoteDate);
        return new(noteId, goalId, commonItemId);
    }

    /// <summary>
    /// Adds a note of <paramref name="organisation"/>'s for <paramref name="participantId"/> on
    /// <paramref name="noteDate"/>, written by its worker and submitted, as version 1 leaves it.
    /// </summary>
    private async Task<Guid> AddSubmittedNoteAsync(
        SeededOrganisation organisation, Guid participantId, DateOnly noteDate)
    {
        var note = new Note
        {
            OrganisationId = organisation.OrganisationId,
            ParticipantId = participantId,
            NoteDate = noteDate,
            AuthorUserId = organisation.WorkerId,
            Status = NoteStatus.Submitted,
            CreatedAtUtc = StartedAtUtc,
            FirstSubmittedAtUtc = SubmittedAtUtc,
            CurrentVersionNumber = 1,
        };
        await AddAsync(organisation, [note]);
        return note.Id;
    }

    /// <summary>
    /// Version 1 of <paramref name="noteId"/>, by <paramref name="organisation"/>'s worker, saved with
    /// <paramref name="idempotencyKey"/>, by default a new one. Its ID is given here, so that its rows can name it
    /// before it is added.
    /// </summary>
    private static NoteVersion NewVersion(SeededOrganisation organisation, Guid noteId, Guid? idempotencyKey = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisation.OrganisationId,
            NoteId = noteId,
            VersionNumber = 1,
            Narrative = "Jane made toast.",
            CreatedByUserId = organisation.WorkerId,
            CreatedAtUtc = SubmittedAtUtc,
            IdempotencyKey = idempotencyKey ?? Guid.NewGuid(),
        };

    private NoteVersionItem GoalRow(Guid versionId, Sources sources) => new()
    {
        NoteVersionId = versionId,
        Section = NoteSection.Goal,
        Position = 0,
        OrganisationId = A.OrganisationId,
        GoalId = sources.GoalId,
        Text = "Makes own breakfast",
        IsTicked = true,
    };

    private NoteVersionItem CommonItemRow(Guid versionId, Sources sources) => new()
    {
        NoteVersionId = versionId,
        Section = NoteSection.CommonItem,
        Position = 0,
        OrganisationId = A.OrganisationId,
        CommonItemId = sources.CommonItemId,
        GroupPosition = 0,
        Text = "Medication given",
    };

    // Through the app's context under the organisation's tenant, as the app adds rows, from a scope of its own, so that
    // a refused save leaves no tracked row behind for the next.
    private async Task AddAsync(SeededOrganisation organisation, object[] rows)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        db.AddRange(rows);
        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(organisation.OrganisationId))
        {
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
    }

    // Read in SQL from a scope of its own, so that neither the query filter nor a context's tracked rows decide what is
    // seen.
    private async Task<List<VersionRow>> StoredVersionsAsync(Guid noteId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        return await db.Database.SqlQuery<VersionRow>($"""
            SELECT [Id], [OrganisationId], [NoteId], [VersionNumber], [Narrative], [IsFlagged], [FlagReason],
                   [CreatedByUserId], [CreatedAtUtc], [IdempotencyKey]
            FROM [NoteVersion]
            WHERE [NoteId] = {noteId}
            ORDER BY [VersionNumber]
            """).ToListAsync(TestContext.Current.CancellationToken);
    }

    private async Task<List<ItemRow>> StoredItemsAsync(Guid versionId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        return await db.Database.SqlQuery<ItemRow>($"""
            SELECT [NoteVersionId], [Section], [Position], [OrganisationId], [GoalId], [CommonItemId], [GroupPosition],
                   [Text], [IsTicked]
            FROM [NoteVersionItem]
            WHERE [NoteVersionId] = {versionId}
            ORDER BY [Section], [Position]
            """).ToListAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>The note that a test's version is of, and the goal and common item that its rows point at.</summary>
    private sealed record Sources(Guid NoteId, Guid GoalId, Guid CommonItemId);

    /// <summary>A <c>NoteVersion</c> row.</summary>
    private sealed record VersionRow(
        Guid Id,
        Guid OrganisationId,
        Guid NoteId,
        int VersionNumber,
        string Narrative,
        bool IsFlagged,
        string? FlagReason,
        Guid CreatedByUserId,
        DateTime CreatedAtUtc,
        Guid IdempotencyKey);

    /// <summary>A <c>NoteVersionItem</c> row, its section as its stored <c>tinyint</c>.</summary>
    private sealed record ItemRow(
        Guid NoteVersionId,
        byte Section,
        int Position,
        Guid OrganisationId,
        Guid? GoalId,
        Guid? CommonItemId,
        int? GroupPosition,
        string Text,
        bool IsTicked);
}
