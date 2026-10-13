using System.Security.Claims;
using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Identity;

namespace Grow2Notes.Tests.Platform;

/// <summary>
/// The acting user and the channel (mcp-server.md §3.5, convention 1). <see cref="ActorOnSqlServerTests"/> builds the
/// app's <see cref="Actor"/> from a principal that the claims factory builds.
/// </summary>
public sealed class ActorTests
{
    private static readonly Guid OrganisationId = Guid.Parse("0b4c1f6e-7a52-4c1d-9e3a-2f8d5b6a7c01");

    [Fact]
    public void A_principal_with_no_user_ID_claim_throws_rather_than_acting_as_no_one()
    {
        var options = new IdentityOptions();

        // Every other claim that the claims factory adds and the policies read.
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(TenantContext.OrganisationIdClaimType, OrganisationId.ToString()),
                new Claim(options.ClaimsIdentity.RoleClaimType, "Manager"),
            ],
            authenticationType: "Test"));

        Assert.Throws<InvalidOperationException>(() => Actor.InApp(user, options));
    }

    // Release 2's tools pass Mcp, and its events say how a change was made, so a value must not change meaning.
    [Fact]
    public void Channel_keeps_its_values() =>
        Assert.Equal(
            new Dictionary<string, int> { ["App"] = 1, ["Mcp"] = 2 },
            Enum.GetValues<Channel>().ToDictionary(c => c.ToString(), c => (int)c));
}
