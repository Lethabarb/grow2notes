using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests.Fixtures;

/// <summary>
/// A draft that a test adds for a participant of its own: the participant, the note date, the author and when they
/// started it; then its author's working copy, with its snapshot's rows (none by default), its text, when it was last
/// saved, by default when it was started, and its flag and reason, unflagged by default; and its snapshot's groups,
/// none by default. The rows are the goals, then the common items, in the order of their groups.
/// </summary>
internal sealed record TestDraft(
    Guid ParticipantId,
    DateOnly NoteDate,
    Guid AuthorUserId,
    DateTime StartedAtUtc,
    IReadOnlyList<DraftItem>? Items = null,
    string Narrative = "",
    DateTime? SavedAtUtc = null,
    bool IsFlagged = false,
    string? FlagReason = null,
    IReadOnlyList<DraftGroup>? Groups = null);

/// <summary>
/// A submitted note that a test adds for a participant of its own: the participant, the note date, the author, when
/// they started it and when they submitted it; then its version 1's text, flag and reason; the note's flag status, as
/// Submit leaves it (design.md §5.8's flag rules), To review when version 1 is flagged and None when it is not, unless
/// the test sets one by hand, To review or Reviewed only over a flagged version 1; version 1's goal rows, none by
/// default, in their snapshot's order; and its groups with their common item rows, none by default, in their
/// snapshot's order.
/// </summary>
internal sealed record TestSubmittedNote(
    Guid ParticipantId,
    DateOnly NoteDate,
    Guid AuthorUserId,
    DateTime StartedAtUtc,
    DateTime SubmittedAtUtc,
    string Narrative = "Jane made toast.",
    bool IsFlagged = false,
    string? FlagReason = null,
    FlagStatus? FlagStatus = null,
    IReadOnlyList<TestVersionGoal>? Goals = null,
    IReadOnlyList<TestVersionGroup>? Groups = null);

/// <summary>
/// A goal row of a <see cref="TestSubmittedNote"/>'s version 1: the ID of a goal of the participant's, its wording as
/// the version holds it, which need not be the goal's own, and whether it is ticked.
/// </summary>
internal sealed record TestVersionGoal(Guid GoalId, string Text, bool IsTicked);

/// <summary>
/// A group row of a version, with the common item rows in it: the ID of a group of the organisation's, its name as the
/// version holds it, which need not be the group's own, whether it is Every note and whether it is picked, and its
/// items in their order. A group takes its place in the version's groups from its place in the list.
/// </summary>
internal sealed record TestVersionGroup(
    Guid GroupId, string Name, bool IsEveryNote, bool IsPicked, IReadOnlyList<TestVersionItem> Items);

/// <summary>
/// A common item row of a <see cref="TestVersionGroup"/>: the ID of an item of the organisation's, its wording as the
/// version holds it, which need not be the item's own, and whether it is ticked.
/// </summary>
internal sealed record TestVersionItem(Guid CommonItemId, string Text, bool IsTicked);

