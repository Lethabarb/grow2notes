using System.Text.Encodings.Web;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Features.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Grow2Notes.Tests.Fixtures;

/// <summary>
/// Signing in, in tests only: an authentication scheme of the test project's own, beside the session cookie, so a test
/// calls as a seeded user, who has no passkey or password. <see cref="Grow2NotesFactory"/>'s default scheme sends a
/// request with <see cref="UserHeader"/> or <see cref="RoleHeader"/> here, and any other to the session cookie, as in
/// the deployed app, so a request with neither is signed in by its cookie or is signed out. A request that names a
/// user's ID in <see cref="UserHeader"/> is signed in as that user, with the principal that the app's claims factory
/// (<see cref="Grow2NotesClaimsFactory"/>) builds from the user's row, as the session cookie's stamp check rebuilds it
/// on every request (design.md §8.4). The scheme challenges with <c>401</c> and forbids with <c>403</c>, as the session
/// cookie's events do. The app has no such scheme.
/// </summary>
/// <remarks>
/// The session checks (<see cref="SessionRules"/>) run in the session cookie only, so a request signed in here skips
/// them: the security stamp, the idle limit and the 12-hour limit do not apply to it, and its principal has no
/// <see cref="Grow2NotesSignInManager.AuthTimeClaimType"/>, which only a sign-in adds.
/// </remarks>
internal static class TestSignIn
{
    public const string UserHeader = "X-Test-User";

    /// <summary>
    /// Replaces the signed-in user's role claim, and no other, for a role that <see cref="UserRole"/> does not have;
    /// with <see cref="NoRole"/>, the user has no role claim. It needs <see cref="UserHeader"/>: a request with this
    /// header alone fails to sign in, rather than going to the session cookie, which would ignore the header.
    /// </summary>
    public const string RoleHeader = "X-Test-Role";

    /// <summary>
    /// The <see cref="RoleHeader"/> value that leaves the user with no role claim. An empty value cannot say so: the
    /// test server drops a header whose value is empty, and the user keeps the role on their row.
    /// </summary>
    public const string NoRole = "(none)";

    private const string SchemeName = "TestSignIn";

    private const string PolicySchemeName = "TestSignInOrCookie";

    /// <summary>
    /// Adds the test-only sign-in, and makes the default scheme, in place of the session cookie, one that sends each
    /// request to the test-only sign-in or to the cookie by its headers, to challenge or forbid it as well as to
    /// authenticate it, so a request is refused by the scheme that signed it in or found it signed out.
    /// </summary>
    public static IServiceCollection AddTestSignIn(this IServiceCollection services)
    {
        services.AddAuthentication(PolicySchemeName)
            .AddPolicyScheme(PolicySchemeName, displayName: null, options =>
                options.ForwardDefaultSelector = context =>
                    HasTestHeader(context.Request) ? SchemeName : IdentityConstants.ApplicationScheme)
            .AddScheme<AuthenticationSchemeOptions, Handler>(SchemeName, null);
        return services;
    }

    /// <summary>
    /// Signs in every request that <paramref name="client"/> sends from now on as the user whose ID is
    /// <paramref name="userId"/>, in place of any user it signed in as before.
    /// </summary>
    public static HttpClient SignInAs(this HttpClient client, Guid userId)
    {
        client.DefaultRequestHeaders.Remove(UserHeader);
        client.DefaultRequestHeaders.Add(UserHeader, userId.ToString());
        return client;
    }

    /// <summary>
    /// Signs in every request that <paramref name="client"/> sends as <paramref name="caller"/>, one of the users in
    /// <paramref name="seeded"/>, or leaves it signed out.
    /// </summary>
    public static HttpClient SignInAs(this HttpClient client, Caller caller, SeededOrganisations seeded) =>
        caller switch
        {
            Caller.SignedOut => client,
            Caller.WorkerOfA => client.SignInAs(seeded.A.WorkerId),
            Caller.ManagerOfA => client.SignInAs(seeded.A.ManagerId),
            Caller.ManagerOfB => client.SignInAs(seeded.B.ManagerId),
            _ => throw new ArgumentOutOfRangeException(nameof(caller), caller, null),
        };

    private static bool HasTestHeader(HttpRequest request) =>
        request.Headers.ContainsKey(UserHeader) || request.Headers.ContainsKey(RoleHeader);

    private sealed class Handler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        UserManager<ApplicationUser> users,
        IUserClaimsPrincipalFactory<ApplicationUser> claimsFactory)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            // The default scheme sends a request here only when it has one of the two headers.
            if (!Request.Headers.TryGetValue(UserHeader, out var userId))
            {
                return AuthenticateResult.Fail($"{RoleHeader} needs {UserHeader}.");
            }

            if (!Guid.TryParse(userId, out var id) || await users.FindByIdAsync(id.ToString()) is not { } user)
            {
                return AuthenticateResult.Fail($"{UserHeader} names no user.");
            }

            var principal = await claimsFactory.CreateAsync(user);
            if (Request.Headers.TryGetValue(RoleHeader, out var role))
            {
                // The factory builds one identity, with one role claim, of the type that its IsInRole reads.
                var identity = principal.Identities.Single();
                identity.RemoveClaim(identity.FindFirst(identity.RoleClaimType));
                if (role != NoRole)
                {
                    identity.AddClaim(new(identity.RoleClaimType, role.ToString()));
                }
            }

            return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
        }
    }
}
