using Grow2Notes.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace Grow2Notes.Web.Features.Users;

/// <summary>
/// The last-manager rule (design.md §4.11, §8.6, A26): an organisation always keeps an Active manager, so its only one
/// cannot be reset, deactivated, given a new address or made a worker in the app. With at least two, one can always
/// reset the other; when none can sign in, the operator's <c>admin reset-signin</c> is the way out, which skips the
/// rule.
/// </summary>
/// <remarks>
/// One per scope, as it reads through the scope's context, inside the transaction of the change it checks.
/// </remarks>
internal sealed class LastManagerRule(Grow2NotesDbContext db)
{
    /// <summary>
    /// Whether <paramref name="user"/> is their organisation's only Active manager, so that a change that ends their
    /// being one is refused with <see cref="Platform.ErrorCode.LastManager"/>. Call it in the change's transaction,
    /// after the version check and before anything is saved.
    /// </summary>
    /// <remarks>
    /// Azure SQL reads committed snapshots, so two such changes at once in one organisation, such as two managers
    /// resetting each other, could each still see the other Active and both go on, leaving none. So, for an Active
    /// manager, it first takes an update lock on the organisation's row, held to the end of the transaction, which the
    /// second change's check waits for; the second then reads the users once the first has committed. A user who is not
    /// an Active manager takes no lock: changing them cannot leave the organisation without one, and nor can a change
    /// that only adds one, such as a reactivation or setup's completion. The user is as loaded before the lock, which
    /// does not matter: if they changed since, the change's save finds it and saves nothing.
    /// </remarks>
    public async Task<bool> IsOnlyActiveManagerAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        if (user is not { Role: UserRole.Manager, Status: UserStatus.Active })
        {
            return false;
        }

        await db.Database.ExecuteSqlAsync(
            $"SELECT [Id] FROM [Organisation] WITH (UPDLOCK) WHERE [Id] = {user.OrganisationId}", cancellationToken);

        // AspNetUsers has no tenant query filter, so the query names the organisation itself (§5.9 item 5).
        return !await db.Users.AnyAsync(
            other => other.OrganisationId == user.OrganisationId
                && other.Id != user.Id
                && other.Role == UserRole.Manager
                && other.Status == UserStatus.Active,
            cancellationToken);
    }
}
