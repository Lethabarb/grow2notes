using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Grow2Notes.Web.Platform.Audit;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;

namespace Grow2Notes.Web.Features.Auth;

/// <summary>
/// <c>POST /api/auth/login</c> (design.md §6.2, §8.2): the password step of a password sign-in. When Identity accepts
/// the password, it asks for the code from the person's authenticator app, so the answer is <c>{"next":"totp"}</c>,
/// and Identity's two-factor cookie holds the step until <c>POST /api/auth/login/totp</c> takes the code. Any other
/// outcome is the <c>401</c> of a failed sign-in, which starts nothing.
/// </summary>
/// <remarks>
/// Anyone may call it, as whoever asks is not signed in yet. It finds the account by its email first, in any letter
/// case, and makes that account's organisation the tenant (§5.9 item 1). Identity's <c>PasswordSignInAsync</c> then
/// checks that the user may sign in (<see cref="Grow2NotesSignInManager.CanSignInAsync"/>) and is not locked out, and
/// checks the password, counting a wrong one towards the lockout (A25). It never signs anyone in here: every password
/// user has two-factor on, and the sign-in manager refuses one who does not
/// (<see cref="Grow2NotesSignInManager"/>'s <c>SignInOrTwoFactorAsync</c>, §8.1's invariant). The code step writes the
/// audit event of a success, with the session; this step writes one only for a failure on the account, after
/// Identity's own save, with no transaction, as §5.9 lists none for a sign-in: <c>auth.locked_out</c> when Identity
/// reports a lockout, and <c>auth.signin_failed</c> otherwise. An email of no account is audited nowhere (§5.4: known
/// accounts only); it is counted in telemetry by one log entry, which holds no value of the request (§9.5), and
/// Identity is not asked about it.
/// </remarks>
internal static partial class PasswordSignInEndpoint
{
    /// <summary>Maps the endpoint in the <c>/api</c> group, which <see cref="ApiGroup.MapApiGroup"/> returns.</summary>
    public static RouteGroupBuilder MapPasswordSignInEndpoint(this RouteGroupBuilder api)
    {
        api.MapPost("/auth/login", SignInAsync).AllowAnonymous();
        return api;
    }

    private static async Task<Results<Ok<PasswordSignInResponse>, UnauthorizedHttpResult>> SignInAsync(
        PasswordSignInRequest request,
        SignInManager<ApplicationUser> signInManager,
        ITenantContext tenant,
        IAuditWriter audit,
        ILoggerFactory loggers,
        CancellationToken cancellationToken)
    {
        if (await signInManager.UserManager.FindByEmailAsync(request.Email) is not { } user)
        {
            LogUnknownEmail(loggers.CreateLogger(typeof(PasswordSignInEndpoint)));
            return TypedResults.Unauthorized();
        }

        using (tenant.Use(user.OrganisationId))
        {
            var result = await signInManager.PasswordSignInAsync(
                user, request.Password, isPersistent: false, lockoutOnFailure: true);
            if (result.RequiresTwoFactor)
            {
                return TypedResults.Ok(new PasswordSignInResponse(Next: "totp"));
            }

            await audit.WriteAsync(
                user.OrganisationId,
                actorUserId: user.Id,
                result.IsLockedOut ? AuditEventTypes.Auth.LockedOut : AuditEventTypes.Auth.SignInFailed,
                AuditEntityTypes.User,
                entityId: user.Id,
                participantId: null,
                details: null,
                cancellationToken);
            return TypedResults.Unauthorized();
        }
    }

    // Counted in Log Analytics by its message, so the message never changes and holds no value: not the email, the
    // password or the caller's address.
    [LoggerMessage(Level = LogLevel.Information, Message = "Password sign-in with an unknown email")]
    private static partial void LogUnknownEmail(ILogger logger);
}
