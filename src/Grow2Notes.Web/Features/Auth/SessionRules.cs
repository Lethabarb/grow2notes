using System.Globalization;
using System.Security.Claims;
using Grow2Notes.Web.Data;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;

namespace Grow2Notes.Web.Features.Auth;

/// <summary>
/// The checks that the session cookie runs on each request that carries it (design.md §8.4, §8.5): Identity's security
/// stamp check, which rebuilds the principal with <see cref="Grow2NotesClaimsFactory"/> and renews the cookie, and then
/// the 12-hour limit from sign-in, in <see cref="Grow2NotesSignInManager.AuthTimeClaimType"/>. The 30-minute idle
/// limit is the cookie's own sliding expiry, which that renewal restarts.
/// </summary>
internal static class SessionRules
{
    private static readonly TimeSpan AbsoluteLimit = TimeSpan.FromHours(12);

    /// <summary>
    /// The cookie's <c>OnValidatePrincipal</c>. First the stamp check, which rejects and signs out a session whose
    /// user's security stamp has changed, as deactivation and a sign-in reset change it (§8.6), and otherwise rebuilds
    /// the principal, keeping its sign-in time (<see cref="CarryForward"/>), and renews the cookie. Then a session that
    /// signed in more than 12 hours ago, by <see cref="TimeProvider"/>, is rejected and signed out too, and so is one
    /// with no sign-in time: it did not sign in through <see cref="Grow2NotesSignInManager"/>, so it has no start to
    /// count the 12 hours from.
    /// </summary>
    public static async Task ValidateAsync(CookieValidatePrincipalContext context)
    {
        await SecurityStampValidator.ValidatePrincipalAsync(context);

        // The stamp check rejected it, and has signed it out.
        if (context.Principal is null)
        {
            return;
        }

        var services = context.HttpContext.RequestServices;
        if (!IsWithinAbsoluteLimit(context.Principal, services.GetRequiredService<TimeProvider>().GetUtcNow()))
        {
            // Signing out deletes the cookie, as the stamp check does when it rejects, so the browser stops sending it.
            context.RejectPrincipal();
            await services.GetRequiredService<SignInManager<ApplicationUser>>().SignOutAsync();
        }
    }

    /// <summary>
    /// The stamp check's <c>OnRefreshingPrincipal</c>: copies the sign-in time to the principal that the claims factory
    /// has rebuilt from the user's row, which cannot know it, so the 12-hour limit counts from the sign-in, not from
    /// the last request.
    /// </summary>
    public static Task CarryForward(SecurityStampRefreshingPrincipalContext context)
    {
        if (context.CurrentPrincipal?.FindFirst(Grow2NotesSignInManager.AuthTimeClaimType) is { } signedInAt
            && context.NewPrincipal?.Identity is ClaimsIdentity rebuilt)
        {
            rebuilt.AddClaim(new(signedInAt.Type, signedInAt.Value, signedInAt.ValueType));
        }

        return Task.CompletedTask;
    }

    // In whole seconds since 1970, as the sign-in manager writes it. A session is let in at exactly 12 hours, and
    // refused only once they are exceeded (design.md §8.4).
    private static bool IsWithinAbsoluteLimit(ClaimsPrincipal principal, DateTimeOffset now) =>
        long.TryParse(
            principal.FindFirstValue(Grow2NotesSignInManager.AuthTimeClaimType),
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out var signedInAt)
        && now - DateTimeOffset.FromUnixTimeSeconds(signedInAt) <= AbsoluteLimit;
}
