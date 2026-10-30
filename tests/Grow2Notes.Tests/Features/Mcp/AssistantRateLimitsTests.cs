using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform.Audit;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using static Grow2Notes.Tests.Features.Mcp.ConfirmationCalls;
using static Grow2Notes.Tests.Features.Mcp.McpAuditRows;

namespace Grow2Notes.Tests.Features.Mcp;

/// <summary>
/// The rate limits of the AI-assistant access (mcp-server.md §5.6, A139) with the design's numbers, which the test
/// hosts lift for every other test (<see cref="TestRateLimits"/>): calls of <c>/mcp</c> for each user and, with an
/// <c>Origin</c>, for each address; the confirmed invites, resets and deactivations of a user in an hour; the token and
/// revocation endpoints for each approved assistant and overall; and the authorise endpoint and the Connect screen's
/// two for each address. Each fact starts an app of its own with the design's permits back
/// (<c>AssistantFlow.StartAsync(..., limited: true)</c>) and uses keys that no other count has seen (new users,
/// addresses and client IDs), so each count's window starts with that sequence's first request. The framework's
/// windows follow real time, as RateLimitsTests relies on, and a sequence of cheap requests takes a few seconds
/// against the 60 of a window, so there is no retry loop and no number is relaxed. The hour of the confirmed changes
/// is on the app's fake clock, which stands still. The numbers are written out here and not read from the permits.
/// Addresses are the ones that RFC 5737 reserves for documents.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class AssistantRateLimitsTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    : IClassFixture<Grow2NotesFactory>
{
    private const string ForeignOrigin = "https://other.example";

    private const string NoCapabilities = "{}";

    private const string DeclinedAnswer = """{"confirm":{"action":"decline"}}""";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Calls_of_mcp_allow_60_a_minute_for_each_user_and_with_an_origin_for_each_address()
    {
        await using var flow = await StartAsync();
        var tokenA = await ConnectAsync(flow, await flow.AddUserAsync(UserRole.Manager), "192.0.2.1");
        var tokenB = await ConnectAsync(flow, await flow.AddUserAsync(UserRole.Manager), "192.0.2.2");
        using var client = flow.App.CreateAssistantClient();
        var listUsers = AssistantFlow.ToolCall("list_users", new JsonObject());
        var before = await LastEventIdAsync(flow);

        // 59 tools/list calls, which write no audit row, and one list_users, which writes one.
        for (var i = 0; i < 59; i++)
        {
            using var response = await SendMcpAsync(client, tokenA, "198.51.100.10");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        using (var response = await SendMcpAsync(client, tokenA, "198.51.100.10", body: listUsers))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        Assert.Equal(AuditEventTypes.Mcp.Read, Assert.Single(await EventsAfterAsync(flow, before)).EventType);
        var lastEventId = await LastEventIdAsync(flow);

        // The 61st for the user is refused, from the same address and from another: one user, one count.
        using var sixtyFirst = await SendMcpAsync(client, tokenA, "198.51.100.10", body: listUsers);
        await AssertRefusedAsync(sixtyFirst);
        Assert.Empty(await EventsAfterAsync(flow, lastEventId));
        using var otherAddress = await SendMcpAsync(client, tokenA, "198.51.100.11");
        await AssertRefusedAsync(otherAddress);

        // Another user on the first address is counted on their own: the count is for the user, never the address.
        using var otherUser = await SendMcpAsync(client, tokenB, "198.51.100.10");
        Assert.Equal(HttpStatusCode.OK, otherUser.StatusCode);

        // 60 calls with an Origin that no approved assistant has are each refused by the Origin rule, which has read
        // the approved assistants; the 61st is refused before it, and another address is not.
        for (var i = 0; i < 60; i++)
        {
            using var response = await SendMcpAsync(client, token: null, "203.0.113.50", ForeignOrigin);
            await response.ReadProblemAsync(HttpStatusCode.Forbidden);
        }

        using var sixtyFirstWithOrigin = await SendMcpAsync(client, token: null, "203.0.113.50", ForeignOrigin);
        await AssertRefusedAsync(sixtyFirstWithOrigin);
        using var otherAddressWithOrigin = await SendMcpAsync(client, token: null, "203.0.113.51", ForeignOrigin);
        await otherAddressWithOrigin.ReadProblemAsync(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Confirmed_invites_resets_and_deactivations_allow_10_an_hour_for_each_user()
    {
        await using var flow = await StartAsync();
        var tokenA = await ConnectAsync(flow, await flow.AddUserAsync(UserRole.Manager), "192.0.2.1");
        var tokenB = await ConnectAsync(flow, await flow.AddUserAsync(UserRole.Manager), "192.0.2.2");
        var tokenC = await ConnectAsync(flow, await flow.AddUserAsync(UserRole.Manager), "192.0.2.3");
        var workers = new List<string>();
        for (var i = 0; i < 3; i++)
        {
            workers.Add((await flow.UserAsync(await flow.AddUserAsync(UserRole.Worker))).Email!);
        }

        // Previews, a form and a declined form count nothing.
        Assert.Equal("preview", StatusOf(await CallAsync(flow, tokenA, "invite_user", InviteArguments(Fresh()))));
        Assert.Equal(
            "preview",
            StatusOf(await CallAsync(flow, tokenA, "deactivate_user", new() { ["email"] = workers[0] })));
        Assert.Equal(
            "preview",
            StatusOf(await CallAsync(flow, tokenA, "reset_user_sign_in", new() { ["email"] = workers[0] })));
        var declinedInvite = InviteArguments(Fresh());
        var state = await AskAsync(flow, "invite_user", tokenA, declinedInvite, FormCapabilities);
        var (declined, _) = await RawAsync(
            flow, "invite_user", tokenA, declinedInvite, FormCapabilities, state, DeclinedAnswer);
        Assert.Equal("declined", StatusOf(declined));

        // 10 confirmed changes, mixed: 4 invites, 3 resends to the invited, 3 deactivations of workers.
        var invited = new List<string>();
        for (var i = 0; i < 4; i++)
        {
            var email = Fresh();
            invited.Add(email);
            Assert.Equal("done", StatusOf(await CallAsync(flow, tokenA, "invite_user", InviteArguments(email, true))));
            if (i == 3)
            {
                continue;
            }

            Assert.Equal("done", StatusOf(await CallAsync(
                flow, tokenA, "reset_user_sign_in", await ConfirmedArgumentsAsync(flow, email))));
            Assert.Equal("done", StatusOf(await CallAsync(
                flow, tokenA, "deactivate_user", await ConfirmedArgumentsAsync(flow, workers[i]))));
        }

        // The 11th is refused for the hour, which has not moved, before the handler: nothing is made, written or sent.
        var sent = flow.App.Services.GetRequiredService<RecordingEmailSender>().Sent.Count;
        var eleventh = Fresh();
        var (limited, row) = await RawAsync(
            flow, "invite_user", tokenA, InviteArguments(eleventh, true), NoCapabilities);
        AssertRateLimited(limited["structuredContent"]!["error"]!, 3600);
        Assert.True(limited["isError"]!.GetValue<bool>());
        AssertRefused(row, "invite_user", "rate_limited");
        Assert.Null(await FindAsync(flow, eleventh));
        Assert.Equal(sent, flow.App.Services.GetRequiredService<RecordingEmailSender>().Sent.Count);

        // Archiving a participant is not one of the three.
        var participantId = (await flow.AddParticipantAsync(flow.OrganisationId)).ToString();
        var archiveArguments = new JsonObject { ["participantId"] = participantId };
        var preview = await CallAsync(flow, tokenA, "archive_participant", archiveArguments);
        var version = preview["structuredContent"]!["expectedVersion"]!.GetValue<string>();
        var archived = await CallAsync(flow, tokenA, "archive_participant", new JsonObject
        {
            ["participantId"] = participantId,
            ["confirm"] = true,
            ["expectedVersion"] = version,
        });
        Assert.Equal("done", StatusOf(archived));

        // Another manager has a count of their own.
        Assert.Equal("done", StatusOf(await CallAsync(flow, tokenB, "invite_user", InviteArguments(Fresh(), true))));

        // A confirmed call that the handler refuses has used a permit: 10 invites of an address that has an account
        // are refused for it, and the 11th for the hour. So the address cannot be probed without limit.
        var existing = (await flow.UserAsync(await flow.AddUserAsync(UserRole.Worker))).Email!;
        for (var i = 0; i < 10; i++)
        {
            var refused = await CallAsync(flow, tokenC, "invite_user", InviteArguments(existing, true));
            Assert.Equal("user.email_in_use", refused["structuredContent"]!["error"]!["code"]!.GetValue<string>());
        }

        var overTheHour = await CallAsync(flow, tokenC, "invite_user", InviteArguments(existing, true));
        AssertRateLimited(overTheHour["structuredContent"]!["error"]!, 3600);
    }

    [Fact]
    public async Task The_token_and_revocation_endpoints_allow_30_for_each_assistant_10_for_the_rest_and_60_overall()
    {
        await using var flow = await StartAsync();
        const string a = "https://assistant-a.example/client.json";
        const string b = "https://assistant-b.example/client.json";
        const string c = "https://assistant-c.example/client.json";
        foreach (var clientId in new[] { a, b, c })
        {
            await flow.App.RegisterClientAsync(clientId, applicationType: null, "https://assistant.example/callback");
        }

        using var client = flow.App.CreateAssistantClient();

        // 10 requests that no approved assistant sent, in every shape that makes no count of its own, get OpenIddict's
        // own refusals: its answers to a client it does not know, a body that is not a form, a form that cannot be
        // read, and a registered ID in another case or with a trailing space (the manager's second check is exact).
        // Each takes one of the 10 permits of the rest, and then one of the overall 60: 10 so far.
        var shapes = new List<Func<Task<HttpResponseMessage>>>
        {
            () => TokenAsync(client, "https://unknown-1.example/client.json"),
            () => TokenAsync(client, clientId: null),
            () => PostAsync(client, "/oauth/token", new StringContent("{}", Encoding.UTF8, "application/json")),
            () => TokenAsync(client, new string('a', 101)),
            () => PostAsync(client, "/oauth/token", NotAForm()),
            () => TokenAsync(client, b.ToUpperInvariant()),
            () => TokenAsync(client, b + " "),
            () => RevokeAsync(client, "https://unknown-2.example/client.json"),
            () => TokenAsync(client, string.Empty),
            () => TokenAsync(client, "https://unknown-3.example/client.json"),
        };
        foreach (var shape in shapes)
        {
            using var response = await shape();
            Assert.Contains(response.StatusCode, new[] { HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized });
        }

        // The 11th of the rest is refused by its own count and takes no overall permit. B has a count of its own and
        // is still answered, by OpenIddict: 11 so far.
        using var eleventh = await TokenAsync(client, "https://unknown-4.example/client.json");
        await AssertRefusedAsync(eleventh, noStore: false);
        using var firstOfB = await TokenAsync(client, b);
        Assert.Equal(HttpStatusCode.BadRequest, firstOfB.StatusCode);

        // A is answered 30 times and refused the 31st, which takes no overall permit: 41 so far.
        for (var i = 0; i < 30; i++)
        {
            using var response = await TokenAsync(client, a);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        using var thirtyFirstOfA = await TokenAsync(client, a);
        await AssertRefusedAsync(thirtyFirstOfA, noStore: false);

        // 19 revocations for B bring the overall count to 60 (41 + 19), and none is refused. Had the two refused
        // requests taken overall permits, the last two of these would have been.
        for (var i = 0; i < 19; i++)
        {
            using var response = await RevokeAsync(client, b);
            Assert.NotEqual(HttpStatusCode.TooManyRequests, response.StatusCode);
        }

        // C has never called, and is refused by the overall count.
        using var firstOfC = await TokenAsync(client, c);
        await AssertRefusedAsync(firstOfC, noStore: false);
    }

    [Fact]
    public async Task The_authorise_endpoint_and_the_consent_endpoints_allow_30_a_minute_for_each_address()
    {
        await using var flow = await StartAsync();
        using var client = flow.App.CreateAssistantClient();
        string[] queries =
        [
            McpTestApp.AuthorizeQuery(AssistantFlow.ClientId, AssistantFlow.RedirectUri),
            McpTestApp.AuthorizeQuery("https://unknown.example/client.json", AssistantFlow.RedirectUri),
            McpTestApp.AuthorizeQuery(AssistantFlow.ClientId, AssistantFlow.RedirectUri, ("code_challenge", null)),
        ];

        // 30 requests of three kinds from one address: a valid one, which is sent to the Connect screen, an unknown
        // assistant and one with no PKCE challenge, which OpenIddict refuses.
        for (var i = 0; i < 30; i++)
        {
            using var response = await GetAsync(client, "/oauth/authorize?" + queries[i % 3], "203.0.113.20");
            Assert.NotEqual(HttpStatusCode.TooManyRequests, response.StatusCode);
            if (i % 3 == 0)
            {
                Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            }
        }

        using var thirtyFirst = await GetAsync(client, "/oauth/authorize?" + queries[0], "203.0.113.20");
        await AssertRefusedAsync(thirtyFirst, noStore: false);
        using var otherAddress = await GetAsync(client, "/oauth/authorize?" + queries[0], "203.0.113.21");
        Assert.Equal(HttpStatusCode.Redirect, otherAddress.StatusCode);

        // The Connect screen's two endpoints are in the sign-in group, with the passkey options: 29 of them and the
        // GET make 30 from one address, and the POST is the 31st.
        using var browser = await flow.SignInAsync(await flow.AddUserAsync(UserRole.Manager));
        browser.DefaultRequestHeaders.Add(ClientAddressHeader.Name, "203.0.113.22");
        var token = await browser.FetchTokenAsync();
        for (var i = 0; i < 29; i++)
        {
            using var options = await browser.PostWithTokenAsync("/api/auth/passkey/options", token);
            Assert.NotEqual(HttpStatusCode.TooManyRequests, options.StatusCode);
        }

        using var screen = await browser.GetAsync("/api/connect?request=made-up", Ct);
        Assert.NotEqual(HttpStatusCode.TooManyRequests, screen.StatusCode);
        using var decision = await browser.PostWithTokenAsync(
            "/api/connect", token, new { request = "made-up", allow = false });
        await AssertRefusedAsync(decision);
    }

    private Task<AssistantFlow> StartAsync() =>
        AssistantFlow.StartAsync(factory, sqlServer, "AssistantLimits", probes: false, limited: true);

    // The person presses Allow at an address of their own, and the assistant has the access token.
    private static async Task<string> ConnectAsync(AssistantFlow flow, Guid userId, string address)
    {
        using var browser = await flow.SignInAsync(userId);
        browser.DefaultRequestHeaders.Add(ClientAddressHeader.Name, address);
        return (await flow.ConnectAsync(browser)).AccessToken;
    }

    // A call of /mcp from an address, with an Origin if one is given; a tools/list unless a body is.
    private static async Task<HttpResponseMessage> SendMcpAsync(
        HttpClient client, string? token, string address, string? origin = null, string? body = null)
    {
        using var request = AssistantFlow.McpRequest(token, origin, body: body);
        request.Headers.Add(ClientAddressHeader.Name, address);
        return await client.SendAsync(request, Ct);
    }

    private static async Task<HttpResponseMessage> GetAsync(HttpClient client, string path, string address)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add(ClientAddressHeader.Name, address);
        return await client.SendAsync(request, Ct);
    }

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string path, HttpContent content)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = content };
        return await client.SendAsync(request, Ct);
    }

    // A refresh with a token that was never issued, which OpenIddict answers invalid_grant for an approved assistant.
    private static Task<HttpResponseMessage> TokenAsync(HttpClient client, string? clientId) =>
        PostAsync(client, "/oauth/token", Form(
            ("grant_type", "refresh_token"), ("refresh_token", "made-up"), ("client_id", clientId)));

    private static Task<HttpResponseMessage> RevokeAsync(HttpClient client, string clientId) =>
        PostAsync(client, "/oauth/revoke", Form(
            ("token", "made-up"), ("token_type_hint", "refresh_token"), ("client_id", clientId)));

    private static FormUrlEncodedContent Form(params (string Name, string? Value)[] fields) =>
        new(fields
            .Where(field => field.Value is not null)
            .Select(field => KeyValuePair.Create(field.Name, field.Value!)));

    // A form content type with no boundary, which cannot be read.
    private static StringContent NotAForm()
    {
        var content = new StringContent("made-up");
        content.Headers.ContentType = MediaTypeHeaderValue.Parse("multipart/form-data");
        return content;
    }

    // A call as the raw 2026-07-28 client of a person who shows no forms, so confirm: true confirms.
    private static Task<JsonObject> CallAsync(AssistantFlow flow, string token, string tool, JsonObject arguments) =>
        flow.PostToolCallAsync(token, tool, arguments, JsonNode.Parse(NoCapabilities)!.AsObject());

    private static string StatusOf(JsonObject result)
    {
        Assert.Null(result["isError"]);
        return result["structuredContent"]!["status"]!.GetValue<string>();
    }

    private static void AssertRateLimited(JsonNode error, int retryAfterSeconds)
    {
        Assert.Equal("rate_limited", error["code"]!.GetValue<string>());
        Assert.Null(error["fields"]);
        Assert.Equal(retryAfterSeconds, error["retryAfterSeconds"]!.GetValue<int>());
    }

    private static string Fresh() => $"invitee.{Guid.NewGuid():N}@example.test";

    private static JsonObject InviteArguments(string email, bool confirm = false)
    {
        var arguments = new JsonObject { ["email"] = email, ["displayName"] = "Invitee Lee", ["role"] = "worker" };
        if (confirm)
        {
            arguments["confirm"] = true;
        }

        return arguments;
    }

    // The email, confirm: true and the version of the account as it is now.
    private static async Task<JsonObject> ConfirmedArgumentsAsync(AssistantFlow flow, string email) => new()
    {
        ["email"] = email,
        ["confirm"] = true,
        ["expectedVersion"] = (await FindAsync(flow, email))!.ConcurrencyStamp,
    };

    private static async Task<ApplicationUser?> FindAsync(AssistantFlow flow, string email)
    {
        await using var scope = flow.App.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByEmailAsync(email);
    }

    // What every refusal is: a 429 that says to wait the 60 seconds of the window, as problem details. Only /mcp and
    // /api answers are never cached, so the other endpoints' have no Cache-Control.
    private static async Task AssertRefusedAsync(HttpResponseMessage response, bool noStore = true)
    {
        await response.ReadProblemAsync(HttpStatusCode.TooManyRequests);
        Assert.Equal(TimeSpan.FromSeconds(60), response.Headers.RetryAfter?.Delta);
        if (noStore)
        {
            Assert.True(response.Headers.NonValidated.TryGetValues("Cache-Control", out var cacheControl));
            Assert.Equal("no-store", cacheControl.ToString());
        }
    }
}
