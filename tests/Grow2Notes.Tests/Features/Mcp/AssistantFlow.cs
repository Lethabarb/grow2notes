using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Features.Mcp;
using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using ModelContextProtocol.Client;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Grow2Notes.Tests.Features.Mcp;

/// <summary>
/// An approved assistant connecting to the app, and using its tokens, for the tests of the token endpoint and of
/// <c>/mcp</c> (mcp-server.md §4.2, §4.5): the app with the access on, on a database of its own and a fake clock, with
/// the people that connect it signed in by the real session cookie. Each step is what the assistant or its person
/// does: the person presses Allow, the assistant exchanges the code, then refreshes, and calls <c>/mcp</c> with the
/// access token. tests/README.md, <i>Calling /mcp in a test</i>, says how.
/// </summary>
internal sealed partial class AssistantFlow : IAsyncDisposable
{
    public const string ClientId = "https://assistant.example/client.json";
    public const string RedirectUri = "https://assistant.example/callback";

    /// <summary>The authorisation server's issuer, which the protected-resource document names.</summary>
    public const string Issuer = Grow2NotesFactory.Origin + "/";

    private const string ToolsListBody = """{"jsonrpc":"2.0","id":1,"method":"tools/list"}""";

    private static readonly TimeSpan Wait = TimeSpan.FromMinutes(29);

    private readonly WebApplicationFactory<Program> databaseOfItsOwn;
    private Guid organisationId;

    private AssistantFlow(
        WebApplicationFactory<Program> databaseOfItsOwn,
        WebApplicationFactory<Program> app,
        FakeTimeProvider clock,
        RecordingLoggerProvider logs,
        RecordingHttpHandler outbound)
    {
        this.databaseOfItsOwn = databaseOfItsOwn;
        App = app;
        Clock = clock;
        Logs = logs;
        Outbound = outbound;
    }

    public WebApplicationFactory<Program> App { get; }

    public FakeTimeProvider Clock { get; }

    /// <summary>Every log entry that the app's logging levels let through.</summary>
    public RecordingLoggerProvider Logs { get; }

    /// <summary>
    /// Every outbound call that the app makes through <see cref="IHttpClientFactory"/>, which fails on any.
    /// </summary>
    public RecordingHttpHandler Outbound { get; }

    /// <summary>The organisation that <see cref="AddUserAsync"/> adds a person to unless it is given another.</summary>
    public Guid OrganisationId => organisationId;

    /// <summary>The edit that the next access token the app issues is given (<see cref="IssuedTokenEdits"/>).</summary>
    public IssuedTokenEdits TokenEdits => App.Services.GetRequiredService<IssuedTokenEdits>();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string ValidQuery => McpTestApp.AuthorizeQuery(ClientId, RedirectUri);

    /// <summary>
    /// Starts the app with the access on, an organisation and the registered assistant. With <paramref name="probes"/>
    /// the app also has <see cref="ProbeTools"/>, which no real app has; without it the app has the tools production
    /// has.
    /// </summary>
    public static async Task<AssistantFlow> StartAsync(
        Grow2NotesFactory factory, SqlServerFixture sqlServer, string name, bool probes = true)
    {
        var databaseOfItsOwn = await factory.OnDatabaseOfItsOwnAsync(sqlServer, name);
        var clock = new FakeTimeProvider(TestClock.Start);
        var logs = new RecordingLoggerProvider();
        var outbound = new RecordingHttpHandler();
        var app = databaseOfItsOwn.WithWebHostBuilder(builder => builder
            .MapCookieSignIn()
            .UseClock(clock)
            .ConfigureTestServices(services =>
            {
                // Every outbound call of the app goes here, and the handler fails on any.
                services.ConfigureAll<HttpClientFactoryOptions>(options =>
                    options.HttpMessageHandlerBuilderActions.Add(handler => handler.PrimaryHandler = outbound));
                services.AddSingleton<ILoggerProvider>(logs);
                services.AddClientAddressHeader();
                services.AddOpenIddict().AddServer(server => server.AddEventHandler(IssuedTokenEdits.Descriptor));
                if (probes)
                {
                    services.AddMcpServer().WithTools<ProbeTools>();
                }
            }));
        await app.RegisterClientAsync(ClientId, applicationType: null, RedirectUri);

        var flow = new AssistantFlow(databaseOfItsOwn, app, clock, logs, outbound);
        flow.organisationId = await flow.AddOrganisationAsync();
        return flow;
    }

