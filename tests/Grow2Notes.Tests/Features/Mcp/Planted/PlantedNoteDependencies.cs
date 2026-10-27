using Grow2Notes.Web.Data;
using Grow2Notes.Web.Features.Today;
using Microsoft.EntityFrameworkCore;

namespace Grow2Notes.Tests.Features.Mcp.Planted;

// Code that the no-note-content rule must refuse (NoNoteContentRulesTests): each class is a violation of its own, one
// way in. They are compiled and never called, so that the rule is shown to see what it is there to stop.

/// <summary>A field of a note type.</summary>
internal static class PlantedNoteField
{
    internal static readonly Note? Held = null;
}

/// <summary>A parameter of a note type.</summary>
internal static class PlantedNoteReviewParameter
{
    internal static void Take(NoteReview review) => ArgumentNullException.ThrowIfNull(review);
}

/// <summary>A reference to a type of the Today feature.</summary>
internal static class PlantedTodayReference
{
    internal static Type Name() => typeof(TodayList);
}

/// <summary>A query of the notes table.</summary>
internal static class PlantedNotesQuery
{
    internal static async Task<int> CountAsync(Grow2NotesDbContext db) => await db.Notes.CountAsync();
}

/// <summary>A projection of the comment of a review.</summary>
internal static class PlantedReviewCommentQuery
{
    internal static async Task<List<string?>> CommentsAsync(Grow2NotesDbContext db) =>
        await db.NoteReviews.Select(review => review.Comment).ToListAsync();
}

/// <summary>A query of a note type through <c>Set&lt;T&gt;()</c>.</summary>
internal static class PlantedDraftSetQuery
{
    internal static async Task<bool> AnyAsync(Grow2NotesDbContext db) => await db.Set<NoteDraft>().AnyAsync();
}

/// <summary>A query of the audit table.</summary>
internal static class PlantedAuditQuery
{
    internal static async Task<int> CountAsync(Grow2NotesDbContext db) => await db.AuditEvents.CountAsync();
}

/// <summary>A query of the audit table through <c>Set&lt;T&gt;()</c>.</summary>
internal static class PlantedAuditSetQuery
{
    internal static async Task<List<AuditEvent>> ListAsync(Grow2NotesDbContext db) =>
        await db.Set<AuditEvent>().ToListAsync();
}

/// <summary>A projection of the details of audit events.</summary>
internal static class PlantedAuditDetailsQuery
{
    internal static async Task<List<string?>> DetailsAsync(Grow2NotesDbContext db) =>
        await db.AuditEvents.Select(auditEvent => auditEvent.Details).ToListAsync();
}
