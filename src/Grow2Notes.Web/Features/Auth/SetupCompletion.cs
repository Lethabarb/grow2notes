using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Grow2Notes.Web.Platform.Audit;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Grow2Notes.Web.Features.Auth;

/// <summary>
/// Completion (design.md §8.1 step 5), which both ways to finish setup share once each has checked its method:
/// <see cref="SetupPasskeyEndpoint"/> and <see cref="SetupPasswordEndpoint"/>. In one transaction, the method is added,
/// and the user is made Active, with <c>ActivatedAtUtc</c> and <c>EmailConfirmed</c>, the security stamp rotated, which
/// ends the setup link, and <c>auth.setup_completed</c> written; then the user is signed in, and their
/// <see cref="Me"/> is the answer.
/// </summary>
/// <remarks>
/// The transaction is in the user's organisation, the tenant from the user that the enrolment cookie names, never from
/// the request (§5.9 item 1). The sign-in comes after the commit, so the session cookie holds the rotated security
/// stamp, without which the next request's stamp check would end the session at once, and a retried transaction signs
/// in once. One per scope, so the method's step, which saves through the scope's <see cref="UserManager{TUser}"/>, and
/// the audit writer join the transaction opened on the scope's context.
/// </remarks>
internal sealed class SetupCompletion(
    SignInManager<ApplicationUser> signInManager,
    Grow2NotesDbContext db,
    ITenantContext tenant,
    IAuditWriter audit,
    TimeProvider timeProvider,
    MelbourneClock clock)
{
    /// <summary>
    /// Completes setup for <paramref name="invited"/>, the user that the enrolment check found, with
    /// <paramref name="addMethodAsync"/>, which adds the method, checked before the transaction, to the user loaded in
    /// it, and throws for any refusal (<see cref="ThrowIfRefused"/>). Returns the user's <see cref="Me"/>, or
    /// <see langword="null"/> for the <c>401</c> that the enrolment check gives, if the user is no longer Invited with
    /// the cookie's stamp.
    /// </summary>
    public async Task<Me?> CompleteAsync(
        ApplicationUser invited,
        Func<ApplicationUser, Task> addMethodAsync,
        CancellationToken cancellationToken)
    {
        var users = signInManager.UserManager;
        using (tenant.Use(invited.OrganisationId))
        {
            // Each attempt loads the user again, as InTransactionAsync clears the change tracker first, so this ends as
            // the row that committed, with the rotated stamp. Only while still Invited with the stamp that the
            // enrolment check matched: another device's completion, a deactivation, or a new link from a resend or a
            // reset (A53) may have committed since that check. One that commits after this load makes the save's
            // concurrency check refuse it instead, and the method's step throws.
            ApplicationUser? user = null;
            await db.InTransactionAsync(async ct =>
            {
                user = await db.Users.SingleOrDefaultAsync(
                    row => row.Id == invited.Id
                        && row.Status == UserStatus.Invited
                        && row.SecurityStamp == invited.SecurityStamp,
                    ct);
                if (user is null)
                {
                    return;
                }

                await addMethodAsync(user);
                user.Status = UserStatus.Active;
                user.ActivatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
                user.EmailConfirmed = true;

                // Saves the changes above with the new stamp, which ends the setup link (§8.1 step 5).
                ThrowIfRefused(await users.UpdateSecurityStampAsync(user));
                await audit.WriteAsync(
                    user.OrganisationId, user.Id, AuditEventTypes.Auth.SetupCompleted, AuditEntityTypes.User, user.Id,
                    participantId: null, details: null, ct);
            }, cancellationToken);

            if (user is null)
            {
                return null;
            }

            await signInManager.SignInAsync(user, isPersistent: false);
            return await MeEndpoint.OfUserAsync(user, db, clock, cancellationToken);
        }
    }

    /// <summary>
    /// Throws for a refusal that only a fault, or another request changing the user at the same moment, can cause, so
    /// the transaction rolls back and nothing of the completion commits.
    /// </summary>
    public static void ThrowIfRefused(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Identity refused to complete setup: {string.Join(" ", result.Errors.Select(error => error.Code))}.");
        }
    }
}
