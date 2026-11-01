using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Grow2Notes.Web.Platform.Audit;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;

namespace Grow2Notes.Web.Features.Auth;

/// <summary>
/// <c>POST /api/auth/login</c> (design.md §6.2, §8.2): the password step of a password sign-in. It answers in one of
/// three ways. For a person who chose a second step, Identity accepts the password and asks for it: the answer is
/// <c>{"next":"passkey"}</c> for one who holds a passkey and <c>{"next":"totp"}</c> otherwise
/// (<see cref="SecondStep"/>), and Identity's two-factor cookie holds the step until
/// <c>POST /api/auth/login/passkey/options</c> or
/// <c>POST /api/auth/login/totp</c> takes it. For one who chose a password only, with two-factor off (D97), Identity
/// signs them in on the password alone, and the answer is who they are, as a <see cref="Me"/>. Any other outcome is the
/// <c>401</c> of a failed sign-in, which starts nothing.
/// </summary>
/// <remarks>
/// Anyone may call it, as whoever asks is not signed in yet. It first uses a permit of the account's limit of 10 a
/// minute (design.md §9.9, <see cref="AccountRateLimiter"/>), by the email as Identity normalises it, so an email of no
/// account is counted like any other; a request over it is a <c>429</c> that writes no event, logs nothing and counts
/// nothing towards the lockout. It then finds the account by its email, in any letter case, and makes that account's
/// organisation the tenant (§5.9 item 1). Identity's <c>PasswordSignInAsync</c> then checks that the user may sign in
/// (<see cref="Grow2NotesSignInManager.CanSignInAsync"/>) and is not locked out, and checks the password,
/// counting a wrong one towards the lockout (A25), but only for an account that has a password: one set up with a
/// passkey has none to guess, and counting guesses at it would let anyone who knows the email lock its passkey out
/// (A55). It never signs in a person who chose a second step: the sign-in manager refuses one whom Identity would sign
/// in without it (<see cref="Grow2NotesSignInManager"/>'s <c>SignInOrTwoFactorAsync</c>, §8.1's invariant). A password
/// only is signed in here, so this step then writes the audit event of the success, <c>auth.signin_succeeded</c>,
/// after
/// Identity's own save, with no transaction, in the code step's shape: if writing it fails, the exception handler's
/// <c>500</c> clears the response's headers, the session cookie's <c>Set-Cookie</c> among them, so no session starts
/// without its event (<see cref="PasskeySignInEndpoint"/>). It writes an event for a failure on the account too, as
/// §5.9 lists no transaction for a sign-in: <c>auth.locked_out</c> when Identity reports a lockout, and
/// <c>auth.signin_failed</c> otherwise. An email of no account is audited nowhere (§5.4: known accounts only); it is
/// counted in telemetry by one log entry, which holds no value of the request (§9.5), and Identity is not asked about
/// it.
/// </remarks>
internal static partial class PasswordSignInEndpoint
{
    /// <summary>Maps the endpoint in the <c>/api</c> group, which <see cref="ApiGroup.MapApiGroup"/> returns.</summary>
    public static RouteGroupBuilder MapPasswordSignInEndpoint(this RouteGroupBuilder api)
    {
        api.MapPost("/auth/login", SignInAsync).AllowAnonymous();
        return api;
    }

    private static async Task<Results<
        Ok<PasswordSignInResponse>, Ok<Me>, UnauthorizedHttpResult, StatusCodeHttpResult>> SignInAsync(
        PasswordSignInRequest request,
        AccountRateLimiter accounts,
        HttpResponse response,
        SignInManager<ApplicationUser> signInManager,
        ITenantContext tenant,
        IAuditWriter audit,
        Grow2NotesDbContext db,
        MelbourneClock clock,
        ILoggerFactory loggers,
        CancellationToken cancellationToken)
    {
        if (!accounts.TryAcquirePasswordStep(signInManager.UserManager.NormalizeEmail(request.Email)))
        {
            return RateLimits.TooManyRequests(response);
        }

        if (await signInManager.UserManager.FindByEmailAsync(request.Email) is not { } user)
        {
            LogUnknownEmail(loggers.CreateLogger(typeof(PasswordSignInEndpoint)));
            return TypedResults.Unauthorized();
        }

        using (tenant.Use(user.OrganisationId))
        {
            var result = await signInManager.PasswordSignInAsync(
                user,
                request.Password,
                isPersistent: false,
                lockoutOnFailure: await signInManager.UserManager.HasPasswordAsync(user));
            if (result.RequiresTwoFactor)
            {
                return TypedResults.Ok(new PasswordSignInResponse(
                    Next: await SecondStep.IsPasskeyAsync(signInManager.UserManager, user) ? "passkey" : "totp"));
            }

            if (result.Succeeded)
            {
                await audit.WriteAsync(
                    user.OrganisationId,
                    actorUserId: user.Id,
                    AuditEventTypes.Auth.SignInSucceeded,
                    AuditEntityTypes.User,
                    entityId: user.Id,
                    participantId: null,
                    details: null,
                    cancellationToken);
                return TypedResults.Ok(await MeEndpoint.OfUserAsync(user, db, clock, cancellationToken));
            }

            // When another attempt on the account saved first, Identity's concurrency check refuses this one's save of
            // the failed-attempt count but leaves the change tracked, and the audit writer's save would send it again
            // and throw. Nothing else is left to save on a failure, so the event is written with nothing else tracked.
            db.ChangeTracker.Clear();
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
