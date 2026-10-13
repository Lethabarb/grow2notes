using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Microsoft.EntityFrameworkCore;

namespace Grow2Notes.Web.Features.Users;

/// <summary>
/// The handler that lists an organisation's users (design.md §6.6), as a handler class that takes the acting user and
/// the channel (mcp-server.md §3.5, convention 1): <c>GET /api/admin/users</c> calls it with
/// <see cref="Channel.App"/>, and Release 2's <c>list_users</c> tool will call it with <see cref="Channel.Mcp"/>.
/// </summary>
internal sealed class UserList(Grow2NotesDbContext db)
{
    /// <summary>
    /// Every user of the acting user's organisation, Invited, Active and Deactivated, as <see cref="AdminUser"/>s, in
    /// no particular order: the Users screen sorts them by name itself, with a collator that SQL Server's collation
    /// would not match. The list is whole, as design.md §6.1 returns lists.
    /// </summary>
    /// <remarks>
    /// <c>AspNetUsers</c> has no tenant query filter, so the query names the organisation itself (design.md §5.9 item
    /// 5). Neither the user's ID nor the channel changes a read, and nothing here uses them: reading writes no audit
    /// event in the app (§5.4, A28), and Release 2's <c>mcp.read</c> is written by the MCP server's call filter, not
    /// the handler (mcp-server.md §9). They are taken so that every admin handler has the same shape.
    /// </remarks>
    public async Task<IReadOnlyList<AdminUser>> ListAsync(
        ActingUser actingUser, Channel channel, CancellationToken cancellationToken) =>
        await db.Users
            .Where(user => user.OrganisationId == actingUser.OrganisationId)
            // Identity gives every account an email, which RequireUniqueEmail requires, and a concurrency stamp.
            .Select(user => new AdminUser(
                user.Id,
                user.Email!,
                user.DisplayName,
                user.Role.ToString(),
                user.Status.ToString(),
                user.InvitedAtUtc,
                user.ActivatedAtUtc,
                user.DeactivatedAtUtc,
                user.ConcurrencyStamp!))
            .ToListAsync(cancellationToken);
}
