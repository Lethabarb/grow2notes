using System.Security.Claims;
using Grow2Notes.Web.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;

namespace Grow2Notes.Web.Features.Auth;

/// <summary>
/// The enrolment cookie (design.md §6.1, §8.1 step 3): a cookie scheme of its own, apart from the session cookie, that
/// <c>POST /api/auth/setup/start</c> signs in with the ID and security stamp of the user whose setup link it has
/// checked, so that the later setup steps check the cookie, not the token. The browser sends it to
/// <c>/api/auth/setup</c> and the paths under it only, the one place it is valid (§8).
/// </summary>
/// <remarks>
/// It is <c>Secure</c>, <c>HttpOnly</c> and <c>SameSite=Strict</c>, as the session cookie is. A <c>__Host-</c> name
/// would need <c>Path=/</c>, so its prefix is <c>__Secure-</c>, which needs only <c>Secure</c>. It is a session cookie,
/// with no <c>Expires</c> or <c>Max-Age</c>, whose ticket ends 30 minutes after the link check and is never renewed,
/// so the window has one end. The setup endpoints read it themselves, on <see cref="Scheme"/>, never through a policy,
/// so it never becomes the request's user: the antiforgery token that the SPA fetched before the start, for the user
/// the antiforgery endpoint saw then, still checks on every later setup <c>POST</c> (S00.04.02's Notes). The stamp is
/// there because a resend or a Reset sign-in leaves the user Invited with a new stamp and link: a cookie that an older
/// link started must stop working then, or whoever holds it could finish setup in the person's place (A53).
/// </remarks>
internal static class EnrolmentCookie
{
    /// <summary>
    /// The scheme's name, which is also part of its tickets' Data Protection purpose, so neither this cookie nor the
    /// session cookie can stand in for the other.
    /// </summary>
    public const string Scheme = "Enrolment";

    /// <summary>Adds the scheme to <paramref name="authentication"/>.</summary>
    public static AuthenticationBuilder AddEnrolmentCookie(this AuthenticationBuilder authentication) =>
        authentication.AddCookie(Scheme, o =>
        {
            o.Cookie.Name = "__Secure-grow2notes-setup";
            o.Cookie.Path = "/api/auth/setup";
            o.Cookie.HttpOnly = true;
            o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            o.Cookie.SameSite = SameSiteMode.Strict;
            o.ExpireTimeSpan = TimeSpan.FromMinutes(30);
            o.SlidingExpiration = false;
        });

    /// <summary>
    /// Issues the cookie for <paramref name="user"/>, with their ID and security stamp, replacing any that the browser
    /// holds.
    /// </summary>
    public static async Task SignInAsync(HttpContext context, UserManager<ApplicationUser> users, ApplicationUser user)
    {
        Claim[] claims =
        [
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(users.Options.ClaimsIdentity.SecurityStampClaimType, await users.GetSecurityStampAsync(user)),
        ];
        await context.SignInAsync(Scheme, new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme)));
    }

    /// <summary>
    /// The enrolment check of each setup step after the start (design.md §8.1 step 3): the user whose ID the request's
    /// cookie holds, while that user is still Invited with the security stamp that the cookie holds; or
    /// <see langword="null"/>, which the step answers with a <c>401</c>, when the request has no cookie, or one that
    /// has ended or does not verify, or its user has completed setup, been deactivated or been sent a new link since.
    /// </summary>
    public static async Task<ApplicationUser?> FindInvitedUserAsync(
        HttpContext context, UserManager<ApplicationUser> users)
    {
        var result = await context.AuthenticateAsync(Scheme);
        return result.Succeeded
            && result.Principal.FindFirstValue(ClaimTypes.NameIdentifier) is { } userId
            && await users.FindByIdAsync(userId) is { Status: UserStatus.Invited } user
            && result.Principal.FindFirstValue(users.Options.ClaimsIdentity.SecurityStampClaimType) is { } stamp
            && stamp == user.SecurityStamp
                ? user
                : null;
    }
}
