using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;

namespace Grow2Notes.Web.Features.Users;

/// <summary>
/// How a handler changes a user who exists (design.md §6.6, §6.7): in one transaction, the user is loaded in the
/// tenant's organisation and the version the client sent is checked against theirs, before the handler makes its
/// change, so that every change to a user from the app answers <c>404</c>, <c>428</c> and <c>412</c> alike, and in
/// that order; a change through an assistant's tool holds the version refusal back, and the change gives it unless it
/// would change nothing (mcp-server.md §5.1). Resend invite, Reset sign-in, Deactivate, Reactivate and Edit take it.
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
    public Task<UserChangeResult> ChangeAsync(
        Guid userId,
        StringValues ifMatch,
        Func<ApplicationUser, CancellationToken, Task<UserChangeResult>> change,
        CancellationToken cancellationToken) =>
        LoadAndChangeAsync(
            userId,
            (user, ct) =>
                // Identity gives every account a concurrency stamp.
                IfMatch.Check(ifMatch, user.ConcurrencyStamp!) is { } versionError
                    ? Task.FromResult(UserChangeResult.Refused(versionError))
                    : change(user, ct),
            cancellationToken);

    /// <summary>
    /// The same, for a change by an <paramref name="actor"/> of either channel: the version is checked with
    /// <see cref="IfMatch.CheckFor"/>, so the app's change is refused at once (<c>428</c> or <c>412</c>, as above), and
    /// an assistant's tool is handed the refusal to give unless its change would change nothing (mcp-server.md §5.1).
    /// </summary>
    /// <param name="actor">Who changes the user, and by which channel.</param>
    /// <param name="userId">The user to change.</param>
    /// <param name="ifMatch">
    /// The request's <c>If-Match</c> values, or the tool's <c>expectedVersion</c> through
    /// <see cref="IfMatch.FromExpectedVersion"/>.
    /// </param>
    /// <param name="change">
    /// As above, with the version refusal that the channel holds back as its second argument, or
    /// <see langword="null"/> when the version matches or the app has already refused it. It gives that refusal when it
    /// would change something.
    /// </param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>
    /// <see cref="UserChangeResult.NotFound"/> for an ID that no user in the organisation has, another organisation's
    /// user's included (§9.2); for the app, <see cref="ErrorCode.PreconditionRequired"/> or
    /// <see cref="ErrorCode.PreconditionFailed"/> with nothing changed; and otherwise what <paramref name="change"/>
    /// gives.
    /// </returns>
    public Task<UserChangeResult> ChangeAsync(
        Actor actor,
        Guid userId,
        StringValues ifMatch,
        Func<ApplicationUser, ErrorCode?, CancellationToken, Task<UserChangeResult>> change,
        CancellationToken cancellationToken) =>
        LoadAndChangeAsync(
            userId,
            (user, ct) =>
            {
                var version = IfMatch.CheckFor(actor, ifMatch, user.ConcurrencyStamp!);
                return version.RefuseNow is { } refused
                    ? Task.FromResult(UserChangeResult.Refused(refused))
                    : change(user, version.RefuseIfChanged, ct);
            },
            cancellationToken);

    // In one transaction: loads the user in the tenant's organisation, and gives them to the check and change.
    private async Task<UserChangeResult> LoadAndChangeAsync(
        Guid userId,
        Func<ApplicationUser, CancellationToken, Task<UserChangeResult>> checkAndChange,
        CancellationToken cancellationToken)
    {
        var organisationId = tenant.OrganisationId;

        // Set by each attempt, as a transient failure runs the whole work again.
        var result = UserChangeResult.NotFound;
        await db.InTransactionAsync(async ct =>
        {
            var user = await db.Users.SingleOrDefaultAsync(
                row => row.Id == userId && row.OrganisationId == organisationId, ct);
            result = user is null ? UserChangeResult.NotFound : await checkAndChange(user, ct);
        }, cancellationToken);

        return result;
    }
}
