using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
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
}
