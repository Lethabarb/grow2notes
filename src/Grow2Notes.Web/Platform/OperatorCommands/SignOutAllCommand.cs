using Grow2Notes.Web.Data;
using Grow2Notes.Web.Features.Mcp.OAuth;
using Grow2Notes.Web.Platform.Audit;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;

namespace Grow2Notes.Web.Platform.OperatorCommands;

/// <summary>
/// <c>admin signout-all</c> (design.md §7.4, §9.10 <i>Contain</i>): ends every session of every user, in every
/// organisation, at once, to contain a breach. It gives each user a new security stamp, which the session check finds
/// changed on each session's next request (§8.4). Setup links hold the stamp too (§5.3), so it also ends every link not
/// yet used, whose invites a manager then resends. It also revokes each user's assistant authorisations, which ends
/// every assistant connection on its next call. It removes no way of signing in, so everyone then signs in as
/// before. It prints nothing, the exit code saying that it ran, and logs nothing.
/// </summary>
internal sealed class SignOutAllCommand(
    Grow2NotesDbContext db,
    UserManager<ApplicationUser> users,
    IOpenIddictAuthorizationManager authorizations,
    IAuditWriter audit,
    ITenantContext tenant)
{
    /// <summary>The command's name, after <c>admin</c>.</summary>
    public const string Name = "signout-all";

    /// <summary>How the command is used, as the operator types it.</summary>
    public const string Usage = "Usage: admin signout-all";

    /// <summary>
    /// Refuses any option in <paramref name="options"/>, saying why on <paramref name="error"/>, having written
    /// nothing; otherwise, in one transaction, gives every user in every organisation a new security stamp and revokes
    /// their assistant authorisations, and writes one <c>admin.signout_all</c> about each organisation, with no actor,
    /// so that each organisation's audit extract shows it (A28).
    /// </summary>
    /// <returns><see cref="AdminCommands.Done"/>, or <see cref="AdminCommands.Refused"/>.</returns>
    /// <exception cref="InvalidOperationException">
    /// Identity refused a user's new stamp, which only a change to them since the command loaded them can cause, such
    /// as a failed sign-in counted meanwhile; the transaction rolls back, and the operator runs the command again
    /// (ops/break-glass.md).
    /// </exception>
    public async Task<int> RunAsync(
        IReadOnlyList<string> options, TextWriter error, CancellationToken cancellationToken)
    {
        if (options is [var option, ..])
        {
            return await AdminCommands.RefuseAsync(
                error, $"\"{option}\" is not an option of admin signout-all.", Usage);
        }

        await db.InTransactionAsync(async ct =>
        {
            foreach (var organisationId in await db.Organisations.Select(row => row.Id).ToListAsync(ct))
            {
                // AspNetUsers has no tenant query filter, so the query names the organisation itself (§5.9 item 5).
                foreach (var user in await db.Users.Where(row => row.OrganisationId == organisationId).ToListAsync(ct))
                {
                    var rotated = await users.UpdateSecurityStampAsync(user);
                    if (!rotated.Succeeded)
                    {
                        throw new InvalidOperationException(
                            "Identity refused a user's new security stamp: " +
                            $"{string.Join(" ", rotated.Errors.Select(refusal => refusal.Code))}.");
                    }

                    await authorizations.RevokeConnectionsAsync(user.Id, ct);
                }

                using (tenant.Use(organisationId))
                {
                    await audit.WriteAsync(
                        organisationId, actorUserId: null, AuditEventTypes.Admin.SignOutAll,
                        AuditEntityTypes.Organisation, organisationId, participantId: null, details: null, ct);
                }
            }
        }, cancellationToken);

        return AdminCommands.Done;
    }
}
