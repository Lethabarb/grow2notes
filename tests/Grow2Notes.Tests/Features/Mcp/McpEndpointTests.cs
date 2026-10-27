using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Features.Mcp;
using Grow2Notes.Web.Platform;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Grow2Notes.Tests.Features.Mcp;

/// <summary>
/// <c>POST /mcp</c> with the access on (mcp-server.md §3.2, §4.2, §4.5, §4.7, S07.02.01): the challenge and the
/// protected-resource document, the sign-in kept apart from the session cookie, the <c>Origin</c> rule, both protocol
/// revisions, and the checks on each call that follow the person: the role and organisation come from the row and the
/// token, never from the token alone. Every case calls the real endpoint with a token from the real flow; the tools
/// are <see cref="ProbeTools"/> and the app's own, and the app's own alone where the case asks for production's setup.
/// Each case has an app of its own on a fake clock.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class McpEndpointTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    : IClassFixture<Grow2NotesFactory>, IAsyncLifetime
{
    private const string AssistantOrigin = "https://assistant.example";

    private AssistantFlow flow = null!;
    private Guid managerId;
    private HttpClient browser = null!;

    public static TheoryData<string> Revisions { get; } = ["2025-11-25", "2026-07-28"];

    public static TheoryData<string> ChangesToThePerson { get; } = ["deactivated", "stamp changed", "made a worker"];

    public static TheoryData<string> MethodsThatAreRefused { get; } = ["GET", "DELETE"];

    // "none" sends no Origin header, and "two" sends the approved assistant's twice.
    public static MatrixTheoryData<string, bool> AcceptedOrigins { get; } =
        new(["none", AssistantOrigin], [true, false]);

    public static MatrixTheoryData<string, bool> RefusedOrigins { get; } = new(
        ["https://other.example", "null", "http://127.0.0.1:3000", "two"], [true, false]);

    public static MatrixTheoryData<string, string> UnmappedPaths { get; } =
        new(["GET", "POST"], ["/mcp/sse", "/mcp/message", "/mcp/anything"]);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        flow = await AssistantFlow.StartAsync(factory, sqlServer, "McpEndpoint");
        managerId = await flow.AddUserAsync(UserRole.Manager);
        browser = await flow.SignInAsync(managerId);
    }

    public async ValueTask DisposeAsync()
    {
        browser.Dispose();
        await flow.DisposeAsync();
    }

    [Fact]
    public async Task Without_a_token_mcp_answers_401_with_the_challenge_naming_the_document_and_the_scope()
    {
        using var response = await flow.PostMcpAsync(accessToken: null);

        await AssistantFlow.AssertChallengeAsync(response);
    }

    [Fact]
    public async Task A_bad_token_answers_401_with_invalid_token()
    {
        using var response = await flow.PostMcpAsync("this-is-not-a-token");

        await AssistantFlow.AssertChallengeAsync(response, Errors.InvalidToken);
    }

    [Fact]
    public async Task The_protected_resource_document_names_the_mcp_resource_and_the_issuer_alone()
    {
        using var client = flow.App.CreateAssistantClient();

        using var response = await client.GetAsync("/.well-known/oauth-protected-resource", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal(4, document.EnumerateObject().Count());
        Assert.Equal(McpTestApp.Resource, document.GetProperty("resource").GetString());
        Assert.Equal(["grow2notes.admin"], Strings(document, "scopes_supported"));
        Assert.Equal(["header"], Strings(document, "bearer_methods_supported"));

        using var discovery = await client.GetAsync("/.well-known/oauth-authorization-server", Ct);
        var issuer = (await discovery.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("issuer").GetString();
        Assert.Equal([issuer], Strings(document, "authorization_servers"));
        Assert.Equal(AssistantFlow.Issuer, issuer);

        using var withThePath = await client.GetAsync("/.well-known/oauth-protected-resource/mcp", Ct);
        await withThePath.ReadProblemAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task The_session_cookie_is_refused_at_mcp()
    {
        using var request = AssistantFlow.McpRequest(accessToken: null);

        using var response = await browser.SendAsync(request, Ct);

        await AssistantFlow.AssertChallengeAsync(response);
    }

    [Fact]
    public async Task A_bearer_token_is_ignored_at_api()
    {
        var tokens = await flow.ConnectAsync(browser);
        using var client = flow.App.CreateAssistantClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.Authorization = new("Bearer", tokens.AccessToken);

        using var response = await client.SendAsync(request, Ct);

        // Refused as any request without a session is, and not challenged for a token as /mcp is.
        await response.ReadProblemAsync(HttpStatusCode.Unauthorized);
        Assert.DoesNotContain("resource_metadata", string.Join(" ", response.Headers.WwwAuthenticate));
        using var withTheSession = await browser.GetAsync("/api/auth/me", Ct);
        Assert.Equal(HttpStatusCode.OK, withTheSession.StatusCode);
    }

    [Theory]
    [MemberData(nameof(UnmappedPaths))]
    public async Task Other_mcp_paths_answer_404_with_the_switch_on(string method, string path)
    {
        using var client = flow.App.CreateAssistantClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), path);

        using var response = await client.SendAsync(request, Ct);

        await response.ReadProblemAsync(HttpStatusCode.NotFound);
    }

    [Theory]
    [MemberData(nameof(AcceptedOrigins))]
    public async Task No_origin_or_the_origin_of_an_approved_assistant_passes_the_origin_rule(
        string origin, bool withToken)
    {
        var token = withToken ? (await flow.ConnectAsync(browser)).AccessToken : null;

        using var response = await SendAsync(token, origin);

        if (withToken)
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        else
        {
            await AssistantFlow.AssertChallengeAsync(response);
        }
    }

    [Theory]
    [MemberData(nameof(RefusedOrigins))]
    public async Task Any_other_origin_gets_403_before_the_token_is_read(string origin, bool withToken)
    {
        var token = withToken ? (await flow.ConnectAsync(browser)).AccessToken : null;

        using var response = await SendAsync(token, origin);

        await response.ReadProblemAsync(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task No_response_from_mcp_carries_cors_headers()
    {
        var tokens = await flow.ConnectAsync(browser);
        var responses = new List<HttpResponseMessage>();

        try
        {
            responses.Add(await SendAsync(tokens.AccessToken, "none"));
            responses.Add(await SendAsync(token: null, "none"));
            responses.Add(await SendAsync(tokens.AccessToken, "https://other.example"));
            responses.Add(await flow.PostMcpAsync(tokens.AccessToken, AssistantOrigin, HttpMethod.Options));

            Assert.Equal(
                [HttpStatusCode.OK, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.NotFound],
                responses.Select(response => response.StatusCode));
            Assert.All(responses, response => Assert.DoesNotContain(
                response.Headers.Concat(response.Content.Headers),
                header => header.Key.StartsWith("Access-Control-", StringComparison.OrdinalIgnoreCase)));
        }
        finally
        {
            responses.ForEach(response => response.Dispose());
        }
    }

    [Theory]
    [MemberData(nameof(Revisions))]
    public async Task Clients_on_2025_11_25_and_2026_07_28_both_list_tools_and_get_the_server_name_and_instructions(
        string revision)
    {
        var tokens = await flow.ConnectAsync(browser);

        await using var client = await flow.McpClientAsync(tokens.AccessToken, revision);

        Assert.Equal("Grow2Notes", client.ServerInfo.Name);
        Assert.Equal("2.0", client.ServerInfo.Version);
        Assert.Equal(McpSetup.ServerInstructions, client.ServerInstructions);
        var tools = await client.ListToolsAsync(cancellationToken: Ct);
        Assert.Contains(tools, tool => tool.Name == "probe_whoami");
    }

    [Theory]
    [MemberData(nameof(Revisions))]
    public async Task The_production_setup_lists_the_snapshot_tools_on_either_revision(string revision)
    {
        await using var production = await AssistantFlow.StartAsync(factory, sqlServer, "McpProduction", probes: false);
        using var productionBrowser = await production.SignInAsync(await production.AddUserAsync(UserRole.Manager));
        var tokens = await production.ConnectAsync(productionBrowser);

        await using var client = await production.McpClientAsync(tokens.AccessToken, revision);

        Assert.NotNull(client.ServerCapabilities.Tools);
        var listed = await client.ListToolsAsync(cancellationToken: Ct);

        // What a model is told is reviewed in the diff of tools-list.json: a tool, a title, a description or a schema
        // changes only with that file.
        var actual = new JsonArray([.. listed.OrderBy(tool => tool.Name, StringComparer.Ordinal).Select(Project)]);
        var expected = JsonNode.Parse(await File.ReadAllTextAsync(
            Path.Combine(Repository.Root(), "tests", "Grow2Notes.Tests", "Features", "Mcp", "tools-list.json"), Ct));
        Assert.True(
            JsonNode.DeepEquals(expected, actual),
            "The tools production lists are not those of tools-list.json. If the change is meant, the file is:\n"
                + actual.ToJsonString(new() { WriteIndented = true }));
    }

    [Theory]
    [MemberData(nameof(MethodsThatAreRefused))]
    public async Task Get_and_delete_answer_405_with_allow_post(string method)
    {
        var tokens = await flow.ConnectAsync(browser);

        using var response = await flow.PostMcpAsync(tokens.AccessToken, method: new HttpMethod(method));

        await response.ReadProblemAsync(HttpStatusCode.MethodNotAllowed);
        Assert.Equal(["POST"], response.Content.Headers.Allow);
    }

    [Fact]
    public async Task A_header_mismatch_gets_the_sdks_400()
    {
        var tokens = await flow.ConnectAsync(browser);
        const string body = """
            {"jsonrpc":"2.0","id":1,"method":"tools/list","params":{"_meta":{
            "io.modelcontextprotocol/protocolVersion":"2026-07-28",
            "io.modelcontextprotocol/clientCapabilities":{}}}}
            """;
        using var client = flow.App.CreateAssistantClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new("Bearer", tokens.AccessToken);
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.Accept.ParseAdd("text/event-stream");
        request.Headers.Add("MCP-Protocol-Version", "2026-07-28");
        request.Headers.Add("Mcp-Method", "tools/call");

        using var response = await client.SendAsync(request, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(ChangesToThePerson))]
    public async Task A_token_of_a_person_who_changed_is_refused_and_their_authorisations_revoked(string change)
    {
        var tokens = await flow.ConnectAsync(browser);
        Assert.Equal([Statuses.Valid], await flow.AuthorisationStatusesAsync(managerId));

        // These change the row directly and revoke nothing, so that the revoking can only be the call's.
        await (change switch
        {
            "deactivated" => flow.DeactivateAsync(managerId),
            "stamp changed" => flow.ResetSignInAsync(managerId),
            "made a worker" => flow.ChangeRoleAsync(managerId, UserRole.Worker),
            _ => throw new ArgumentOutOfRangeException(nameof(change)),
        });
        Assert.Equal([Statuses.Valid], await flow.AuthorisationStatusesAsync(managerId));

        using var response = await flow.PostMcpAsync(tokens.AccessToken);

        await AssistantFlow.AssertChallengeAsync(response, Errors.InvalidToken);
        Assert.Equal([Statuses.Revoked], await flow.AuthorisationStatusesAsync(managerId));
    }

    [Fact]
    public async Task A_manager_changed_to_support_in_the_app_is_seen_as_support_on_the_next_call()
    {
        var tokens = await flow.ConnectAsync(browser);
        using var another = await flow.SignInAsync(await flow.AddUserAsync(UserRole.Manager));
        var row = await flow.UserAsync(managerId);

        using (var edit = await another.PutWithTokenAsync(
            $"/api/admin/users/{managerId}",
            await another.FetchTokenAsync(),
            new { displayName = row.DisplayName, email = row.Email, role = nameof(UserRole.Support) },
            IfMatch.ETag(row.ConcurrencyStamp!)))
        {
            Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
        }

        var seen = await WhoAmIAsync(tokens.AccessToken);
        Assert.Equal([nameof(UserRole.Support)], seen.Values(Claims.Role, ClaimTypes.Role));
        Assert.Equal([Statuses.Valid], await flow.AuthorisationStatusesAsync(managerId));
    }

    [Fact]
    public async Task Each_call_runs_in_the_token_organisation()
    {
        var participantOfTheFirst = await flow.AddParticipantAsync(flow.OrganisationId);
        var second = await flow.AddOrganisationAsync();
        var participantOfTheSecond = await flow.AddParticipantAsync(second);
        using var browserOfTheSecond = await flow.SignInAsync(await flow.AddUserAsync(UserRole.Manager, second));
        var tokensOfTheFirst = await flow.ConnectAsync(browser);
        var tokensOfTheSecond = await flow.ConnectAsync(browserOfTheSecond);

        var first = await WhoAmIAsync(tokensOfTheFirst.AccessToken);
        var other = await WhoAmIAsync(tokensOfTheSecond.AccessToken);

        Assert.Equal(flow.OrganisationId, first.OrganisationId);
        Assert.Equal([participantOfTheFirst], first.ParticipantIds);
        Assert.Equal(second, other.OrganisationId);
        Assert.Equal([participantOfTheSecond], other.ParticipantIds);
    }

    [Fact]
    public async Task A_call_past_12_hours_from_sign_in_is_refused_without_revoking()
    {
        var tokens = await flow.ConnectAsync(browser);
        var limit = TestClock.Start + TimeSpan.FromHours(12);
        tokens = await flow.RefreshEveryAsync(tokens, limit);

        // The last refresh is a minute within the limit, so the token it gives is still valid when the call comes after
        // the limit: only the check on the person's session can refuse it.
        flow.Clock.Advance(limit - TimeSpan.FromMinutes(1) - flow.Clock.GetUtcNow());
        using (var refresh = await flow.RefreshAsync(tokens.RefreshToken))
        {
            tokens = await AssistantFlow.ReadTokensAsync(refresh);
        }

        using (var within = await flow.PostMcpAsync(tokens.AccessToken))
        {
            Assert.Equal(HttpStatusCode.OK, within.StatusCode);
        }

        flow.Clock.Advance(TimeSpan.FromMinutes(2));
        using var past = await flow.PostMcpAsync(tokens.AccessToken);

        await AssistantFlow.AssertChallengeAsync(past, Errors.InvalidToken);
        Assert.Equal([Statuses.Valid], await flow.AuthorisationStatusesAsync(managerId));
    }

    [Fact]
    public async Task A_token_whose_authorisation_was_revoked_is_refused()
    {
        var tokens = await flow.ConnectAsync(browser);
        using (var before = await flow.PostMcpAsync(tokens.AccessToken))
        {
            Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        }

        await flow.RevokeAuthorisationsAsync(managerId);

        using var response = await flow.PostMcpAsync(tokens.AccessToken);

        await AssistantFlow.AssertChallengeAsync(response, Errors.InvalidToken);
        Assert.Equal([Statuses.Revoked], await flow.AuthorisationStatusesAsync(managerId));
    }

    [Fact]
    public async Task A_token_whose_registration_was_removed_is_refused()
    {
        var tokens = await flow.ConnectAsync(browser);
        var removal = await flow.App.RunAdminCommandAsync(["mcp-client", "remove", AssistantFlow.ClientId]);
        Assert.Equal(0, removal.ExitCode);

        using var response = await flow.PostMcpAsync(tokens.AccessToken);

        await AssistantFlow.AssertChallengeAsync(response, Errors.InvalidToken);
    }

    [Fact]
    public async Task No_tool_argument_result_or_token_is_logged()
    {
        var tokens = await flow.ConnectAsync(browser);
        var canary = Guid.NewGuid().ToString("N");

        await using var client = await flow.McpClientAsync(tokens.AccessToken, "2025-11-25");
        var seen = ProbeTools.ReadWhoAmI(await client.CallToolAsync(
            "probe_whoami", new Dictionary<string, object?> { ["echo"] = canary }, cancellationToken: Ct));

        // The canary went in as an argument and came back in the result.
        Assert.Equal(canary, seen.Echo);
        foreach (var secret in new[] { canary, tokens.AccessToken, tokens.RefreshToken })
        {
            Assert.DoesNotContain(flow.Logs.Entries, entry => entry.Holds(secret));
        }
    }

    [Fact]
    public async Task The_mcp_server_makes_no_outbound_call()
    {
        var tokens = await flow.ConnectAsync(browser);

        await using var client = await flow.McpClientAsync(tokens.AccessToken, "2025-11-25");
        await client.ListToolsAsync(cancellationToken: Ct);
        ProbeTools.ReadWhoAmI(await client.CallToolAsync("probe_whoami", cancellationToken: Ct));

        Assert.Empty(flow.Outbound.Requests);
    }

    // The parts of a tool that a model reads, as the wire has them. Its icons, execution and _meta are left out.
    private static JsonObject Project(McpClientTool tool)
    {
        var wire = JsonSerializer.SerializeToNode(tool.ProtocolTool, McpJsonUtilities.DefaultOptions)!.AsObject();
        var projected = new JsonObject();
        foreach (var key in new[] { "name", "title", "description", "inputSchema", "outputSchema", "annotations" })
        {
            if (wire[key] is { } value)
            {
                projected[key] = value.DeepClone();
            }
        }

        return projected;
    }

    private static string[] Strings(JsonElement document, string name) =>
        [.. document.GetProperty(name).EnumerateArray().Select(value => value.GetString()!)];

    // An origin of "none" sends no Origin header, and "two" sends the approved assistant's origin in two.
    private async Task<HttpResponseMessage> SendAsync(string? token, string origin)
    {
        using var client = flow.App.CreateAssistantClient();
        using var request = AssistantFlow.McpRequest(
            token, origin switch { "none" => null, "two" => AssistantOrigin, _ => origin });
        if (origin == "two")
        {
            request.Headers.TryAddWithoutValidation("Origin", AssistantOrigin);
        }

        return await client.SendAsync(request, Ct);
    }

    private async Task<WhoAmI> WhoAmIAsync(string accessToken)
    {
        await using var client = await flow.McpClientAsync(accessToken, "2025-11-25");
        return ProbeTools.ReadWhoAmI(await client.CallToolAsync("probe_whoami", cancellationToken: Ct));
    }
}
