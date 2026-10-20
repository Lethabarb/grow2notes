using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests.Fixtures;

/// <summary>
/// A draft that a test adds for a participant of its own: the participant, the note date, the author and when they
/// started it; then its author's working copy, with its snapshot's rows (none by default), its text, when it was last
/// saved, by default when it was started, and its flag and reason, unflagged by default.
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
    string? FlagReason = null);

/// <summary>
/// A submitted note that a test adds for a participant of its own: the participant, the note date, the author, when
/// they started it and when they submitted it; then its version 1's text, flag and reason; the note's flag status, as
/// Submit leaves it (design.md §5.8's flag rules), To review when version 1 is flagged and None when it is not, unless
/// the test sets one by hand, To review or Reviewed only over a flagged version 1; and version 1's goal rows, none by
/// default, in their snapshot's order.
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
    IReadOnlyList<TestVersionGoal>? Goals = null);

/// <summary>
/// A goal row of a <see cref="TestSubmittedNote"/>'s version 1: the ID of a goal of the participant's, its wording as
/// the version holds it, which need not be the goal's own, and whether it is ticked.
/// </summary>
internal sealed record TestVersionGoal(Guid GoalId, string Text, bool IsTicked);

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
    /// <see langword="null"/>; and the header moved on to it, its flagged version
    /// <paramref name="flaggedVersionNumber"/> and its flag status left as it was.
    /// </summary>
    /// <remarks>
    /// The test gives the flagged version, as §5.8's flag rules set it or, to show that a reason is read from that
    /// version alone, against them.
    /// </remarks>
    public static async Task AddVersion2Async(
        this IServiceProvider services,
        SeededOrganisation organisation,
        Guid noteId,
        DateTime savedAtUtc,
        string? flagReason,
        int flaggedVersionNumber)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(organisation.OrganisationId))
        {
            var note = await db.Notes.SingleAsync(n => n.Id == noteId, cancellationToken);
            note.CurrentVersionNumber = 2;
            note.FlaggedVersionNumber = flaggedVersionNumber;
            db.NoteVersions.Add(new NoteVersion
            {
                OrganisationId = organisation.OrganisationId,
                NoteId = noteId,
                VersionNumber = 2,
                Narrative = "Jane made toast. Then a walk to the shops.",
                IsFlagged = flagReason is not null,
                FlagReason = flagReason,
                CreatedByUserId = organisation.ManagerId,
                CreatedAtUtc = savedAtUtc,
                IdempotencyKey = Guid.NewGuid(),
            });
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Adds <paramref name="editorUserId"/>'s pending edit to the note of ID <paramref name="noteId"/> in
    /// <paramref name="organisation"/>, a submitted note that <see cref="AddSubmittedAsync"/> added, through the app's
    /// context under its tenant, as T14's first save of an edit will leave one (design.md §5.3, §5.6): a working copy
    /// on version 1, its base, with no snapshot rows and made-up text, from a made-up page, started and last saved at
    /// <paramref name="savedAtUtc"/>.
    /// </summary>
    public static async Task AddPendingEditAsync(
        this IServiceProvider services,
        SeededOrganisation organisation,
        Guid noteId,
        Guid editorUserId,
        DateTime savedAtUtc)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        db.NoteDrafts.Add(new NoteDraft
        {
            NoteId = noteId,
            EditorUserId = editorUserId,
            OrganisationId = organisation.OrganisationId,
            BaseVersionNumber = 1,
            Narrative = "Jane made toast.",
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
