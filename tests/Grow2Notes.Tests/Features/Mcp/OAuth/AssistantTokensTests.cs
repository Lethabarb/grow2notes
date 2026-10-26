using System.Security.Claims;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Features.Auth;
using Grow2Notes.Web.Features.Mcp;
using Grow2Notes.Web.Features.Mcp.OAuth;
using Grow2Notes.Web.Platform;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Grow2Notes.Tests.Features.Mcp.OAuth;

/// <summary>What an approved assistant's tokens carry (mcp-server.md §4.2, §6.1).</summary>
public sealed class AssistantTokensTests
{
    private const string Stamp = "stamp-of-the-test";
    private const string AuthTime = "1790000000";

    private static readonly string[] GrantedScopes = [McpSetup.AdminScope, Scopes.OfflineAccess];

    private readonly ApplicationUser user = new()
    {
        Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
        OrganisationId = Guid.Parse("33333333-3333-3333-3333-333333333333"),
        DisplayName = "Sam Lee",
        Role = UserRole.Manager,
        Status = UserStatus.Active,
        InvitedAtUtc = new(2026, 10, 18, 0, 0, 0, DateTimeKind.Utc),
        SecurityStamp = Stamp,
    };

    [Fact]
    public void The_principal_holds_the_person_their_organisation_the_stamp_hash_and_the_sign_in_time()
    {
        var principal = AssistantTokens.PrincipalFor(user, AuthTime, GrantedScopes, McpTestApp.Resource);

        Assert.Equal(user.Id.ToString("D"), principal.FindFirstValue(Claims.Subject));
        Assert.Equal(
            user.OrganisationId.ToString("D"), principal.FindFirstValue(TenantContext.OrganisationIdClaimType));
        Assert.Equal(AssistantTokens.StampHash(Stamp), principal.FindFirstValue(AssistantTokens.StampHashClaimType));
        Assert.Equal(AuthTime, principal.FindFirstValue(Grow2NotesSignInManager.AuthTimeClaimType));
    }

    [Fact]
    public void The_stamp_hash_is_not_the_stamp()
    {
        var hash = AssistantTokens.StampHash(Stamp);

        Assert.NotEqual(Stamp, hash);
        Assert.Equal(hash, AssistantTokens.StampHash(Stamp));
        Assert.NotEqual(hash, AssistantTokens.StampHash(Stamp + "x"));
    }

    [Fact]
    public void The_principal_has_no_role_claim_of_any_type()
    {
        var principal = AssistantTokens.PrincipalFor(user, AuthTime, GrantedScopes, McpTestApp.Resource);

        Assert.DoesNotContain(principal.Claims, claim => claim.Type is ClaimTypes.Role or Claims.Role);
        Assert.All(principal.Identities, identity => Assert.Empty(identity.FindAll(identity.RoleClaimType)));
        Assert.DoesNotContain(principal.Claims, claim => claim.Type is Claims.Name or Claims.Email);
    }

    [Fact]
    public void The_resource_is_the_one_given_and_the_scopes_are_the_ones_passed()
    {
        var principal = AssistantTokens.PrincipalFor(user, AuthTime, GrantedScopes, McpTestApp.Resource);

        Assert.Equal([McpTestApp.Resource], principal.GetResources().ToArray());
        Assert.Equal(GrantedScopes, principal.GetScopes().ToArray());
    }

    [Fact]
    public void Every_claim_goes_to_the_access_token_alone()
    {
        var principal = AssistantTokens.PrincipalFor(user, AuthTime, GrantedScopes, McpTestApp.Resource);

        var claims = principal.Claims.Where(claim => !claim.Type.StartsWith("oi_", StringComparison.Ordinal)).ToList();
        Assert.NotEmpty(claims);
        Assert.All(claims, claim => Assert.Equal(Destinations.AccessToken, Assert.Single(claim.GetDestinations())));
    }
}
