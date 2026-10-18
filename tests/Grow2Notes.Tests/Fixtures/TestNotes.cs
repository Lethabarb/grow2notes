using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests.Fixtures;

/// <summary>
/// A draft that a test adds for a participant of its own: the participant, the note date, the author and when they
/// started it; then its author's working copy, with its snapshot's rows (none by default), its text, and when it was
/// last saved, by default when it was started.
/// </summary>
internal sealed record TestDraft(
    Guid ParticipantId,
    DateOnly NoteDate,
    Guid AuthorUserId,
    DateTime StartedAtUtc,
    IReadOnlyList<DraftItem>? Items = null,
    string Narrative = "",
    DateTime? SavedAtUtc = null);

/// <summary>
/// A submitted note that a test adds for a participant of its own: the participant, the note date, the author, when
/// they started it and when they submitted it; then its version 1's text, flag and reason; and the note's flag status,
/// None unless the test sets another by hand, as Submit leaves it until T11 (tasks.md, T09's Notes).
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
    FlagStatus FlagStatus = FlagStatus.None);

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
    /// tenant, as Submit leaves a note (design.md §5.8): the <c>Note</c>, submitted, and its version 1, written by its
    /// author, with a key of its own and no item rows, its working copy gone; and gives the note's ID.
    /// </summary>
    public static async Task<Guid> AddSubmittedAsync(
        this IServiceProvider services, SeededOrganisation organisation, TestSubmittedNote submitted)
    {
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
            FlagStatus = submitted.FlagStatus,
        };
        db.Notes.Add(note);
        db.NoteVersions.Add(new NoteVersion
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
        });
        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(organisation.OrganisationId))
        {
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        return note.Id;
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
