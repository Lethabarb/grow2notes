using System.Globalization;
using System.Security.Claims;
using Grow2Notes.Web.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Grow2Notes.Web.Features.Auth;

/// <summary>
/// The app's sign-in manager (design.md §8.2, §8.4): only an Active user can sign in, and every sign-in records when
/// it happened, in <see cref="AuthTimeClaimType"/>, for the 12-hour session limit (§8.5).
/// </summary>
internal sealed class Grow2NotesSignInManager(
    UserManager<ApplicationUser> userManager,
    IHttpContextAccessor contextAccessor,
    IUserClaimsPrincipalFactory<ApplicationUser> claimsFactory,
    IOptions<IdentityOptions> optionsAccessor,
    ILogger<SignInManager<ApplicationUser>> logger,
    IAuthenticationSchemeProvider schemes,
    IUserConfirmation<ApplicationUser> confirmation,
    TimeProvider timeProvider)
    : SignInManager<ApplicationUser>(
        userManager, contextAccessor, claimsFactory, optionsAccessor, logger, schemes, confirmation)
{
    /// <summary>
    /// The claim that holds when the session signed in, from <see cref="TimeProvider"/>, in whole seconds since 1970
    /// UTC, as OpenID Connect writes its <c>auth_time</c> claim. It is added once, at sign-in, because the claims
    /// factory cannot know it; <see cref="SessionRules.CarryForward"/> copies it each time the session check rebuilds
    /// the principal, and <see cref="SessionRules.ValidateAsync"/> ends the session 12 hours after it.
    /// </summary>
    public const string AuthTimeClaimType = "auth_time";

    /// <summary>
    /// Admits only an Active user (design.md §8.1, §8.2). An Invited user has not finished setup, or has had their
    /// sign-in reset, and a Deactivated one may not sign in at all. The status is the gate, not a confirmed email.
    /// </summary>
    public override async Task<bool> CanSignInAsync(ApplicationUser user) =>
        user.Status == UserStatus.Active && await base.CanSignInAsync(user);

    /// <summary>
    /// Checks a passkey sign-in's credential against the ceremony in Identity's two-factor cookie, and fails, rather
    /// than throws, where no passkey sign-in is underway: the cookie is missing, has expired after its 5 minutes, or
    /// holds another operation, such as setup's ceremony or a password sign-in's code step. For the person, that is a
    /// failed sign-in like any other, so <c>PasskeySignInAsync</c> returns <c>Failed</c> (S00.04.04's Notes).
    /// </summary>
    /// <remarks>
    /// Identity throws <see cref="InvalidOperationException"/> for each of those where it reads the ceremony, which is
    /// private, so the exception is caught around this call alone. Saving the passkey and signing in come after it, so
    /// an exception from either still reaches the exception handler as a <c>500</c>.
    /// </remarks>
    public override async Task<PasskeyAssertionResult<ApplicationUser>> PerformPasskeyAssertionAsync(
        string credentialJson)
    {
        try
        {
            return await base.PerformPasskeyAssertionAsync(credentialJson);
        }
        catch (InvalidOperationException exception)
        {
            return PasskeyAssertionResult.Fail<ApplicationUser>(new PasskeyException(exception.Message, exception));
        }
    }

    // Every sign-in ends here: SignInAsync, as setup completion calls it, and the password, two-factor and passkey
    // sign-ins.
    public override Task SignInWithClaimsAsync(
        ApplicationUser user, AuthenticationProperties? authenticationProperties, IEnumerable<Claim> additionalClaims)
    {
        var signedInAt = timeProvider.GetUtcNow().ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        return base.SignInWithClaimsAsync(
            user,
            authenticationProperties,
            [.. additionalClaims, new(AuthTimeClaimType, signedInAt, ClaimValueTypes.Integer64)]);
    }
}
