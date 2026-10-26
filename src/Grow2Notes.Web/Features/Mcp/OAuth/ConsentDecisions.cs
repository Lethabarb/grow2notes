using System.Security.Claims;
using System.Text.Json.Nodes;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Grow2Notes.Web.Platform.Audit;
using Microsoft.AspNetCore.Identity;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Grow2Notes.Web.Features.Mcp.OAuth;

/// <summary>
/// Acts on a person's decision on an authorise request (mcp-server.md §4.1 step 5, §9): allows it only for a manager or
/// a Support person, and writes the audit event of either outcome. It is registered whether or not the access is
/// switched on, as a handler class (§3.5, convention 1).
/// </summary>
internal sealed class ConsentDecisions(
    UserManager<ApplicationUser> users,
    IAuditWriter audit,
    ITenantContext tenant,
    IConfiguration configuration)
{
    // Every allowed assistant is given exactly these (A112).
    private static readonly string[] GrantedScopes = [McpSetup.AdminScope, Scopes.OfflineAccess];

    /// <summary>
    /// Writes <c>mcp.authorised</c> and returns the principal to sign in when <paramref name="allow"/> is true and the
    /// person is a manager or Support, as their row says now. Otherwise writes <c>mcp.authorisation_refused</c>, with
    /// why <c>cancelled</c> for a manager or Support who cancelled and <c>not_allowed</c> for anyone else, and returns
    /// null. The event is saved at once, just before the code is issued or refused.
    /// </summary>
    /// <param name="actor">The signed-in person who chose.</param>
    /// <param name="allow">Whether they chose Allow.</param>
    /// <param name="clientId">The assistant's client ID.</param>
    /// <param name="authTime">The sign-in time of the person's session, from its <c>auth_time</c> claim.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    public async Task<ClaimsPrincipal?> TakeAsync(
        Actor actor, bool allow, string clientId, string authTime, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(actor.UserId.ToString("D"))
            ?? throw new InvalidOperationException("The person who chose is not a user.");
        var mayConnect = user.Role is UserRole.Manager or UserRole.Support;

        // The tenant is the person's own organisation: outside a request, as in a test, there is no session to give it.
        using (tenant.Use(user.OrganisationId))
        {
            if (allow && mayConnect)
            {
                await WriteAsync(
                    user,
                    AuditEventTypes.Mcp.Authorised,
                    new() { ["client"] = clientId, ["scope"] = string.Join(' ', GrantedScopes) },
                    cancellationToken);
                return AssistantTokens.PrincipalFor(user, authTime, GrantedScopes, McpSetup.Resource(configuration));
            }

            await WriteAsync(
                user,
                AuditEventTypes.Mcp.AuthorisationRefused,
                new() { ["client"] = clientId, ["why"] = mayConnect ? "cancelled" : "not_allowed" },
                cancellationToken);
            return null;
        }
    }

    // No entity: the event is about the person's act, not a row.
    private Task WriteAsync(
        ApplicationUser user, string eventType, JsonObject details, CancellationToken cancellationToken) =>
        audit.WriteAsync(
            user.OrganisationId,
            actorUserId: user.Id,
            eventType,
            entityType: null,
            entityId: null,
            participantId: null,
            details,
            cancellationToken);
}
