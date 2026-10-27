using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Features.Auth;
using Grow2Notes.Web.Features.Mcp;
using Grow2Notes.Web.Features.Mcp.OAuth;
using Grow2Notes.Web.Platform;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Grow2Notes.Tests.Features.Mcp;

/// <summary>
/// The code exchange at <c>POST /oauth/token</c> (mcp-server.md §4.2): what an assistant is given for the code that
/// the person's Allow issued, what its access token holds, and what OpenIddict and the app refuse. The app runs with
/// the access on, on a database of its own and a fake clock. The endpoint has no row in the endpoint matrix, as it
/// exists only where the access is on (A110); these tests call it as an assistant does.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class TokenTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    : IClassFixture<Grow2NotesFactory>, IAsyncLifetime
{
    private AssistantFlow flow = null!;
    private Guid managerId;
    private HttpClient browser = null!;

    public static TheoryData<string> RequestedScopes { get; } = ["both", "admin alone", "none"];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        flow = await AssistantFlow.StartAsync(factory, sqlServer, "McpToken");
        managerId = await flow.AddUserAsync(UserRole.Manager);
        browser = await flow.SignInAsync(managerId);
    }

    public async ValueTask DisposeAsync()
    {
        browser.Dispose();
        await flow.DisposeAsync();
    }

    [Theory]
    [MemberData(nameof(RequestedScopes))]
    public async Task The_code_is_exchanged_for_a_bearer_token_that_lasts_10_minutes_and_a_refresh_token(string requested)
    {
        string? scope = requested switch
        {
            "both" => $"{McpSetup.AdminScope} {Scopes.OfflineAccess}",
            "admin alone" => McpSetup.AdminScope,
            _ => null,
        };
        var code = await flow.AllowAsync(
            browser, McpTestApp.AuthorizeQuery(AssistantFlow.ClientId, AssistantFlow.RedirectUri, ("scope", scope)));

        using var response = await flow.ExchangeAsync(code);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("Bearer", body.GetProperty("token_type").GetString());
        Assert.Equal(600, body.GetProperty("expires_in").GetInt32());
        Assert.NotEmpty(body.GetProperty("refresh_token").GetString()!);
        Assert.NotEmpty(body.GetProperty("access_token").GetString()!);
        Assert.Contains(McpSetup.AdminScope, body.GetProperty("scope").GetString()!.Split(' '));
    }

    [Fact]
    public async Task The_access_token_names_the_person_and_the_resource_and_carries_no_role()
    {
        var tokens = await flow.ConnectAsync(browser);

        var principal = await flow.App.ReadAsync(tokens.AccessToken);

        Assert.Equal([McpTestApp.Resource], principal.GetAudiences().ToArray());
        Assert.Equal([AssistantFlow.ClientId], principal.GetPresenters().ToArray());
        Assert.Equal(managerId.ToString("D"), principal.GetClaim(Claims.Subject));
        Assert.NotEmpty(principal.GetClaim(TenantContext.OrganisationIdClaimType)!);
        Assert.NotEmpty(principal.GetClaim(AssistantTokens.StampHashClaimType)!);
        Assert.Equal(
            TestClock.Start.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
            principal.GetClaim(Grow2NotesSignInManager.AuthTimeClaimType));
        Assert.DoesNotContain(principal.Claims, claim => claim.Type is ClaimTypes.Role or Claims.Role);
        Assert.Equal(TimeSpan.FromMinutes(10), principal.GetExpirationDate() - principal.GetCreationDate());
    }

    [Fact]
    public async Task A_code_exchange_with_no_resource_is_an_invalid_request()
    {
        var code = await flow.AllowAsync(browser);

        using var response = await flow.ExchangeAsync(code, withResource: false);

        Assert.Equal(Errors.InvalidRequest, await AssistantFlow.ErrorAsync(response));
    }

    [Fact]
    public async Task A_code_that_has_been_exchanged_is_refused_the_second_time()
    {
        var code = await flow.AllowAsync(browser);
        using var first = await flow.ExchangeAsync(code);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        using var second = await flow.ExchangeAsync(code);

        Assert.Equal(Errors.InvalidGrant, await AssistantFlow.ErrorAsync(second));
    }

    [Fact]
    public async Task A_client_secret_is_an_invalid_client()
    {
        var code = await flow.AllowAsync(browser);

        using var response = await flow.ExchangeAsync(code, clientSecret: "a-secret");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(Errors.InvalidClient, await AssistantFlow.ErrorAsync(response));
    }

    [Fact]
    public async Task A_person_deactivated_between_Allow_and_the_exchange_is_refused_and_their_authorisations_revoked()
    {
        var code = await flow.AllowAsync(browser);
        await flow.DeactivateAsync(managerId);

        using var response = await flow.ExchangeAsync(code);

        Assert.Equal(Errors.InvalidGrant, await AssistantFlow.ErrorAsync(response));
        var statuses = await flow.AuthorisationStatusesAsync(managerId);
        Assert.NotEmpty(statuses);
        Assert.All(statuses, status => Assert.Equal(Statuses.Revoked, status));
    }
}
