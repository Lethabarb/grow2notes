using System.Security.Claims;
using System.Text.Encodings.Web;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Grow2Notes.Tests.Fixtures;

/// <summary>
/// Signing in, in tests only: an authentication scheme of the test project's own, which <see cref="Grow2NotesFactory"/>
/// makes the app's default, so a test calls as a seeded user, who has no passkey or password. A request that names a
/// user's ID in <see cref="UserHeader"/> is signed in as that user, with the claims that design.md §8.4's claims
/// factory adds, read from the user's row: the user's ID, their organisation's ID in
/// <see cref="TenantContext.OrganisationIdClaimType"/>, and their role by its <see cref="UserRole"/> name, the ID and
/// the role in Identity's claim types for them. A request with neither header is signed out. The scheme challenges
/// with <c>401</c> and forbids with <c>403</c>, as the session cookie's events do. The app has no such scheme.
/// </summary>
internal static class TestSignIn
{
    public const string UserHeader = "X-Test-User";

    /// <summary>
    /// Replaces the signed-in user's role claim value, for a role that <see cref="UserRole"/> does not have; with
    /// <see cref="NoRole"/>, the user has no role claim. It needs <see cref="UserHeader"/>.
    /// </summary>
    public const string RoleHeader = "X-Test-Role";

    /// <summary>
    /// The <see cref="RoleHeader"/> value that leaves the user with no role claim. An empty value cannot say so: the
    /// test server drops a header whose value is empty, and the user keeps the role on their row.
    /// </summary>
    public const string NoRole = "(none)";

    private const string SchemeName = "TestSignIn";

    public static IServiceCollection AddTestSignIn(this IServiceCollection services)
    {
        services.AddAuthentication(SchemeName).AddScheme<AuthenticationSchemeOptions, Handler>(SchemeName, null);
        return services;
    }

    /// <summary>
    /// Signs in every request that <paramref name="client"/> sends as the user whose ID is <paramref name="userId"/>.
    /// </summary>
    public static HttpClient SignInAs(this HttpClient client, Guid userId)
    {
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

    private sealed class Handler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        UserManager<ApplicationUser> users)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var replacesRole = Request.Headers.TryGetValue(RoleHeader, out var role);
            if (!Request.Headers.TryGetValue(UserHeader, out var userId))
            {
                return replacesRole
                    ? AuthenticateResult.Fail($"{RoleHeader} needs {UserHeader}.")
                    : AuthenticateResult.NoResult();
            }

            if (!Guid.TryParse(userId, out var id) || await users.FindByIdAsync(id.ToString()) is not { } user)
            {
                return AuthenticateResult.Fail($"{UserHeader} names no user.");
            }

            var types = users.Options.ClaimsIdentity;
            List<Claim> claims =
            [
                new(types.UserIdClaimType, user.Id.ToString()),
                new(TenantContext.OrganisationIdClaimType, user.OrganisationId.ToString()),
            ];

            var roleValue = replacesRole ? role.ToString() : user.Role.ToString();
            if (roleValue != NoRole)
            {
                claims.Add(new(types.RoleClaimType, roleValue));
            }

            var identity = new ClaimsIdentity(claims, Scheme.Name, types.UserNameClaimType, types.RoleClaimType);
            return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name));
        }
    }
}
