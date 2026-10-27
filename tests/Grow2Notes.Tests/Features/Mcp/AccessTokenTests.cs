using System.Net;
using System.Security.Claims;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Features.Auth;
using Grow2Notes.Web.Features.Mcp;
using Grow2Notes.Web.Features.Mcp.OAuth;
using Grow2Notes.Web.Platform;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Grow2Notes.Tests.Features.Mcp;

/// <summary>
/// The access token of an approved assistant, as the real flow issues it and <c>/mcp</c> validates it (S07.01.06's
/// access-token criterion, mcp-server.md §4.2, §6.1). Each case gets its token from the flow (the person allows, the
/// assistant exchanges the code) and uses it at <c>/mcp</c>, where <c>probe_whoami</c> reads the principal that the
/// endpoint's own validation made of it, so what is proved is what a real call meets, not a token read outside a
/// request. The app runs on a fake clock, which OpenIddict takes its expiry from, and each case has an app of its own.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class AccessTokenTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    : IClassFixture<Grow2NotesFactory>, IAsyncLifetime
{
    private AssistantFlow flow = null!;
    private Guid managerId;
    private HttpClient browser = null!;

    public async ValueTask InitializeAsync()
    {
        flow = await AssistantFlow.StartAsync(factory, sqlServer, "McpAccessToken");
        managerId = await flow.AddUserAsync(UserRole.Manager);
        browser = await flow.SignInAsync(managerId);
    }

    public async ValueTask DisposeAsync()
    {
        browser.Dispose();
        await flow.DisposeAsync();
    }

    [Fact]
    public async Task
        A_real_access_token_carries_the_mcp_audience_alone_the_person_the_organisation_the_client_the_stamp_hash_and_auth_time_and_no_role()
    {
        var tokens = await flow.ConnectAsync(browser);
        var row = await flow.UserAsync(managerId);

        await using var client = await flow.McpClientAsync(tokens.AccessToken, "2025-11-25");
        var seen = ProbeTools.ReadWhoAmI(await client.CallToolAsync(
            "probe_whoami", cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal([McpTestApp.Resource], Values(seen, Claims.Audience));
        Assert.Equal([managerId.ToString("D")], Values(seen, Claims.Subject));
        Assert.Equal([flow.OrganisationId.ToString("D")], Values(seen, TenantContext.OrganisationIdClaimType));
        Assert.Equal([AssistantFlow.ClientId], Values(seen, Claims.ClientId));
        Assert.Equal([AssistantTokens.StampHash(row.SecurityStamp!)], Values(seen, AssistantTokens.StampHashClaimType));
        var signedInAt = TestClock.Start.ToUnixTimeSeconds().ToString();
        Assert.Equal([signedInAt], Values(seen, Grow2NotesSignInManager.AuthTimeClaimType));

        // The token lasts 10 minutes, which the next case proves to the second.
        Assert.Equal(600, tokens.ExpiresIn);

        // The token has no role, so the only role claim the call has is the one that the endpoint added from the row.
        Assert.Equal([nameof(UserRole.Manager)], Values(seen, Claims.Role));
        Assert.DoesNotContain(seen.Claims, claim => claim.Type is ClaimTypes.Role or "roles");

        Assert.Equal(managerId, seen.UserId);
        Assert.Equal(flow.OrganisationId, seen.OrganisationId);
    }

    [Fact]
    public async Task A_real_access_token_is_accepted_until_10_minutes_after_issue_and_refused_after()
    {
        var tokens = await flow.ConnectAsync(browser);

        flow.Clock.Advance(TimeSpan.FromMinutes(9) + TimeSpan.FromSeconds(59));
        using (var accepted = await flow.PostMcpAsync(tokens.AccessToken))
        {
            Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        }

        flow.Clock.Advance(TimeSpan.FromSeconds(2));
        using var refused = await flow.PostMcpAsync(tokens.AccessToken);
        await AssistantFlow.AssertChallengeAsync(refused, Errors.InvalidToken);
    }

    [Theory]
    [InlineData(TokenEdit.AnotherAudience)]
    [InlineData(TokenEdit.NoStampHash)]
    [InlineData(TokenEdit.AnotherStampHash)]
    [InlineData(TokenEdit.AnotherOrganisation)]
    public async Task A_real_access_token_changed_after_issue_is_refused(TokenEdit edit)
    {
        flow.TokenEdits.Queue(edit);
        var tokens = await flow.ConnectAsync(browser);

        using var response = await flow.PostMcpAsync(tokens.AccessToken);

        await AssistantFlow.AssertChallengeAsync(response, Errors.InvalidToken);
    }

    private static string[] Values(WhoAmI seen, string type) =>
        [.. seen.Claims.Where(claim => claim.Type == type).Select(claim => claim.Value)];
}