    public static async Task<AssistantTokenSet> ReadTokensAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        return new(
            body.GetProperty("access_token").GetString()!,
            body.GetProperty("refresh_token").GetString()!,
            body.GetProperty("expires_in").GetInt32());
    }

    /// <summary>
    /// The OpenIddict error code that <paramref name="response"/> carries, once it has the status OpenIddict gives that
    /// error: a 400, but a 401 for <c>invalid_client</c>.
    /// </summary>
    public static async Task<string?> ErrorAsync(
        HttpResponseMessage response, HttpStatusCode status = HttpStatusCode.BadRequest)
    {
        Assert.Equal(status, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        return body.GetProperty("error").GetString();
    }

    /// <summary>
    /// The JSON-RPC body of a <c>tools/call</c> of <paramref name="name"/> with <paramref name="arguments"/>.
    /// </summary>
    public static string ToolCall(string name, JsonObject arguments) =>
        new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = 1,
            ["method"] = "tools/call",
            ["params"] = new JsonObject { ["name"] = name, ["arguments"] = arguments.DeepClone() },
        }.ToJsonString();

    /// <summary>
    /// The request an assistant makes of <c>/mcp</c>: by default a <c>POST</c> of a <c>tools/list</c> call that the
    /// stateless server answers on its own, or of <paramref name="body"/> if it is given (<see cref="ToolCall"/>), with
    /// <paramref name="accessToken"/> as a bearer token if there is one, and <paramref name="origin"/> as its
    /// <c>Origin</c> header if there is one.
    /// </summary>
    public static HttpRequestMessage McpRequest(
        string? accessToken, string? origin = null, HttpMethod? method = null, string? body = null)
    {
        var request = new HttpRequestMessage(method ?? HttpMethod.Post, "/mcp");
        if (request.Method == HttpMethod.Post)
        {
            request.Headers.Accept.ParseAdd("application/json");
            request.Headers.Accept.ParseAdd("text/event-stream");
            request.Content = new StringContent(body ?? ToolsListBody, Encoding.UTF8, "application/json");
        }

        if (accessToken is not null)
        {
            request.Headers.Authorization = new("Bearer", accessToken);
        }

        if (origin is not null)
        {
            request.Headers.TryAddWithoutValidation("Origin", origin);
        }

        return request;
    }

    /// <summary>
    /// Asserts that <paramref name="response"/> is the 401 challenge of <c>/mcp</c> (mcp-server.md §4.2): a problem
    /// that is never stored, with a <c>Bearer</c> challenge that names the protected-resource document at the issuer
    /// and the one scope, and that carries <c>error="<paramref name="error"/>"</c>, or no <c>error</c> when it is null:
    /// a request with no token says nothing of one.
    /// </summary>
    public static async Task AssertChallengeAsync(HttpResponseMessage response, string? error = null)
    {
        await response.ReadProblemAsync(HttpStatusCode.Unauthorized);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());

        var header = string.Join(", ", response.Headers.GetValues("WWW-Authenticate"));
        Assert.StartsWith("Bearer ", header, StringComparison.Ordinal);
        var parameters = ChallengeParameter().Matches(header).ToDictionary(
            match => match.Groups["name"].Value, match => match.Groups["value"].Value);
        Assert.Equal(Issuer + ".well-known/oauth-protected-resource", parameters["resource_metadata"]);
        Assert.Equal(McpSetup.AdminScope, parameters["scope"]);
        Assert.Equal(error, parameters.GetValueOrDefault("error"));
    }

    public async ValueTask DisposeAsync()
    {
        await App.DisposeAsync();
        await databaseOfItsOwn.DisposeAsync();
    }

    // An organisation is the tenant, not tenant-owned, so this needs no tenant.
    public async Task<Guid> AddOrganisationAsync()
    {
        await using var scope = App.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        var organisation = new Organisation
        {
            Name = "Token test",
            GuidePrompts = "",
            CreatedAtUtc = new DateTime(2026, 10, 18, 0, 0, 0, DateTimeKind.Utc),
        };
        db.Organisations.Add(organisation);
        await db.SaveChangesAsync(Ct);
        return organisation.Id;
    }

    // AspNetUsers is not filtered by tenant, so this needs none.
    public async Task<Guid> AddUserAsync(UserRole role, Guid? inOrganisation = null)
    {
        var addedAtUtc = new DateTime(2026, 10, 18, 0, 0, 0, DateTimeKind.Utc);
        var email = $"{Guid.NewGuid():N}@example.org";
        var user = new ApplicationUser
        {
            OrganisationId = inOrganisation ?? organisationId,
            DisplayName = "Sam Lee",
            Role = role,
            Status = UserStatus.Active,
            InvitedAtUtc = addedAtUtc,
            ActivatedAtUtc = addedAtUtc,
            UserName = email,
            Email = email,
            EmailConfirmed = true,
        };
        await using var scope = App.Services.CreateAsyncScope();
        var created = await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().CreateAsync(user);
        Assert.True(created.Succeeded, string.Join(" ", created.Errors.Select(error => error.Code)));
        return user.Id;
    }

    /// <summary>The row of <paramref name="userId"/> as it is now.</summary>
    public async Task<ApplicationUser> UserAsync(Guid userId)
    {
        await using var scope = App.Services.CreateAsyncScope();
        var user = await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>()
            .FindByIdAsync(userId.ToString("D"));
        return Assert.IsType<ApplicationUser>(user);
    }

    /// <summary>Adds a participant to <paramref name="inOrganisation"/>, under its tenant.</summary>
    public async Task<Guid> AddParticipantAsync(Guid inOrganisation)
    {
        await using var scope = App.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        var participant = new Participant
        {
            OrganisationId = inOrganisation,
            GivenName = "Jane",
            FamilyName = "Citizen",
            DateOfBirth = new DateOnly(1987, 3, 7),
            CreatedAtUtc = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
            Status = ParticipantStatus.Active,
        };
        db.Participants.Add(participant);
        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(inOrganisation))
        {
            await db.SaveChangesAsync(Ct);
        }

        return participant.Id;
    }

    /// <summary>A browser signed in as <paramref name="userId"/> at the clock's time now.</summary>
    public async Task<HttpClient> SignInAsync(Guid userId)
    {
        var client = App.CreateAssistantClient();
        await client.SignInWithCookieAsync(userId);
        return client;
    }

    /// <summary>
    /// The person at <paramref name="browser"/> presses Allow on the request that <paramref name="query"/> makes (the
    /// valid one by default), and the code they are sent back with.
    /// </summary>
    public async Task<string> AllowAsync(HttpClient browser, string? query = null)
    {
        const string prefix = "/connect?request=";
        using var handOff = await browser.GetAsync("/oauth/authorize?" + (query ?? ValidQuery), Ct);
        Assert.Equal(HttpStatusCode.Redirect, handOff.StatusCode);
        var location = Assert.IsType<Uri>(handOff.Headers.Location).OriginalString;
        Assert.StartsWith(prefix, location, StringComparison.Ordinal);

        using var answer = await browser.PostWithTokenAsync(
            "/api/connect",
            await browser.FetchTokenAsync(),
            new { request = location[prefix.Length..], allow = true });
        Assert.Equal(HttpStatusCode.OK, answer.StatusCode);
        var next = (await answer.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("next").GetString()!;

        using var sentBack = await browser.GetAsync(next, Ct);
        Assert.Equal(HttpStatusCode.Redirect, sentBack.StatusCode);
        var returned = QueryHelpers.ParseQuery(Assert.IsType<Uri>(sentBack.Headers.Location).Query);
        return returned["code"].ToString();
    }

    /// <summary>The assistant exchanges <paramref name="code"/>, with the person's PKCE verifier.</summary>
    public Task<HttpResponseMessage> ExchangeAsync(
        string code, bool withResource = true, string? clientSecret = null) =>
        PostFormAsync(
            "/oauth/token",
            ("grant_type", GrantTypes.AuthorizationCode),
            ("code", code),
            ("redirect_uri", RedirectUri),
            ("client_id", ClientId),
            ("code_verifier", McpTestApp.CodeVerifier),
            ("resource", withResource ? McpTestApp.Resource : null),
            ("client_secret", clientSecret));

    /// <summary>The assistant refreshes with <paramref name="refreshToken"/>, naming no resource unless asked.
    /// </summary>
    public Task<HttpResponseMessage> RefreshAsync(string refreshToken, bool withResource = false) =>
        PostFormAsync(
            "/oauth/token",
            ("grant_type", GrantTypes.RefreshToken),
            ("refresh_token", refreshToken),
            ("client_id", ClientId),
            ("resource", withResource ? McpTestApp.Resource : null));

    /// <summary>The assistant revokes <paramref name="token"/>, as a public client, with no secret.</summary>
    public Task<HttpResponseMessage> RevokeAsync(string token, string hint) =>
        PostFormAsync("/oauth/revoke", ("token", token), ("token_type_hint", hint), ("client_id", ClientId));

    /// <summary>The person allows, and the assistant exchanges the code: the tokens of a connection.</summary>
    public async Task<AssistantTokenSet> ConnectAsync(HttpClient browser)
    {
        using var response = await ExchangeAsync(await AllowAsync(browser));
        return await ReadTokensAsync(response);
    }

    /// <summary>
    /// Refreshes every 29 minutes, as an assistant in use does, for as long as that stays within the time given.
    /// </summary>
    public async Task<AssistantTokenSet> RefreshEveryAsync(AssistantTokenSet tokens, DateTimeOffset until)
    {
        while (Clock.GetUtcNow() + Wait <= until)
        {
            Clock.Advance(Wait);
            using var response = await RefreshAsync(tokens.RefreshToken);
            tokens = await ReadTokensAsync(response);
        }

        return tokens;
    }

    /// <summary>
    /// The assistant calls <c>/mcp</c> once, as <see cref="McpRequest"/> makes the call. The caller disposes the
    /// response.
    /// </summary>
    public async Task<HttpResponseMessage> PostMcpAsync(
        string? accessToken, string? origin = null, HttpMethod? method = null, string? body = null)
    {
        using var client = App.CreateAssistantClient();
        using var request = McpRequest(accessToken, origin, method, body);
        return await client.SendAsync(request, Ct);
    }

    /// <summary>
    /// An MCP client of the revision <paramref name="protocolVersion"/> (<c>2025-11-25</c>, which opens with the
    /// initialize handshake, or <c>2026-07-28</c>, which sends its details with each request) that calls <c>/mcp</c>
    /// with <paramref name="accessToken"/>, from <paramref name="clientAddress"/> if it is given. The caller disposes
    /// it.
    /// </summary>
    public async Task<McpClient> McpClientAsync(
        string accessToken, string protocolVersion, string? clientAddress = null)
    {
        var http = App.CreateAssistantClient();
        http.DefaultRequestHeaders.Authorization = new("Bearer", accessToken);
        if (clientAddress is not null)
        {
            http.DefaultRequestHeaders.Add(ClientAddressHeader.Name, clientAddress);
        }

        var transport = new HttpClientTransport(
            new() { Endpoint = new(McpTestApp.Resource), TransportMode = HttpTransportMode.StreamableHttp },
            http,
            ownsHttpClient: true);
        return await McpClient.CreateAsync(
            transport, new McpClientOptions { ProtocolVersion = protocolVersion }, cancellationToken: Ct);
    }

    /// <summary>The status of each authorisation that OpenIddict holds for <paramref name="userId"/>.</summary>
    public async Task<IReadOnlyList<string?>> AuthorisationStatusesAsync(Guid userId)
    {
        await using var scope = App.Services.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictAuthorizationManager>();
        var statuses = new List<string?>();
        await foreach (var authorisation in manager.FindBySubjectAsync(userId.ToString("D"), Ct))
        {
            statuses.Add(await manager.GetStatusAsync(authorisation, Ct));
        }

        return statuses;
    }

    /// <summary>
    /// Revokes every authorisation of <paramref name="userId"/>, as the app does when a person changes, so that what
    /// is tested is what the next call makes of the revoked connection.
    /// </summary>
    public async Task RevokeAuthorisationsAsync(Guid userId)
    {
        await using var scope = App.Services.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictAuthorizationManager>();
        await manager.RevokeBySubjectAsync(userId.ToString("D"), Ct);
    }

    public Task DeactivateAsync(Guid userId) => ChangeAsync(userId, async (users, user) =>
    {
        user.Status = UserStatus.Deactivated;
        await users.UpdateAsync(user);
        await users.UpdateSecurityStampAsync(user);
    });

    public Task ResetSignInAsync(Guid userId) =>
        ChangeAsync(userId, (users, user) => users.UpdateSecurityStampAsync(user));

    public Task ChangeRoleAsync(Guid userId, UserRole role) => ChangeAsync(userId, (users, user) =>
    {
        user.Role = role;
        return users.UpdateAsync(user);
    });

    /// <summary>The person signs out of the app, which changes their security stamp (design.md §8.5).</summary>
    public async Task SignOutOfTheAppAsync(HttpClient browser)
    {
        using var response = await browser.PostWithTokenAsync("/api/auth/logout", await browser.FetchTokenAsync());
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [GeneratedRegex(@"(?<name>\w+)=""(?<value>[^""]*)""")]
    private static partial Regex ChallengeParameter();

    private async Task ChangeAsync(Guid userId, Func<UserManager<ApplicationUser>, ApplicationUser, Task> change)
    {
        await using var scope = App.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByIdAsync(userId.ToString("D"));
        Assert.NotNull(user);
        await change(users, user);
    }

    private async Task<HttpResponseMessage> PostFormAsync(string path, params (string Name, string? Value)[] fields)
    {
        using var client = App.CreateAssistantClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new FormUrlEncodedContent(fields
                .Where(field => field.Value is not null)
                .Select(field => new KeyValuePair<string, string>(field.Name, field.Value!))),
        };
        return await client.SendAsync(request, Ct);
    }
}

/// <summary>What the token endpoint gave an assistant.</summary>
internal sealed record AssistantTokenSet(string AccessToken, string RefreshToken, int ExpiresIn);
