using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Features.Mcp;
using Grow2Notes.Web.Features.Mcp.OAuth;
using Grow2Notes.Web.Platform.Audit;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Microsoft.Extensions.Time.Testing;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Grow2Notes.Tests.Features.Mcp;

/// <summary>
/// The consent step of the authorisation flow (mcp-server.md §4.1, §4.4): an authorise request is handed to the Connect
/// screen, <c>GET</c> and <c>POST /api/connect</c> give the screen its details and take the person's answer, and the
/// navigation that the answer gives issues the code or the refusal, with its audit event. The app runs with the access
/// on, on a database of its own and on a fake clock, and every person signs in with the real session cookie, since a
/// decision needs the session's <c>auth_time</c>. Everything calls the app by its origin over HTTPS with one client for
/// each person, so that the cookies are theirs.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class ConnectTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    : IClassFixture<Grow2NotesFactory>, IAsyncLifetime
{
    private const string ClientId = "https://assistant.example/client.json";
    private const string RedirectUri = "https://assistant.example/callback";
    private const string State = "state-of-the-test";
    private const string Issuer = "https://grow2notes.example/";
    private const string ConnectRequestPrefix = "/connect?request=";

    private readonly FakeTimeProvider clock = new(TestClock.Start);
    private readonly RecordingHttpHandler outbound = new();
    private readonly RecordingLoggerProvider logs = new();

    private WebApplicationFactory<Program> databaseOfItsOwn = null!;
    private WebApplicationFactory<Program> app = null!;
    private Guid managerId;
    private Guid workerId;
    private Guid supportId;
    private Guid managerOfAnotherOrganisationId;

    public static TheoryData<string> CallersOfTheScreen { get; } = ["manager", "support", "worker", "other manager"];

    public static TheoryData<string> PeopleWhoMayAllow { get; } = ["manager", "support"];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        databaseOfItsOwn = await factory.OnDatabaseOfItsOwnAsync(sqlServer, "McpConnect");
        app = databaseOfItsOwn.WithWebHostBuilder(builder => builder
            .MapCookieSignIn()
            .UseClock(clock)
            .ConfigureTestServices(services =>
            {
                // Every outbound call of the app goes here, and the handler fails on any.
                services.ConfigureAll<HttpClientFactoryOptions>(options =>
                    options.HttpMessageHandlerBuilderActions.Add(handler => handler.PrimaryHandler = outbound));
                services.AddSingleton<ILoggerProvider>(logs);
            }));
        await app.RegisterClientAsync(
            ClientId,
            applicationType: null,
            new AssistantDetails("Claude Desktop", "Example AI Pty Ltd", "In the United States"),
            RedirectUri);

        var organisationId = await AddOrganisationAsync();
        managerId = await AddUserAsync(organisationId, UserRole.Manager);
        workerId = await AddUserAsync(organisationId, UserRole.Worker);
        supportId = await AddUserAsync(organisationId, UserRole.Support);
        managerOfAnotherOrganisationId = await AddUserAsync(await AddOrganisationAsync(), UserRole.Manager);
    }

    public async ValueTask DisposeAsync()
    {
        await app.DisposeAsync();
        await databaseOfItsOwn.DisposeAsync();
    }

    [Fact]
    public async Task The_details_come_from_the_registration_and_the_request()
    {
        using var client = await SignedInAsync(managerId);
        var handle = await HandOffAsync(client);

        var details = await GetDetailsAsync(client, handle);

        Assert.Equal("Claude Desktop", details.GetProperty("assistantName").GetString());
        Assert.Equal("Example AI Pty Ltd", details.GetProperty("company").GetString());
        Assert.Equal("In the United States", details.GetProperty("dataLocation").GetString());
        Assert.Equal("assistant.example", details.GetProperty("clientIdHost").GetString());
        Assert.Equal("assistant.example", details.GetProperty("redirectHost").GetString());
        Assert.False(details.GetProperty("loopbackOnly").GetBoolean());
    }

    [Fact]
    public async Task A_client_ID_that_is_not_an_address_is_shown_as_it_is_and_an_all_loopback_assistant_is_flagged()
    {
        await app.RegisterClientAsync(
            "native-assistant",
            ApplicationTypes.Native,
            "http://127.0.0.1/cb",
            "http://localhost/cb");
        using var client = await SignedInAsync(managerId);
        var handle = await HandOffAsync(
            client, McpTestApp.AuthorizeQuery("native-assistant", "http://127.0.0.1:51234/cb"));

        var details = await GetDetailsAsync(client, handle);

        Assert.Equal("native-assistant", details.GetProperty("clientIdHost").GetString());
        Assert.Equal("127.0.0.1", details.GetProperty("redirectHost").GetString());
        Assert.True(details.GetProperty("loopbackOnly").GetBoolean());
    }

    [Fact]
    public async Task An_assistant_with_a_loopback_and_an_HTTPS_redirect_URI_is_not_flagged_as_loopback_only()
    {
        await app.RegisterClientAsync(
            "mixed-assistant", ApplicationTypes.Native, "https://assistant.example/callback", "http://127.0.0.1/cb");
        using var client = await SignedInAsync(managerId);
        var handle = await HandOffAsync(
            client, McpTestApp.AuthorizeQuery("mixed-assistant", "https://assistant.example/callback"));

        var details = await GetDetailsAsync(client, handle);

        Assert.False(details.GetProperty("loopbackOnly").GetBoolean());
    }

    [Fact]
    public async Task A_person_who_is_signed_out_gets_401_from_both()
    {
        using var client = app.CreateAssistantClient();
        var handle = await HandOffAsync(client);

        using var get = await client.GetAsync($"/api/connect?request={handle}", Ct);
        using var post = await PostAsync(client, handle, allow: true);

        Assert.Equal(HttpStatusCode.Unauthorized, get.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, post.StatusCode);
    }

    [Theory]
    [MemberData(nameof(CallersOfTheScreen))]
    public async Task Every_signed_in_person_gets_the_details(string caller)
    {
        using var client = await SignedInAsync(caller);
        var handle = await HandOffAsync(client);

        using var response = await client.GetAsync($"/api/connect?request={handle}", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task A_handle_that_is_missing_or_unreadable_is_a_404()
    {
        using var client = await SignedInAsync(managerId);

        using var missing = await client.GetAsync("/api/connect", Ct);
        using var unreadable = await client.GetAsync("/api/connect?request=not-a-handle", Ct);
        using var posted = await PostAsync(client, "not-a-handle", allow: true);

        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unreadable.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, posted.StatusCode);
    }

    [Fact]
    public async Task A_handle_of_an_assistant_that_is_no_longer_registered_is_a_404()
    {
        using var client = await SignedInAsync(managerId);
        var handle = await HandOffAsync(client);
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var applications = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
            await applications.DeleteAsync((await applications.FindByClientIdAsync(ClientId, Ct))!, Ct);
        }

        using var response = await client.GetAsync($"/api/connect?request={handle}", Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task A_post_without_allow_is_a_422()
    {
        using var client = await SignedInAsync(managerId);
        var handle = await HandOffAsync(client);

        using var response = await client.PostWithTokenAsync(
            "/api/connect", await client.FetchTokenAsync(), new { request = handle });

        await response.ReadProblemAsync(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task A_worker_who_posts_allow_gets_403_with_no_code_and_no_entry()
    {
        using var client = await SignedInAsync(workerId);
        var handle = await HandOffAsync(client);

        using var response = await PostAsync(client, handle, allow: true);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.DoesNotContain("next", await response.Content.ReadAsStringAsync(Ct));
        Assert.Empty(await McpEventsAsync());
    }

    [Fact]
    public async Task At_10_minutes_and_a_second_the_details_and_the_answer_are_a_404()
    {
        using var client = await SignedInAsync(managerId);
        var handle = await HandOffAsync(client);

        clock.Advance(ConsentRequests.Lifetime + TimeSpan.FromSeconds(1));
        using var get = await client.GetAsync($"/api/connect?request={handle}", Ct);
        using var post = await PostAsync(client, handle, allow: true);

        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, post.StatusCode);
    }

    [Fact]
    public async Task A_decision_followed_after_its_deadline_goes_to_the_Connect_screen_with_no_code_and_no_entry()
    {
        using var client = await SignedInAsync(managerId);
        var next = await NextAsync(client, await HandOffAsync(client), allow: true);

        clock.Advance(ConsentRequests.Lifetime + TimeSpan.FromSeconds(1));
        using var response = await client.GetAsync(next, Ct);

        AssertSentToTheConnectScreen(response);
        Assert.Empty(await McpEventsAsync());
    }

    [Theory]
    [MemberData(nameof(PeopleWhoMayAllow))]
    public async Task Allow_sends_the_person_back_to_the_registered_redirect_URI_with_a_code_and_writes_one_entry(
        string person)
    {
        using var client = await SignedInAsync(person);
        var next = await NextAsync(client, await HandOffAsync(client), allow: true);

        using var response = await client.GetAsync(next, Ct);

        Assert.StartsWith("/oauth/authorize?", next, StringComparison.Ordinal);
        var returned = AssertSentTo(response, RedirectUri);
        Assert.NotEmpty(returned["code"].ToString());
        Assert.Equal(State, returned["state"]);
        Assert.Equal(Issuer, returned["iss"]);
        Assert.DoesNotContain("error", returned.Keys);
        AssertOneEvent(
            AuditEventTypes.Mcp.Authorised,
            Id(person),
            new JsonObject { ["client"] = ClientId, ["scope"] = "grow2notes.admin offline_access" },
            await McpEventsAsync());
    }

    [Theory]
    [MemberData(nameof(PeopleWhoMayAllow))]
    public async Task Cancel_sends_the_person_back_with_access_denied_and_writes_one_entry_saying_cancelled(
        string person)
    {
        using var client = await SignedInAsync(person);
        var next = await NextAsync(client, await HandOffAsync(client), allow: false);

        using var response = await client.GetAsync(next, Ct);

        var returned = AssertSentTo(response, RedirectUri);
        Assert.Equal("access_denied", returned["error"]);
        Assert.Equal(State, returned["state"]);
        Assert.Equal(Issuer, returned["iss"]);
        Assert.DoesNotContain("code", returned.Keys);
        AssertOneEvent(
            AuditEventTypes.Mcp.AuthorisationRefused,
            Id(person),
            new JsonObject { ["client"] = ClientId, ["why"] = "cancelled" },
            await McpEventsAsync());
    }

    [Fact]
    public async Task A_workers_cancel_sends_them_back_with_access_denied_and_writes_one_entry_saying_not_allowed()
    {
        using var client = await SignedInAsync(workerId);
        var next = await NextAsync(client, await HandOffAsync(client), allow: false);

        using var response = await client.GetAsync(next, Ct);

        var returned = AssertSentTo(response, RedirectUri);
        Assert.Equal("access_denied", returned["error"]);
        Assert.DoesNotContain("code", returned.Keys);
        AssertOneEvent(
            AuditEventTypes.Mcp.AuthorisationRefused,
            workerId,
            new JsonObject { ["client"] = ClientId, ["why"] = "not_allowed" },
            await McpEventsAsync());
    }

    [Fact]
    public async Task A_manager_who_became_a_worker_between_the_answer_and_the_navigation_is_denied()
    {
        using var client = await SignedInAsync(managerId);
        var next = await NextAsync(client, await HandOffAsync(client), allow: true);
        await ChangeRoleAsync(managerId, UserRole.Worker);

        using var response = await client.GetAsync(next, Ct);

        var returned = AssertSentTo(response, RedirectUri);
        Assert.Equal("access_denied", returned["error"]);
        Assert.DoesNotContain("code", returned.Keys);
        AssertOneEvent(
            AuditEventTypes.Mcp.AuthorisationRefused,
            managerId,
            new JsonObject { ["client"] = ClientId, ["why"] = "not_allowed" },
            await McpEventsAsync());
    }

    [Fact]
    public async Task Another_persons_session_following_a_decision_goes_to_the_Connect_screen_with_no_code_or_entry()
    {
        using var manager = await SignedInAsync(managerId);
        var next = await NextAsync(manager, await HandOffAsync(manager), allow: true);
        using var support = await SignedInAsync(supportId);

        using var response = await support.GetAsync(next, Ct);

        AssertSentToTheConnectScreen(response);
        Assert.Empty(await McpEventsAsync());
    }

    [Fact]
    public async Task A_decision_whose_query_was_altered_goes_to_the_Connect_screen_with_no_code_or_entry()
    {
        using var client = await SignedInAsync(managerId);
        var next = await NextAsync(client, await HandOffAsync(client), allow: true);

        var altered = next.Replace(State, State[..^1] + "u", StringComparison.Ordinal);
        using var response = await client.GetAsync(altered, Ct);

        AssertSentToTheConnectScreen(response);
        Assert.Empty(await McpEventsAsync());
    }

    [Fact]
    public async Task A_decision_with_no_session_goes_to_the_Connect_screen_with_no_code_or_entry()
    {
        using var manager = await SignedInAsync(managerId);
        var next = await NextAsync(manager, await HandOffAsync(manager), allow: true);
        using var signedOut = app.CreateAssistantClient();

        using var response = await signedOut.GetAsync(next, Ct);

        AssertSentToTheConnectScreen(response);
        Assert.Empty(await McpEventsAsync());
    }

    [Fact]
    public async Task A_second_authorise_request_is_handed_to_the_Connect_screen_again()
    {
        using var client = await SignedInAsync(managerId);
        var next = await NextAsync(client, await HandOffAsync(client), allow: true);
        using var allowed = await client.GetAsync(next, Ct);
        AssertSentTo(allowed, RedirectUri);

        using var second = await client.GetAsync("/oauth/authorize?" + ValidQuery, Ct);

        Assert.Equal(HttpStatusCode.Redirect, second.StatusCode);
        Assert.StartsWith(ConnectRequestPrefix, second.Headers.Location?.OriginalString, StringComparison.Ordinal);
        Assert.Single(await McpEventsAsync());
    }

    [Fact]
    public async Task An_unknown_client_goes_to_the_Connect_screen_with_no_request_and_no_outbound_call()
    {
        const string unknown = "https://unknown.example/client.json";
        using var client = app.CreateAssistantClient();

        using var response = await client.GetAsync(
            "/oauth/authorize?" + McpTestApp.AuthorizeQuery(unknown, RedirectUri), Ct);

        AssertSentToTheConnectScreen(response);
        Assert.Empty(outbound.Requests);
        var entry = Assert.Single(logs.Entries, line => line.Category == typeof(AuthoriseEndpoint).FullName);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal(unknown, entry.ClientId);
    }

    [Fact]
    public async Task A_client_ID_with_line_breaks_is_logged_without_them()
    {
        using var client = app.CreateAssistantClient();

        using var response = await client.GetAsync(
            "/oauth/authorize?"
                + McpTestApp.AuthorizeQuery("https://evil.example/\r\nWarning: forged", RedirectUri),
            Ct);

        AssertSentToTheConnectScreen(response);
        var entry = Assert.Single(logs.Entries, line => line.Category == typeof(AuthoriseEndpoint).FullName);
        Assert.Equal("https://evil.example/Warning: forged", entry.ClientId);
    }

    [Fact]
    public async Task A_client_ID_of_5000_characters_is_logged_cut_to_100_and_an_ellipsis()
    {
        var longClientId = "https://long.example/" + new string('a', 4979);
        using var client = app.CreateAssistantClient();

        using var response = await client.GetAsync(
            "/oauth/authorize?" + McpTestApp.AuthorizeQuery(longClientId, RedirectUri), Ct);

        AssertSentToTheConnectScreen(response);
        var entry = Assert.Single(logs.Entries, line => line.Category == typeof(AuthoriseEndpoint).FullName);
        Assert.Equal(longClientId[..Limits.McpClientId] + "…", entry.ClientId);
    }

    [Fact]
    public async Task A_client_ID_with_Unicode_line_separators_is_logged_without_them()
    {
        using var client = app.CreateAssistantClient();

        using var response = await client.GetAsync(
            "/oauth/authorize?"
                + McpTestApp.AuthorizeQuery("https://evil.example/ a b", RedirectUri),
            Ct);

        AssertSentToTheConnectScreen(response);
        var entry = Assert.Single(logs.Entries, line => line.Category == typeof(AuthoriseEndpoint).FullName);
        Assert.Equal("https://evil.example/ab", entry.ClientId);
    }

    [Fact]
    public async Task A_client_ID_is_never_cut_in_the_middle_of_a_surrogate_pair()
    {
        // "https://long.example/" is 21 characters, so the emoji's two halves are the 100th and 101st.
        var longClientId = "https://long.example/" + new string('a', Limits.McpClientId - 22) + "\U0001F600tail";
        using var client = app.CreateAssistantClient();

        using var response = await client.GetAsync(
            "/oauth/authorize?" + McpTestApp.AuthorizeQuery(longClientId, RedirectUri), Ct);

        AssertSentToTheConnectScreen(response);
        var entry = Assert.Single(logs.Entries, line => line.Category == typeof(AuthoriseEndpoint).FullName);
        Assert.Equal(longClientId[..(Limits.McpClientId - 1)] + "…", entry.ClientId);
    }

    private static string ValidQuery =>McpTestApp.AuthorizeQuery(ClientId, RedirectUri);

    private static void AssertSentToTheConnectScreen(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/connect", response.Headers.Location?.OriginalString);
    }

    // The redirect must be to exactly the registered address, without a query of its own; the query it carries is what
    // OpenIddict added.
    private static Dictionary<string, StringValues> AssertSentTo(
        HttpResponseMessage response, string redirectUri)
    {
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = Assert.IsType<Uri>(response.Headers.Location);
        Assert.Equal(redirectUri, location.GetLeftPart(UriPartial.Path));
        return QueryHelpers.ParseQuery(location.Query);
    }

    private static void AssertOneEvent(string eventType, Guid actorId, JsonObject details, List<McpEvent> events)
    {
        var written = Assert.Single(events);
        Assert.Equal(eventType, written.EventType);
        Assert.Equal(actorId, written.ActorUserId);
        Assert.True(
            JsonNode.DeepEquals(details, JsonNode.Parse(written.Details!)),
            $"Details: expected {details.ToJsonString()}, written {written.Details}.");
    }

    private Guid Id(string person) => person switch
    {
        "manager" => managerId,
        "support" => supportId,
        "worker" => workerId,
        "other manager" => managerOfAnotherOrganisationId,
        _ => throw new ArgumentOutOfRangeException(nameof(person)),
    };

    private async Task<HttpClient> SignedInAsync(string person) => await SignedInAsync(Id(person));

    private async Task<HttpClient> SignedInAsync(Guid userId)
    {
        var client = app.CreateAssistantClient();
        await client.SignInWithCookieAsync(userId);
        return client;
    }

    // Opens the authorise request, as the assistant's browser does, and gives the handle that it is sent to /connect
    // with.
    private static async Task<string> HandOffAsync(HttpClient client, string? query = null)
    {
        using var response = await client.GetAsync("/oauth/authorize?" + (query ?? ValidQuery), Ct);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = Assert.IsType<Uri>(response.Headers.Location).OriginalString;
        Assert.StartsWith(ConnectRequestPrefix, location, StringComparison.Ordinal);
        return location[ConnectRequestPrefix.Length..];
    }

    private static async Task<JsonElement> GetDetailsAsync(HttpClient client, string handle)
    {
        using var response = await client.GetAsync($"/api/connect?request={handle}", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string handle, bool allow) =>
        await client.PostWithTokenAsync(
            "/api/connect", await client.FetchTokenAsync(), new { request = handle, allow });

    private static async Task<string> NextAsync(HttpClient client, string handle, bool allow)
    {
        using var response = await PostAsync(client, handle, allow);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        return body.GetProperty("next").GetString()!;
    }

    // Read in SQL, so the query filter, which would need a tenant, does not decide what is seen.
    private async Task<List<McpEvent>> McpEventsAsync()
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        return await db.Database.SqlQuery<McpEvent>($"""
            SELECT [EventType], [ActorUserId], [Details]
            FROM [AuditEvent]
            WHERE [EventType] LIKE 'mcp.%'
            ORDER BY [Id]
            """).ToListAsync(Ct);
    }

    // An organisation is the tenant, not tenant-owned, so this needs no tenant.
    private async Task<Guid> AddOrganisationAsync()
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        var organisation = new Organisation
        {
            Name = "Connect test",
            GuidePrompts = "",
            CreatedAtUtc = new DateTime(2026, 10, 18, 0, 0, 0, DateTimeKind.Utc),
        };
        db.Organisations.Add(organisation);
        await db.SaveChangesAsync(Ct);
        return organisation.Id;
    }

    // AspNetUsers is not filtered by tenant, so this needs none.
    private async Task<Guid> AddUserAsync(Guid organisationId, UserRole role)
    {
        var addedAtUtc = new DateTime(2026, 10, 18, 0, 0, 0, DateTimeKind.Utc);
        var email = $"{Guid.NewGuid():N}@example.org";
        var user = new ApplicationUser
        {
            OrganisationId = organisationId,
            DisplayName = "Sam Lee",
            Role = role,
            Status = UserStatus.Active,
            InvitedAtUtc = addedAtUtc,
            ActivatedAtUtc = addedAtUtc,
            UserName = email,
            Email = email,
            EmailConfirmed = true,
        };
        await using var scope = app.Services.CreateAsyncScope();
        var created = await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().CreateAsync(user);
        Assert.True(created.Succeeded, string.Join(" ", created.Errors.Select(error => error.Code)));
        return user.Id;
    }

    private async Task ChangeRoleAsync(Guid userId, UserRole role)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByIdAsync(userId.ToString("D"));
        Assert.NotNull(user);
        user.Role = role;
        var updated = await users.UpdateAsync(user);
        Assert.True(updated.Succeeded, string.Join(" ", updated.Errors.Select(error => error.Code)));
    }

    private sealed record McpEvent(string EventType, Guid? ActorUserId, string? Details);

    private sealed record LogEntry(LogLevel Level, string Category, string? ClientId);

    // The log lines of the app, kept, with the client ID property of those that have one. Added to the app's
    // providers by the test, so that no package is needed.
    private sealed class RecordingLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentQueue<LogEntry> entries = new();

        public IReadOnlyCollection<LogEntry> Entries => entries;

        public ILogger CreateLogger(string categoryName) => new Recorder(categoryName, entries);

        public void Dispose()
        {
        }

        private sealed class Recorder(string category, ConcurrentQueue<LogEntry> entries) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                var clientId = state is IEnumerable<KeyValuePair<string, object?>> properties
                    ? properties.FirstOrDefault(property => property.Key == "ClientId").Value as string
                    : null;
                entries.Enqueue(new(logLevel, category, clientId));
            }
        }
    }
}