internal static class TestNotes
{
    /// <summary>
    /// Adds <paramref name="draft"/> to <paramref name="organisation"/>, through the app's context under its tenant, as
    /// a note's first save leaves it (design.md §5.5 item 2): the <c>Note</c>, a draft, and its author's working copy,
    /// whose last save came from a made-up page; and gives the note's ID.
    /// </summary>
    public static async Task<Guid> AddDraftAsync(
        this IServiceProvider services, SeededOrganisation organisation, TestDraft draft)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        var note = new Note
        {
            OrganisationId = organisation.OrganisationId,
            ParticipantId = draft.ParticipantId,
            NoteDate = draft.NoteDate,
            AuthorUserId = draft.AuthorUserId,
            Status = NoteStatus.Draft,
            CreatedAtUtc = draft.StartedAtUtc,
        };
        db.Notes.Add(note);
        db.NoteDrafts.Add(new NoteDraft
        {
            NoteId = note.Id,
            EditorUserId = draft.AuthorUserId,
            OrganisationId = organisation.OrganisationId,
            Groups = [.. draft.Groups ?? []],
            Items = [.. draft.Items ?? []],
            Narrative = draft.Narrative,
            IsFlagged = draft.IsFlagged,
            FlagReason = draft.FlagReason,
            LastClientId = Guid.NewGuid(),
            LastSeq = 1,
            CreatedAtUtc = draft.StartedAtUtc,
            UpdatedAtUtc = draft.SavedAtUtc ?? draft.StartedAtUtc,
        });
        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(organisation.OrganisationId))
        {
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        return note.Id;
    }

    /// <summary>
    /// Adds <paramref name="submitted"/> to <paramref name="organisation"/>, through the app's context under its
    /// tenant, as Submit leaves a note (design.md §5.8): the <c>Note</c>, submitted, with its flag status and, unless
    /// that is None, version 1 its flagged version, the only one it has; and its version 1, written by its author,
    /// with a key of its own and its goal rows, each in its place in the order given, its working copy gone; and gives
    /// the note's ID.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// The flag status is To review or Reviewed and version 1 is not flagged, which no save leaves, as only a flagged
    /// version sends a note to To review (§5.8's flag rules); a test of To review over an unflagged current version
    /// adds a flagged version 1 and an unflagged version 2 (<see cref="AddVersion2Async"/>).
    /// </exception>
    public static async Task<Guid> AddSubmittedAsync(
        this IServiceProvider services, SeededOrganisation organisation, TestSubmittedNote submitted)
    {
        var flagStatus = submitted.FlagStatus ?? (submitted.IsFlagged ? FlagStatus.ToReview : FlagStatus.None);
        if (flagStatus != FlagStatus.None && !submitted.IsFlagged)
        {
            throw new ArgumentException(
                $"A note in {flagStatus} has a flagged version: flag version 1, then add version 2 unflagged.",
                nameof(submitted));
        }

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        var note = new Note
        {
            OrganisationId = organisation.OrganisationId,
            ParticipantId = submitted.ParticipantId,
            NoteDate = submitted.NoteDate,
            AuthorUserId = submitted.AuthorUserId,
            Status = NoteStatus.Submitted,
            CreatedAtUtc = submitted.StartedAtUtc,
            FirstSubmittedAtUtc = submitted.SubmittedAtUtc,
            CurrentVersionNumber = 1,
            FlagStatus = flagStatus,
            FlaggedVersionNumber = flagStatus == FlagStatus.None ? null : 1,
        };
        db.Notes.Add(note);
        var version = new NoteVersion
        {
            OrganisationId = organisation.OrganisationId,
            NoteId = note.Id,
            VersionNumber = 1,
            Narrative = submitted.Narrative,
            IsFlagged = submitted.IsFlagged,
            FlagReason = submitted.FlagReason,
            CreatedByUserId = submitted.AuthorUserId,
            CreatedAtUtc = submitted.SubmittedAtUtc,
            IdempotencyKey = Guid.NewGuid(),
        };
        db.NoteVersions.Add(version);
        db.NoteVersionItems.AddRange((submitted.Goals ?? []).Select((goal, position) => new NoteVersionItem
        {
            NoteVersionId = version.Id,
            Section = NoteSection.Goal,
            Position = position,
            OrganisationId = organisation.OrganisationId,
            GoalId = goal.GoalId,
            Text = goal.Text,
            IsTicked = goal.IsTicked,
        }));
        AddGroupRows(db, organisation, version.Id, submitted.Groups ?? []);
        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(organisation.OrganisationId))
        {
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        return note.Id;
    }

    /// <summary>
    /// Adds version 2 to the note of ID <paramref name="noteId"/> in <paramref name="organisation"/>, one that
    /// <see cref="AddSubmittedAsync"/> added, by hand as Save changes will add it (design.md §5.8 steps 5 and 6),
    /// through the app's context under its tenant: written by the organisation's manager at
    /// <paramref name="savedAtUtc"/>, flagged with <paramref name="flagReason"/>, or not flagged when that is
    /// <see langword="null"/>; its <paramref name="groups"/> with their common item rows, none by default; and the
    /// header moved on to it, its flagged version <paramref name="flaggedVersionNumber"/> and its flag status left as
    /// it was.
    /// </summary>
    /// <remarks>
    /// The test gives the flagged version, as §5.8's flag rules set it or, to show that a reason is read from that
    /// version alone, against them.
    /// </remarks>
    public static Task AddVersion2Async(
        this IServiceProvider services,
        SeededOrganisation organisation,
        Guid noteId,
        DateTime savedAtUtc,
        string? flagReason,
        int flaggedVersionNumber,
        IReadOnlyList<TestVersionGroup>? groups = null) =>
        services.AddVersionAsync(
            organisation, noteId, 2, savedAtUtc, flagReason, flaggedVersionNumber, groups, organisation.ManagerId);

    /// <summary>
    /// Adds version <paramref name="versionNumber"/>, the one after the note's current version, as
    /// <see cref="AddVersion2Async"/> adds version 2, written by <paramref name="savedByUserId"/>.
    /// </summary>
    public static async Task AddVersionAsync(
        this IServiceProvider services,
        SeededOrganisation organisation,
        Guid noteId,
        int versionNumber,
        DateTime savedAtUtc,
        string? flagReason,
        int flaggedVersionNumber,
        IReadOnlyList<TestVersionGroup>? groups,
        Guid savedByUserId)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(organisation.OrganisationId))
        {
            var note = await db.Notes.SingleAsync(n => n.Id == noteId, cancellationToken);
            note.CurrentVersionNumber = versionNumber;
            note.FlaggedVersionNumber = flaggedVersionNumber;
            var version = new NoteVersion
            {
                OrganisationId = organisation.OrganisationId,
                NoteId = noteId,
                VersionNumber = versionNumber,
                Narrative = "Jane made toast. Then a walk to the shops.",
                IsFlagged = flagReason is not null,
                FlagReason = flagReason,
                CreatedByUserId = savedByUserId,
                CreatedAtUtc = savedAtUtc,
                IdempotencyKey = Guid.NewGuid(),
            };
            db.NoteVersions.Add(version);
            AddGroupRows(db, organisation, version.Id, groups ?? []);
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Adds a group row for each of <paramref name="groups"/> to version <paramref name="versionId"/>, each in the
    /// place it has in the list, and a common item row for each of their items: the row's place is its place among all
    /// the common items, by group and then item, and it names its group by that group's place, as Submit writes them
    /// (design.md §5.8).
    /// </summary>
    private static void AddGroupRows(
        Grow2NotesDbContext db,
        SeededOrganisation organisation,
        Guid versionId,
        IReadOnlyList<TestVersionGroup> groups)
    {
        db.NoteVersionGroups.AddRange(groups.Select((group, position) => new NoteVersionGroup
        {
            NoteVersionId = versionId,
            Position = position,
            OrganisationId = organisation.OrganisationId,
            CommonItemGroupId = group.GroupId,
            Name = group.Name,
            IsEveryNote = group.IsEveryNote,
            IsPicked = group.IsPicked,
        }));
        db.NoteVersionItems.AddRange(groups
            .SelectMany((group, groupPosition) => group.Items.Select(item => (groupPosition, item)))
            .Select((entry, position) => new NoteVersionItem
            {
                NoteVersionId = versionId,
                Section = NoteSection.CommonItem,
                Position = position,
                OrganisationId = organisation.OrganisationId,
                CommonItemId = entry.item.CommonItemId,
                GroupPosition = entry.groupPosition,
                Text = entry.item.Text,
                IsTicked = entry.item.IsTicked,
            }));
    }

    /// <summary>
    /// Adds <paramref name="editorUserId"/>'s pending edit to the note of ID <paramref name="noteId"/> in
    /// <paramref name="organisation"/>, a submitted note that <see cref="AddSubmittedAsync"/> added, through the app's
    /// context under its tenant, as T14's first save of an edit will leave one (design.md §5.3, §5.6): a working copy
    /// on <paramref name="baseVersionNumber"/>, its base, version 1 unless the test gives another, with no snapshot rows
    /// and made-up text, from a made-up page, started and last saved at <paramref name="savedAtUtc"/>, flagged with
    /// <paramref name="flagReason"/>, or not flagged when that is <see langword="null"/>.
    /// </summary>
    public static async Task AddPendingEditAsync(
        this IServiceProvider services,
        SeededOrganisation organisation,
        Guid noteId,
        Guid editorUserId,
        DateTime savedAtUtc,
        int baseVersionNumber = 1,
        string? flagReason = null)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        db.NoteDrafts.Add(new NoteDraft
        {
            NoteId = noteId,
            EditorUserId = editorUserId,
            OrganisationId = organisation.OrganisationId,
            BaseVersionNumber = baseVersionNumber,
            Narrative = "Jane made toast.",
            IsFlagged = flagReason is not null,
            FlagReason = flagReason,
            LastClientId = Guid.NewGuid(),
            LastSeq = 1,
            CreatedAtUtc = savedAtUtc,
            UpdatedAtUtc = savedAtUtc,
        });
        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(organisation.OrganisationId))
        {
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
    }

    /// <summary>
    /// Adds a review of version <paramref name="versionNumber"/> of the note of ID <paramref name="noteId"/> in
    /// <paramref name="organisation"/> by <paramref name="reviewedByUserId"/> at <paramref name="reviewedAtUtc"/>,
    /// through the app's context under its tenant, and gives its ID. It adds the row alone, as the Reviewed list's
    /// paging tests need many reviews at times of their own, and leaves the note's flag status as it is.
    /// </summary>
    public static async Task<Guid> AddReviewAsync(
        this IServiceProvider services,
        SeededOrganisation organisation,
        Guid noteId,
        int versionNumber,
        Guid reviewedByUserId,
        DateTime reviewedAtUtc,
        string? comment = null)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        var review = new NoteReview
        {
            OrganisationId = organisation.OrganisationId,
            NoteId = noteId,
            VersionNumber = versionNumber,
            ReviewedByUserId = reviewedByUserId,
            ReviewedAtUtc = reviewedAtUtc,
            Comment = comment,
        };
        db.NoteReviews.Add(review);
        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(organisation.OrganisationId))
        {
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        return review.Id;
    }

    /// <summary>
    /// Discards the note of ID <paramref name="noteId"/> in <paramref name="organisation"/> at
    /// <paramref name="discardedAtUtc"/>, through the app's context under its tenant, as its manager's Discard draft
    /// leaves it (design.md §5.5 item 5): the note kept, with its working copy, and the day freed.
    /// </summary>
    public static async Task DiscardAsync(
        this IServiceProvider services, SeededOrganisation organisation, Guid noteId, DateTime discardedAtUtc)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(organisation.OrganisationId))
        {
            var note = await db.Notes.SingleAsync(n => n.Id == noteId, cancellationToken);
            note.Status = NoteStatus.Discarded;
            note.DiscardedAtUtc = discardedAtUtc;
            note.DiscardedByUserId = organisation.ManagerId;
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
