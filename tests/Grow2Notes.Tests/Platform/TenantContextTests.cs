using System.Security.Claims;
using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Http;

namespace Grow2Notes.Tests.Platform;

public sealed class TenantContextTests
{
    private static readonly Guid OrganisationA = Guid.Parse("0b4c1f6e-7a52-4c1d-9e3a-2f8d5b6a7c01");

    private static readonly Guid OrganisationB = Guid.Parse("5d9e2a7b-3c41-4f8e-8b6d-1a2c3e4f5a02");

    [Fact]
    public void A_signed_in_user_s_tenant_is_their_org_id_claim()
    {
        var tenant = InRequest(SignedIn(OrganisationA.ToString()));

        Assert.Equal(OrganisationA, tenant.OrganisationId);
    }

    [Fact]
    public void A_user_set_after_the_context_was_made_still_gives_the_tenant()
    {
        var request = new DefaultHttpContext();
        var tenant = new TenantContext(new HttpContextAccessor { HttpContext = request });

        request.User = SignedIn(OrganisationA.ToString());

        Assert.Equal(OrganisationA, tenant.OrganisationId);
    }

    [Fact]
    public void Outside_a_request_the_tenant_is_the_one_Use_gives_until_its_block_ends()
    {
        var tenant = new TenantContext(new HttpContextAccessor());

        using (tenant.Use(OrganisationA))
        {
            Assert.Equal(OrganisationA, tenant.OrganisationId);
        }

        Assert.Throws<InvalidOperationException>(() => tenant.OrganisationId);
    }

    [Fact]
    public void In_a_request_with_no_session_the_tenant_is_the_one_Use_gives_until_its_block_ends()
    {
        var tenant = InRequest(new ClaimsPrincipal(new ClaimsIdentity()));

        using (tenant.Use(OrganisationA))
        {
            Assert.Equal(OrganisationA, tenant.OrganisationId);
        }

        Assert.Throws<InvalidOperationException>(() => tenant.OrganisationId);
    }

    [Fact]
    public void Use_wins_over_the_claim_inside_its_block_and_the_claim_applies_again_after_it()
    {
        var tenant = InRequest(SignedIn(OrganisationA.ToString()));

        using (tenant.Use(OrganisationB))
        {
            Assert.Equal(OrganisationB, tenant.OrganisationId);
        }

        Assert.Equal(OrganisationA, tenant.OrganisationId);
    }

    [Fact]
    public void A_nested_Use_block_gives_back_the_outer_block_s_tenant_when_it_ends()
    {
        var tenant = new TenantContext(new HttpContextAccessor());

        using (tenant.Use(OrganisationA))
        {
            using (tenant.Use(OrganisationB))
            {
                Assert.Equal(OrganisationB, tenant.OrganisationId);
            }

            Assert.Equal(OrganisationA, tenant.OrganisationId);
        }

        Assert.Throws<InvalidOperationException>(() => tenant.OrganisationId);
    }

    [Fact]
    public async Task The_tenant_that_Use_gives_holds_across_awaits_and_in_tasks_started_inside_its_block()
    {
        var tenant = new TenantContext(new HttpContextAccessor());

        using (tenant.Use(OrganisationA))
        {
            await Task.Yield();
            Assert.Equal(OrganisationA, tenant.OrganisationId);
            Assert.Equal(OrganisationA, await Task.Run(() => tenant.OrganisationId));
        }

        Assert.Throws<InvalidOperationException>(() => tenant.OrganisationId);
    }

    [Fact]
    public async Task The_tenant_that_Use_gives_does_not_reach_a_flow_started_before_its_block()
    {
        var tenant = new TenantContext(new HttpContextAccessor());
        var blockOpen = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var elsewhere = Task.Run(async () =>
        {
            await blockOpen.Task;
            return Record.Exception(() => tenant.OrganisationId);
        });

        using (tenant.Use(OrganisationA))
        {
            blockOpen.SetResult();
            Assert.IsType<InvalidOperationException>(await elsewhere);
        }
    }

    [Fact]
    public void With_neither_a_claim_nor_Use_reading_the_tenant_throws()
    {
        var tenant = InRequest(new ClaimsPrincipal(new ClaimsIdentity()));

        var exception = Assert.Throws<InvalidOperationException>(() => tenant.OrganisationId);
        Assert.StartsWith("There is no tenant", exception.Message);
    }

    [Fact]
    public void An_org_id_claim_that_is_not_a_GUID_throws()
    {
        var tenant = InRequest(SignedIn("not-a-guid"));

        var exception = Assert.Throws<InvalidOperationException>(() => tenant.OrganisationId);
        Assert.Contains("is not a GUID", exception.Message);
    }

    private static TenantContext InRequest(ClaimsPrincipal user) =>
        new(new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = user } });

    private static ClaimsPrincipal SignedIn(string organisationId) => new(new ClaimsIdentity(
        [new Claim(TenantContext.OrganisationIdClaimType, organisationId)], authenticationType: "Test"));
}
