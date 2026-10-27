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

    [Fact]
    public void A_manager_who_is_as_they_were_is_allowed()
    {
        Assert.Equal(AssistantTokenCheck.Allowed, AssistantTokens.Check(user, Stored(), SignedInAt));
    }

    [Fact]
    public void A_manager_since_made_Support_is_allowed()
    {
        var stored = Stored();
        user.Role = UserRole.Support;

        Assert.Equal(AssistantTokenCheck.Allowed, AssistantTokens.Check(user, stored, SignedInAt));
    }

    [Fact]
    public void A_person_who_is_gone_is_a_changed_user()
    {
        Assert.Equal(AssistantTokenCheck.UserChanged, AssistantTokens.Check(null, Stored(), SignedInAt));
    }

    [Fact]
    public void A_person_who_is_invited_and_not_active_is_a_changed_user()
    {
        var stored = Stored();
        user.Status = UserStatus.Invited;

        Assert.Equal(AssistantTokenCheck.UserChanged, AssistantTokens.Check(user, stored, SignedInAt));
    }

    [Fact]
    public void A_person_who_is_deactivated_is_a_changed_user()
    {
        var stored = Stored();
        user.Status = UserStatus.Deactivated;

        Assert.Equal(AssistantTokenCheck.UserChanged, AssistantTokens.Check(user, stored, SignedInAt));
    }

    [Fact]
    public void A_person_made_a_worker_is_a_changed_user()
    {
        var stored = Stored();
        user.Role = UserRole.Worker;

        Assert.Equal(AssistantTokenCheck.UserChanged, AssistantTokens.Check(user, stored, SignedInAt));
    }

    [Fact]
    public void A_token_for_another_organisation_than_the_person_is_UserChanged()
    {
        var stored = Stored();
        user.OrganisationId = Guid.Parse("44444444-4444-4444-4444-444444444444");

        Assert.Equal(AssistantTokenCheck.UserChanged, AssistantTokens.Check(user, stored, SignedInAt));
    }

    [Fact]
    public void A_changed_security_stamp_is_a_changed_user()
    {
        var stored = Stored();
        user.SecurityStamp = Stamp + "-reset";

        Assert.Equal(AssistantTokenCheck.UserChanged, AssistantTokens.Check(user, stored, SignedInAt));
    }

    [Fact]
    public void A_person_with_no_security_stamp_is_a_changed_user()
    {
        var stored = Stored();
        user.SecurityStamp = null;

        Assert.Equal(AssistantTokenCheck.UserChanged, AssistantTokens.Check(user, stored, SignedInAt));
    }

    [Fact]
    public void A_stored_principal_with_no_stamp_hash_is_a_changed_user()
    {
        var stored = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(Grow2NotesSignInManager.AuthTimeClaimType, AuthTime)], "test"));

        Assert.Equal(AssistantTokenCheck.UserChanged, AssistantTokens.Check(user, stored, SignedInAt));
    }

    [Fact]
    public void A_deactivated_person_past_12_hours_is_a_changed_user_and_not_past_the_limit()
    {
        var stored = Stored();
        user.Status = UserStatus.Deactivated;

        Assert.Equal(
            AssistantTokenCheck.UserChanged,
            AssistantTokens.Check(user, stored, SignedInAt + TimeSpan.FromHours(12) + TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void An_active_manager_is_allowed_at_exactly_12_hours_and_refused_after()
    {
        var stored = Stored();

        Assert.Equal(
            AssistantTokenCheck.Allowed, AssistantTokens.Check(user, stored, SignedInAt + TimeSpan.FromHours(12)));
        Assert.Equal(
            AssistantTokenCheck.PastAbsoluteLimit,
            AssistantTokens.Check(user, stored, SignedInAt + TimeSpan.FromHours(12) + TimeSpan.FromSeconds(1)));
    }

    private static DateTimeOffset SignedInAt => DateTimeOffset.FromUnixTimeSeconds(long.Parse(AuthTime));

    // The principal that a code or a refresh token holds: what PrincipalFor made, for the person as they were.
    private ClaimsPrincipal Stored() =>
        AssistantTokens.PrincipalFor(user, AuthTime, GrantedScopes, McpTestApp.Resource);
}
