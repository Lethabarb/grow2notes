using System.Security.Claims;
using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;

namespace Grow2Notes.Tests.Platform;

/// <summary>
/// The user an admin handler acts as (mcp-server.md §3.5, convention 1), as an endpoint builds it from the session:
/// the user ID claim that the claims factory adds, and the request's tenant, from the <c>org_id</c> claim.
/// </summary>
public sealed class ActingUserTests
{
    private static readonly Guid UserId = Guid.Parse("3f6b2c1d-8e4a-4b7f-9c2d-6a1e5f7b8c03");

    private static readonly Guid OrganisationId = Guid.Parse("0b4c1f6e-7a52-4c1d-9e3a-2f8d5b6a7c01");

    private static readonly IdentityOptions IdentityOptions = new();

    [Fact]
    public void A_signed_in_user_acts_as_themselves_in_their_own_organisation()
    {
        var user = SignedIn(
            new(IdentityOptions.ClaimsIdentity.UserIdClaimType, UserId.ToString()),
            new(TenantContext.OrganisationIdClaimType, OrganisationId.ToString()));

        var actingUser = ActingUser.FromSession(user, IdentityOptions, InRequest(user));

        Assert.Equal(new ActingUser(UserId, OrganisationId), actingUser);
    }

    [Fact]
    public void A_principal_with_no_user_ID_claim_throws_rather_than_acting_as_no_one()
    {
        var user = SignedIn(new Claim(TenantContext.OrganisationIdClaimType, OrganisationId.ToString()));

        Assert.Throws<InvalidOperationException>(() => ActingUser.FromSession(user, IdentityOptions, InRequest(user)));
    }

    private static ClaimsPrincipal SignedIn(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, authenticationType: "Test"));

    private static TenantContext InRequest(ClaimsPrincipal user) =>
        new(new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = user } });
}
