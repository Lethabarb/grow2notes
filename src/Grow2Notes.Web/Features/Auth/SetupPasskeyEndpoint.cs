using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Grow2Notes.Web.Platform.Audit;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Grow2Notes.Web.Features.Auth;

/// <summary>
/// <c>POST /api/auth/setup/passkey</c> (design.md §6.2, §8.1 steps 4 and 5): finishes setup with the passkey that the
/// browser made from <c>POST /api/auth/setup/passkey/options</c>' options, for the user whose
/// <see cref="EnrolmentCookie"/> the request carries, signs the user in, and answers their <see cref="Me"/>.
/// </summary>
/// <remarks>
/// It is <c>AllowAnonymous</c>, as the person has no session yet, and checks the enrolment cookie itself, answering
/// <c>401</c> without a valid one. Identity checks the credential against the ceremony that the options started, which
/// it keeps in its two-factor cookie and ends here, so each options object serves one attempt. A credential that
/// Identity refuses, one sent with no ceremony under way, and one made from another user's options get
/// <c>422 validation.failed</c> for <c>credentialJson</c> and change nothing: a <c>400</c> with no code would be taken
/// by the SPA's <c>api()</c> wrapper for a stale antiforgery token and sent again (S00.04.02's Notes). Completion is
/// one transaction, in the user's organisation, which is the tenant from the user that the cookie names, never from
/// the request (§5.9 item 1). The sign-in comes after the commit, so the session cookie holds the rotated security
/// stamp, without which the next request's stamp check would end the session at once, and a retried transaction
/// signs in once.
/// </remarks>
internal static class SetupPasskeyEndpoint
{
    // The name of every passkey that setup stores (D70), which no screen shows or asks for, whatever name the request
    // sends.
    private const string PasskeyName = "Grow2Notes";

    /// <summary>Maps the endpoint in the <c>/api</c> group, which <see cref="ApiGroup.MapApiGroup"/> returns.</summary>
    public static RouteGroupBuilder MapSetupPasskeyEndpoint(this RouteGroupBuilder api)
    {
        api.MapPost("/auth/setup/passkey", CompleteAsync).AllowAnonymous();
        return api;
    }

    private static async Task<Results<Ok<Me>, UnauthorizedHttpResult, ProblemHttpResult>> CompleteAsync(
        SetupPasskeyRequest request,
        HttpContext context,
        SignInManager<ApplicationUser> signInManager,
        Grow2NotesDbContext db,
        ITenantContext tenant,
        IAuditWriter audit,
        TimeProvider timeProvider,
        MelbourneClock clock,
        CancellationToken cancellationToken)
    {
        var users = signInManager.UserManager;

        // With no body, which the status code pages make problem details with no design.md §6.9 code.
        if (await EnrolmentCookie.FindInvitedUserAsync(context, users) is not { } invited)
        {
            return TypedResults.Unauthorized();
        }

        PasskeyAttestationResult attestation;
        try
        {
            attestation = await signInManager.PerformPasskeyAttestationAsync(request.CredentialJson);
        }
        catch (InvalidOperationException)
        {
            // Identity throws, rather than failing, when its two-factor cookie holds no passkey creation: none was
            // started in this browser, its 5 minutes have passed, an earlier attempt used it, or a sign-in replaced it.
            return PasskeyRefused();
        }

        // The user entity is the one the options were made for, which Identity kept with the ceremony; another user's,
        // when the browser started setup with a second link after asking for options with the first.
        if (!attestation.Succeeded || attestation.UserEntity.Id != invited.Id.ToString())
        {
            return PasskeyRefused();
        }

        attestation.Passkey.Name = PasskeyName;
        using (tenant.Use(invited.OrganisationId))
        {
            // Each attempt loads the user again, as InTransactionAsync clears the change tracker first, so this ends as
            // the row that committed, with the rotated stamp. Only while still Invited with the stamp that the
            // enrolment check matched: another device's completion, a deactivation, or a new link from a resend or a
            // reset (A53) may have committed since that check. One that commits after this load makes the save's
            // concurrency check refuse it instead.
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

                ThrowIfRefused(await users.AddOrUpdatePasskeyAsync(user, attestation.Passkey));
                user.Status = UserStatus.Active;
                user.ActivatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
                user.EmailConfirmed = true;

                // Saves the changes above with the new stamp, which ends the setup link (§8.1 step 5).
                ThrowIfRefused(await users.UpdateSecurityStampAsync(user));
                await audit.WriteAsync(
                    user.OrganisationId, user.Id, AuditEventTypes.Auth.SetupCompleted, AuditEntityTypes.User, user.Id,
                    participantId: null, details: null, ct);
            }, cancellationToken);

            // As the enrolment check answers.
            if (user is null)
            {
                return TypedResults.Unauthorized();
            }

            await signInManager.SignInAsync(user, isPersistent: false);
            return TypedResults.Ok(await MeEndpoint.OfUserAsync(user, db, clock, cancellationToken));
        }
    }

    // The SPA shows its own words for it (sign-in.md's Passkey not set up), not these.
    private static ProblemHttpResult PasskeyRefused() =>
        ValidationProblems.Field(nameof(SetupPasskeyRequest.CredentialJson), "The passkey was not set up.");

    // Only a fault, or another request changing the user at the same moment, makes Identity refuse these. Thrown, so
    // the transaction rolls back and nothing of the completion commits.
    private static void ThrowIfRefused(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Identity refused to complete setup: {string.Join(" ", result.Errors.Select(error => error.Code))}.");
        }
    }
}
