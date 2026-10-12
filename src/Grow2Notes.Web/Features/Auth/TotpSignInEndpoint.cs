using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Grow2Notes.Web.Platform.Audit;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;

namespace Grow2Notes.Web.Features.Auth;

/// <summary>
/// <c>POST /api/auth/login/totp</c> (design.md §6.2, §8.2): the code step of a password sign-in, which signs the
/// person in with the code from their authenticator app once <c>POST /api/auth/login</c> has accepted their password,
/// and answers who they are, as a <see cref="Me"/>. Any other outcome is the <c>401</c> of a failed sign-in, which
/// starts no session.
/// </summary>
/// <remarks>
/// Anyone may call it, as whoever asks is not signed in yet. The account is the one in Identity's two-factor cookie,
/// which the password step set, read before anything else. Without one, because no password step came first, its 5
/// minutes have passed or a passkey ceremony has replaced it, the answer is the <c>401</c> with nothing written.
/// Otherwise that account's organisation is the tenant (§5.9 item 1), and the code is read by the one code check that
/// setup's code passes too (<see cref="OneTimeCode"/>): a badly formed code is a wrong one that never reaches Identity,
/// which would read "12345" as the code 012345, so it counts towards no lockout (totp-setup.md <i>Implementation
/// notes</i>). Identity's <c>TwoFactorAuthenticatorSignInAsync</c> then checks that the user may still sign in
/// (<see cref="Grow2NotesSignInManager.CanSignInAsync"/>) and is not locked out, and checks the code, counting a wrong
/// one towards the lockout with the password failures (§9.9); a right one clears the two-factor cookie and sets the
/// session cookie. One audit event records the attempt, in the passkey sign-in's shape and after Identity's own save,
/// with no transaction, so that a failed write leaves no session (<see cref="PasskeySignInEndpoint"/>):
/// <c>auth.signin_succeeded</c>; <c>auth.locked_out</c> when Identity reports a lockout; <c>auth.signin_failed</c> for
/// a user who may no longer sign in; and <c>auth.mfa_failed</c> for a wrong or badly formed code.
/// </remarks>
internal static class TotpSignInEndpoint
{
    /// <summary>Maps the endpoint in the <c>/api</c> group, which <see cref="ApiGroup.MapApiGroup"/> returns.</summary>
    public static RouteGroupBuilder MapTotpSignInEndpoint(this RouteGroupBuilder api)
    {
        api.MapPost("/auth/login/totp", SignInAsync).AllowAnonymous();
        return api;
    }

    private static async Task<Results<Ok<Me>, UnauthorizedHttpResult>> SignInAsync(
        TotpSignInRequest request,
        SignInManager<ApplicationUser> signInManager,
        ITenantContext tenant,
        IAuditWriter audit,
        Grow2NotesDbContext db,
        MelbourneClock clock,
        CancellationToken cancellationToken)
    {
        if (await signInManager.GetTwoFactorAuthenticationUserAsync() is not { } user)
        {
            return TypedResults.Unauthorized();
        }

        using (tenant.Use(user.OrganisationId))
        {
            var result = OneTimeCode.TryRead(request.Code, out var code)
                ? await signInManager.TwoFactorAuthenticatorSignInAsync(
                    code, isPersistent: false, rememberClient: false)
                : SignInResult.Failed;
            await audit.WriteAsync(
                user.OrganisationId,
                actorUserId: user.Id,
                EventTypeOf(result),
                AuditEntityTypes.User,
                entityId: user.Id,
                participantId: null,
                details: null,
                cancellationToken);
            return result.Succeeded
                ? TypedResults.Ok(await MeEndpoint.OfUserAsync(user, db, clock, cancellationToken))
                : TypedResults.Unauthorized();
        }
    }

    // Identity reports a user who may no longer sign in, Invited or Deactivated, as not allowed, and a code that it
    // refuses as failed, unless that failure locked the account.
    private static string EventTypeOf(SignInResult result) => result switch
    {
        { Succeeded: true } => AuditEventTypes.Auth.SignInSucceeded,
        { IsLockedOut: true } => AuditEventTypes.Auth.LockedOut,
        { IsNotAllowed: true } => AuditEventTypes.Auth.SignInFailed,
        _ => AuditEventTypes.Auth.MfaFailed,
    };
}
