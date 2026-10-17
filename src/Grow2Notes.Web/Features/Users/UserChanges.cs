using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;

namespace Grow2Notes.Web.Features.Users;

/// <summary>
/// How a handler changes a user who exists (design.md §6.6, §6.7): in one transaction, the user is loaded in the
/// tenant's organisation and the version the client sent is checked against theirs, before the handler makes its
/// change, so that every change to a user answers <c>404</c>, <c>428</c> and <c>412</c> alike, and in that order.
/// Resend invite, Reset sign-in, Deactivate, Reactivate and Edit take it.
/// </summary>
/// <remarks>
/// One per scope, as the transaction is opened on the scope's context, which the handler's saves, through
/// <see cref="Microsoft.AspNetCore.Identity.UserManager{TUser}"/> and the audit writer, join. <c>AspNetUsers</c> has no
/// tenant query filter, so the query names the tenant's organisation itself (§5.9 item 5), from
/// <see cref="ITenantContext"/>.
/// </remarks>
internal sealed class UserChanges(Grow2NotesDbContext db, ITenantContext tenant)
{
    /// <summary>
    /// In one transaction: loads the user whose ID is <paramref name="userId"/> in the tenant's organisation; checks
    /// <paramref name="ifMatch"/> against their <c>ConcurrencyStamp</c> with <see cref="IfMatch.Check"/>; then gives
    /// the user to <paramref name="change"/>, and commits what it saved.
    /// </summary>
    /// <param name="userId">The user to change.</param>
    /// <param name="ifMatch">The request's <c>If-Match</c> values, as <c>Headers.IfMatch</c> gives them.</param>
    /// <param name="change">
    /// Makes the change to the user, as loaded on the scope's context, and saves it through that context. It runs
    /// again, with the user loaded again, when a transient failure runs the transaction again (§5.9). Any refusal comes
    /// before it saves anything, as what it saved commits; and it gives <see cref="ErrorCode.PreconditionFailed"/>,
    /// never a <c>500</c>, when a save finds the user changed since they were loaded, which Identity reports as
    /// <c>ConcurrencyFailure</c>, having saved nothing.
    /// </param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>
    /// <see cref="UserChangeResult.NotFound"/> for an ID that no user in the organisation has, another organisation's
    /// user's included (§9.2); <see cref="ErrorCode.PreconditionRequired"/> when <paramref name="ifMatch"/> is missing,
    /// and <see cref="ErrorCode.PreconditionFailed"/> when it is not the user's ETag, with nothing changed; and
    /// otherwise what <paramref name="change"/> gives.
    /// </returns>
    public async Task<UserChangeResult> ChangeAsync(
        Guid userId,
        StringValues ifMatch,
        Func<ApplicationUser, CancellationToken, Task<UserChangeResult>> change,
        CancellationToken cancellationToken)
    {
        var organisationId = tenant.OrganisationId;

        // Set by each attempt, as a transient failure runs the whole work again.
        var result = UserChangeResult.NotFound;
        await db.InTransactionAsync(async ct =>
        {
            var user = await db.Users.SingleOrDefaultAsync(
                row => row.Id == userId && row.OrganisationId == organisationId, ct);
            if (user is null)
            {
                result = UserChangeResult.NotFound;
                return;
            }

            // Identity gives every account a concurrency stamp.
            result = IfMatch.Check(ifMatch, user.ConcurrencyStamp!) is { } versionError
                ? UserChangeResult.Refused(versionError)
                : await change(user, ct);
        }, cancellationToken);

        return result;
    }
}
