using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Microsoft.EntityFrameworkCore;

namespace Grow2Notes.Web.Features.Notes;

/// <summary>
/// The caller's role as their user row stores it, which a note's handlers read where a manager may do or see more than
/// a worker: named by exact equality, never inferred from another role (mcp-server.md §3.5, convention 4).
/// </summary>
internal static class StoredRole
{
    /// <summary>Whether <paramref name="actor"/>'s stored role is Manager.</summary>
    public static Task<bool> IsManagerAsync(
        Grow2NotesDbContext db, Actor actor, CancellationToken cancellationToken) =>
        db.Users.AnyAsync(user => user.Id == actor.UserId && user.Role == UserRole.Manager, cancellationToken);
}
