using Grow2Notes.Web.Data;
using Grow2Notes.Web.Features.Mcp.OAuth;
using Microsoft.AspNetCore.Identity;
using OpenIddict.Abstractions;
using OpenIddict.Validation;
using static OpenIddict.Abstractions.OpenIddictConstants;
using static OpenIddict.Validation.OpenIddictValidationEvents;
using static OpenIddict.Validation.OpenIddictValidationHandlers;

namespace Grow2Notes.Web.Features.Mcp;

/// <summary>
/// The check on every call that an approved assistant makes (mcp-server.md §4.5 step 4): the person the access token
/// names is read from their row, as they are now, and the call goes on only if they are still allowed. The token holds
/// no role, so the role of the call is the one this adds from the row, for this call alone.
/// </summary>
/// <remarks>
/// It runs after OpenIddict's own validation of the token, which has already checked its audience, its expiry and its
/// authorisation, so only a token that passed all of those costs a read of the row.
/// </remarks>
internal sealed class AssistantCallCheck(
    UserManager<ApplicationUser> users,
    IOpenIddictAuthorizationManager authorizations,
    TimeProvider time) : IOpenIddictValidationHandler<ProcessAuthenticationContext>
{
    public static OpenIddictValidationHandlerDescriptor Descriptor { get; } =
        OpenIddictValidationHandlerDescriptor.CreateBuilder<ProcessAuthenticationContext>()
            .UseScopedHandler<AssistantCallCheck>()
            .SetOrder(ValidateAccessToken.Descriptor.Order + 1_000)
            .SetType(OpenIddictValidationHandlerType.Custom)
            .Build();

    public async ValueTask HandleAsync(ProcessAuthenticationContext context)
    {
        if (context.AccessTokenPrincipal is not { } principal)
        {
            return;
        }

        var subject = principal.GetClaim(Claims.Subject);
        var user = subject is null ? null : await users.FindByIdAsync(subject);

        switch (AssistantTokens.Check(user, principal, time.GetUtcNow()))
        {
            case AssistantTokenCheck.Allowed:
                var identity = principal.Identities.First();
                identity.AddClaim(new(identity.RoleClaimType, user!.Role.ToString()));
                break;

            case AssistantTokenCheck.UserChanged:
                // As at the token endpoint, whoever changed, every connection that person has ends.
                await authorizations.RevokeConnectionsAsync(Guid.Parse(subject!), context.CancellationToken);
                context.Reject(Errors.InvalidToken, "The person is no longer allowed to use this connection.");
                break;

            default:
                // The session that began the connection is past its limit, which never changes, so the token cannot
                // be used again. The person's other assistants, from a later sign-in, are not touched.
                context.Reject(Errors.InvalidToken, "The session that began this connection has ended.");
                break;
        }
    }
}
