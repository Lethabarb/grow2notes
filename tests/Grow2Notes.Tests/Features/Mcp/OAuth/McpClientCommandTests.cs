using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Features.Mcp.OAuth;
using Grow2Notes.Web.Platform.Audit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Grow2Notes.Tests.Features.Mcp.OAuth;

/// <summary>
/// <c>admin mcp-client</c> (mcp-server.md §11.2; S07.01.05), run through the operator commands' entry as the app's
/// binary runs it, with writers in place of the console (<see cref="AdminCommandRuns"/>). Every test runs on a migrated
/// database of its own, with nothing seeded and the switch <c>Mcp:Enabled</c> off, which also proves that the command
/// works before switch-on (R15). The metadata fetch goes to a <see cref="RecordingHttpHandler"/>, so none leaves the
/// process and each test knows how many requests were made.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class McpClientCommandTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    : IClassFixture<Grow2NotesFactory>
{
    private const string MetadataUrl = "https://assistant.example/oauth/client.json";
    private const string Redirect = "https://assistant.example/callback";
    private const string Resource = $"{Grow2NotesFactory.Origin}/mcp";

    private static readonly string[] AllPermissions =
    [
        "ept:authorization",
        "ept:token",
        "ept:revocation",
        "gt:authorization_code",
        "gt:refresh_token",
        "rst:code",
        "scp:grow2notes.admin",
        $"rsrc:{Resource}",
    ];

    // The refusals that are found before anything is fetched or written, each with the words of its reason.
    public static TheoryData<string[], string> ArgumentRefusals { get; } = new()
    {
        { [], "No subcommand was given." },
        { ["register"], "\"register\" is not a subcommand of admin mcp-client." },
        { ["list", "everything"], "list takes no arguments" },
        { ["remove"], "No client ID was given." },
        { ["remove", "one", "two"], "remove takes one client ID" },
        { ["remove", "unknown-assistant"], "No approved assistant has the client ID \"unknown-assistant\"." },
        { [.. ById(), "--client-id", "other"], "--client-id is given more than once." },
        { [.. ById(), "--colour", "red"], "\"--colour\" is not an option of admin mcp-client add." },
        { [.. ById(), "--data-location"], "--data-location has no value." },
        { [.. ById(), .. ByMetadataUrl()[1..3]], "cannot both be given." },
        { Without(ById(), "--client-id"), "One of --metadata-url and --client-id is needed." },
        { [.. ByMetadataUrl(), "--redirect-uri", Redirect], "--redirect-uri is only for --client-id" },
        { Without(ById(), "--redirect-uri"), "--client-id needs at least one --redirect-uri." },
        { Without(ById(), "--name"), "--name is missing." },
        { With(ById(), "--company", "   "), "--company is empty." },
        { With(ById(), "--name", new('n', Limits.McpAssistantName + 1)), "--name is longer than 100 characters." },
        { With(ById(), "--company", new('c', Limits.McpCompany + 1)), "--company is longer than 200 characters." },
        {
            With(ById(), "--data-location", new('d', Limits.McpDataLocation + 1)),
            "--data-location is longer than 200 characters."
        },
        { With(ById(), "--name", "Assistant‮One"), "--name has a control or formatting character." },
        {
            With(ById(), "--client-id", new('a', Limits.McpClientId + 1)),
            "--client-id is longer than 100 characters."
        },
        { With(ById(), "--client-id", "assistant one"), "--client-id has white space." },
        { With(ById(), "--redirect-uri", "http://assistant.example/cb"), "must be https, or http on 127.0.0.1" },
        { With(ById(), "--redirect-uri", "http://loopback/cb"), "must be https, or http on 127.0.0.1" },
        { With(ById(), "--redirect-uri", "http://127.5.5.5/cb"), "must be https, or http on 127.0.0.1" },
        { With(ById(), "--redirect-uri", "assistant.example/cb"), "is not an absolute address." },
        { With(ById(), "--redirect-uri", "https://assistant.example/cb#part"), "has a fragment." },
        { With(ById(), "--redirect-uri", "https://user@assistant.example/cb"), "has user information." },
        { With(ByMetadataUrl(), "--metadata-url", "http://assistant.example/client.json"), "must be https." },
        { With(ByMetadataUrl(), "--metadata-url", "https://assistant.example"), "must have a path." },
        { With(ByMetadataUrl(), "--metadata-url", "https://assistant.example/"), "must have a path." },
        {
            With(ByMetadataUrl(), "--metadata-url", "https://user:secret@assistant.example/client.json"),
            "must have no user information."
        },
        { With(ByMetadataUrl(), "--metadata-url", $"{MetadataUrl}#part"), "must have no fragment." },
        {
            With(ByMetadataUrl(), "--metadata-url", "https://assistant.example/a/../client.json"),
            "must be in plain form, with no dot segments."
        },
        {
            With(ByMetadataUrl(), "--metadata-url", $"https://assistant.example/{new string('a', 100)}"),
            "--metadata-url is longer than 100 characters."
        },
    };

    // A document that fails one check, as the single request answers it, with the words of the reason.
    public static TheoryData<string, string> DocumentRefusals { get; } = new()
    {
        { Document(without: "client_id"), "The document's client_id is not exactly the URL." },
        {
            Document(("client_id", MetadataUrl.Replace("client", "Client", StringComparison.Ordinal))),
            "The document's client_id is not exactly the URL."
        },
        { Document(without: "client_name"), "The document has no client_name." },
        { Document(("client_name", "\u001b[31mAssistant")), "client_name has a control or formatting" },
        { Document(without: "redirect_uris"), "redirect_uris is missing or empty." },
        { Document(("redirect_uris", new JsonArray())), "redirect_uris is missing or empty." },
        { Document(("redirect_uris", new JsonArray(Redirect, 7))), "holds something that is not a string." },
        {
            Document(("redirect_uris", new JsonArray("http://assistant.example/cb"))),
            "must be https, or http on 127.0.0.1"
        },
        {
            Document(("token_endpoint_auth_method", "client_secret_basic")),
            "token_endpoint_auth_method is not none."
        },
    };

    [Fact]
    public async Task Add_by_client_ID_stores_one_public_client_and_writes_one_event_for_each_organisation()
    {
        var handler = new RecordingHttpHandler();
        await using var app = await OnDatabaseOfItsOwnAsync("McpClientAddById", handler);
        var organisations = new[] { await AddOrganisationAsync(app), await AddOrganisationAsync(app) };

        var run = await app.RunAdminCommandAsync(ById());

        Assert.Equal((0, ""), (run.ExitCode, run.Error));
        Assert.Equal(
            Lines("Added assistant-one", "Client ID: assistant-one", "Name: Assistant One", "Company: Example AI",
                "Data location: In the United States", $"Redirect URI: {Redirect}"),
            run.Output);
        Assert.Empty(handler.Requests);
        var stored = Assert.Single(await StoredApplicationsAsync(app));
        Assert.Equal("assistant-one", stored.ClientId);
        Assert.Equal(ClientTypes.Public, stored.ClientType);
        Assert.Null(stored.ClientSecret);
        Assert.Null(stored.ConsentType);
        Assert.Null(stored.ApplicationType);
        Assert.Equal("Assistant One", stored.DisplayName);
        Assert.Equal([Redirect], Strings(stored.RedirectUris));
        Assert.Equal(
            new JsonObject { ["company"] = "Example AI", ["dataLocation"] = "In the United States" }.ToJsonString(),
            JsonNode.Parse(stored.Properties!)!.ToJsonString());
        Assert.Equal(
            AllPermissions.Order(StringComparer.Ordinal),
            Strings(stored.Permissions).Order(StringComparer.Ordinal));
        await AssertEventsAsync(
            app, AuditEventTypes.Admin.McpClientAdded, organisations, "assistant-one", "Assistant One");
    }

    [Theory]
    [InlineData("Ipv4", "http://127.0.0.1:8080/callback")]
    [InlineData("Ipv6", "http://[::1]:8080/callback")]
    [InlineData("Localhost", "http://localhost:8080/callback")]
    public async Task A_loopback_redirect_URI_makes_the_client_native(string label, string redirectUri)
    {
        await using var app = await OnDatabaseOfItsOwnAsync($"McpClientNative{label}", new RecordingHttpHandler());
        await AddOrganisationAsync(app);

        var run = await app.RunAdminCommandAsync(With(ById(), "--redirect-uri", redirectUri));

        Assert.Equal((0, ""), (run.ExitCode, run.Error));
        var stored = Assert.Single(await StoredApplicationsAsync(app));
        Assert.Equal(ApplicationTypes.Native, stored.ApplicationType);
        Assert.Equal([redirectUri], Strings(stored.RedirectUris));
    }

    [Fact]
    public async Task Add_again_updates_the_one_row_and_writes_updated()
    {
        await using var app = await OnDatabaseOfItsOwnAsync("McpClientAddAgain", new RecordingHttpHandler());
        var organisations = new[] { await AddOrganisationAsync(app), await AddOrganisationAsync(app) };
        Assert.Equal(0, (await app.RunAdminCommandAsync(ById())).ExitCode);

        var run = await app.RunAdminCommandAsync(With(
            ById(), "--name", "Assistant Two", "--company", "Other AI", "--redirect-uri", "http://localhost/cb"));

        Assert.Equal((0, ""), (run.ExitCode, run.Error));
        Assert.Equal(
            Lines("Updated assistant-one", "Client ID: assistant-one", "Name: Assistant Two", "Company: Other AI",
                "Data location: In the United States", "Redirect URI: http://localhost/cb"),
            run.Output);
        var stored = Assert.Single(await StoredApplicationsAsync(app));
        Assert.Equal("Assistant Two", stored.DisplayName);
        Assert.Equal(ApplicationTypes.Native, stored.ApplicationType);
        Assert.Equal(["http://localhost/cb"], Strings(stored.RedirectUris));
        Assert.Equal(
            "Other AI",
            JsonNode.Parse(stored.Properties!)![ApprovedAssistantProperties.Company]!.GetValue<string>());
        await AssertEventsAsync(
            app, AuditEventTypes.Admin.McpClientAdded, organisations, "assistant-one", "Assistant One");
        await AssertEventsAsync(
            app, AuditEventTypes.Admin.McpClientUpdated, organisations, "assistant-one", "Assistant Two");
    }

    [Theory]
    [InlineData("WithNone", true)]
    [InlineData("WithNoMethod", false)]
    public async Task Add_by_metadata_URL_makes_one_GET_and_stores_the_documents_redirect_URIs(
        string label, bool givesMethod)
    {
        JsonNode uris = new JsonArray(Redirect, "http://[::1]/cb");
        var document = givesMethod
            ? Document(("redirect_uris", uris), ("token_endpoint_auth_method", "none"))
            : Document(("redirect_uris", uris));
        var handler = new RecordingHttpHandler(() => Ok(document));
        await using var app = await OnDatabaseOfItsOwnAsync($"McpClientMetadata{label}", handler);
        await AddOrganisationAsync(app);

        var run = await app.RunAdminCommandAsync(ByMetadataUrl());

        Assert.Equal((0, ""), (run.ExitCode, run.Error));
        var request = Assert.Single(handler.Requests);
        Assert.Equal((HttpMethod.Get, MetadataUrl), (request.Method, request.Url.OriginalString));
        Assert.Equal(
            Lines($"Added {MetadataUrl}", $"Client ID: {MetadataUrl}", "Name: Assistant One", "Company: Example AI",
                "Data location: In the United States", $"Redirect URI: {Redirect}", "Redirect URI: http://[::1]/cb",
                "Document client_name: The Assistant"),
            run.Output);
        var stored = Assert.Single(await StoredApplicationsAsync(app));
        Assert.Equal(MetadataUrl, stored.ClientId);
        Assert.Equal("Assistant One", stored.DisplayName);
        Assert.Equal(ApplicationTypes.Native, stored.ApplicationType);
        Assert.Equal([Redirect, "http://[::1]/cb"], Strings(stored.RedirectUris));
    }

    [Fact]
    public async Task Remove_deletes_the_client_with_its_authorisation_and_token_and_keeps_the_other_clients()
    {
        await using var app = await OnDatabaseOfItsOwnAsync("McpClientRemove", new RecordingHttpHandler());
        var organisations = new[] { await AddOrganisationAsync(app), await AddOrganisationAsync(app) };
        Assert.Equal(0, (await app.RunAdminCommandAsync(ById())).ExitCode);
        Assert.Equal(0, (await app.RunAdminCommandAsync(With(ById(), "--client-id", "assistant-two"))).ExitCode);
        await AddAuthorisationAndTokenAsync(app, "assistant-one");
        await AddAuthorisationAndTokenAsync(app, "assistant-two", withAuthorisation: false);

        var run = await app.RunAdminCommandAsync(["mcp-client", "remove", "assistant-one"]);

        Assert.Equal((0, "", Lines("Removed assistant-one")), (run.ExitCode, run.Error, run.Output));
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        var remaining = await db.Set<OpenIddictEntityFrameworkCoreApplication<Guid>>().ToListAsync(Ct);
        Assert.Equal("assistant-two", Assert.Single(remaining).ClientId);
        Assert.Empty(await db.Set<OpenIddictEntityFrameworkCoreAuthorization<Guid>>().ToListAsync(Ct));
        var token = Assert.Single(
            await db.Set<OpenIddictEntityFrameworkCoreToken<Guid>>().Include(row => row.Application).ToListAsync(Ct));
        Assert.Equal("assistant-two", token.Application!.ClientId);
        await AssertEventsAsync(
            app, AuditEventTypes.Admin.McpClientRemoved, organisations, "assistant-one", "Assistant One");
    }

    [Fact]
    public async Task List_prints_each_client_in_client_ID_order_with_no_token_data_and_writes_no_event()
    {
        await using var app = await OnDatabaseOfItsOwnAsync("McpClientList", new RecordingHttpHandler());
        await AddOrganisationAsync(app);
        Assert.Equal(0, (await app.RunAdminCommandAsync(With(ById(), "--client-id", "b-assistant"))).ExitCode);
        Assert.Equal(
            0,
            (await app.RunAdminCommandAsync([
                .. With(With(ById(), "--client-id", "a-assistant"), "--name", "Assistant A"),
                "--redirect-uri", "http://localhost/cb"])).ExitCode);
        await AddAuthorisationAndTokenAsync(app, "a-assistant");
        var eventsBefore = await StoredEventsAsync(app);

        var run = await app.RunAdminCommandAsync(["mcp-client", "list"]);

        Assert.Equal((0, ""), (run.ExitCode, run.Error));
        Assert.Equal(
            Lines("Client ID: a-assistant", "Name: Assistant A", "Company: Example AI",
                "Data location: In the United States", $"Redirect URI: {Redirect}", "Redirect URI: http://localhost/cb",
                "", "Client ID: b-assistant", "Name: Assistant One", "Company: Example AI",
                "Data location: In the United States", $"Redirect URI: {Redirect}"),
            run.Output);
        Assert.Equal(eventsBefore.Count, (await StoredEventsAsync(app)).Count);
    }

    [Fact]
    public async Task List_prints_nothing_when_there_are_no_clients()
    {
        await using var app = await OnDatabaseOfItsOwnAsync("McpClientListNone", new RecordingHttpHandler());

        var run = await app.RunAdminCommandAsync(["mcp-client", "list"]);

        Assert.Equal((0, "", ""), (run.ExitCode, run.Output, run.Error));
    }

    // Each row asserts that the run is refused, saying why, with nothing written and no request made. The database is
    // the theories' one, which holds no organisation: nothing may write to it, so every test finds it empty.
    [Theory]
    [MemberData(nameof(ArgumentRefusals))]
    public async Task A_wrong_argument_is_refused_before_anything_is_fetched_or_written(
        string[] arguments, string reason)
    {
        var handler = new RecordingHttpHandler();
        await using var app = await OnDatabaseOfItsOwnAsync("McpClientRefusals", handler);

        var run = await app.RunAdminCommandAsync(["mcp-client", .. arguments]);

        await AssertRefusedAsync(app, handler, run, reason, requests: 0);
    }

    [Theory]
    [MemberData(nameof(DocumentRefusals))]
    public async Task A_document_that_fails_a_check_is_refused_after_one_request(string body, string reason)
    {
        var handler = new RecordingHttpHandler(() => Ok(body));
        await using var app = await OnDatabaseOfItsOwnAsync("McpClientRefusals", handler);

        var run = await app.RunAdminCommandAsync(ByMetadataUrl());

        await AssertRefusedAsync(app, handler, run, reason, requests: 1);
    }

    [Theory]
    [InlineData("redirect", "The server answered 302, not a success.")]
    [InlineData("not-json", "The document is not JSON.")]
    [InlineData("array", "The document is not a JSON object.")]
    [InlineData("over-64-KB-with-length", "The document could not be fetched")]
    [InlineData("over-64-KB-streamed", "The document could not be fetched")]
    public async Task A_response_that_is_not_a_small_JSON_object_is_refused_after_one_request(
        string kind, string reason)
    {
        var big = $$"""{"client_name":"{{new string('x', ClientMetadataDocuments.MaxBytes)}}"}""";
        var handler = new RecordingHttpHandler(() => kind switch
        {
            "redirect" => new(HttpStatusCode.Found) { Headers = { Location = new("https://assistant.example/other") } },
            "not-json" => Ok("<html>Not a document.</html>"),
            "array" => Ok("[]"),
            "over-64-KB-with-length" => Ok(big),
            _ => new(HttpStatusCode.OK) { Content = new UnknownLengthContent(Encoding.UTF8.GetBytes(big)) },
        });
        await using var app = await OnDatabaseOfItsOwnAsync("McpClientRefusals", handler);

        var run = await app.RunAdminCommandAsync(ByMetadataUrl());

        await AssertRefusedAsync(app, handler, run, reason, requests: 1);
    }

    [Fact]
    public async Task A_wrong_argument_prints_the_usage_and_any_other_refusal_does_not()
    {
        await using var app = await OnDatabaseOfItsOwnAsync("McpClientRefusals", new RecordingHttpHandler());

        var wrong = await app.RunAdminCommandAsync(["mcp-client", "add"]);
        var unknown = await app.RunAdminCommandAsync(["mcp-client", "remove", "unknown-assistant"]);

        Assert.Contains(McpClientCommand.Usage, wrong.Error, StringComparison.Ordinal);
        Assert.DoesNotContain(McpClientCommand.Usage, unknown.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Add_is_refused_when_App_Origin_is_not_set_and_prints_no_usage()
    {
        var handler = new RecordingHttpHandler();
        await using var app = await OnDatabaseOfItsOwnAsync("McpClientNoOrigin", handler, origin: "");
        await AddOrganisationAsync(app);

        var run = await app.RunAdminCommandAsync(ById());

        await AssertRefusedAsync(app, handler, run, "App:Origin is not set, so the resource", requests: 0);
        Assert.DoesNotContain(McpClientCommand.Usage, run.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Add_is_refused_when_there_is_no_organisation_yet()
    {
        var handler = new RecordingHttpHandler();
        await using var app = await OnDatabaseOfItsOwnAsync("McpClientNoOrganisation", handler);

        var run = await app.RunAdminCommandAsync(ById());

        await AssertRefusedAsync(
            app, handler, run, "There is no organisation yet: run admin bootstrap first.", requests: 0);
    }

    private static string[] Strings(string? json) => JsonSerializer.Deserialize<string[]>(json!)!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // The arguments of an add by client ID, with all its options.
    private static string[] ById() =>
    [
        "add", "--client-id", "assistant-one", "--redirect-uri", Redirect,
        "--name", "Assistant One", "--company", "Example AI", "--data-location", "In the United States",
    ];

    // The arguments of an add by metadata URL, with all its options.
    private static string[] ByMetadataUrl() =>
    [
        "add", "--metadata-url", MetadataUrl,
        "--name", "Assistant One", "--company", "Example AI", "--data-location", "In the United States",
    ];

    // The arguments with the value after each option replaced, given as the option and then its new value.
    private static string[] With(string[] arguments, params string[] optionsAndValues)
    {
        var changed = arguments.ToArray();
        for (var i = 0; i < optionsAndValues.Length; i += 2)
        {
            changed[Array.IndexOf(changed, optionsAndValues[i]) + 1] = optionsAndValues[i + 1];
        }

        return changed;
    }

    // The arguments without option and its value.
    private static string[] Without(string[] arguments, string option)
    {
        var at = Array.IndexOf(arguments, option);
        return [.. arguments[..at], .. arguments[(at + 2)..]];
    }

    // A client metadata document that passes every check, with the given members replaced, or left out.
    private static string Document(params (string Key, JsonNode? Value)[] replaced) =>
        Document(without: null, replaced);

    private static string Document(string? without, params (string Key, JsonNode? Value)[] replaced)
    {
        var document = new JsonObject
        {
            ["client_id"] = MetadataUrl,
            ["client_name"] = "The Assistant",
            ["redirect_uris"] = new JsonArray(Redirect),
        };
        foreach (var (key, value) in replaced)
        {
            document[key] = value;
        }

        if (without is not null)
        {
            document.Remove(without);
        }

        return document.ToJsonString();
    }

    private static HttpResponseMessage Ok(string body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json"),
    };

    // Lines as the command writes them, each ended by the line break.
    private static string Lines(params string[] lines) =>
        string.Concat(lines.Select(line => line + Environment.NewLine));

    // A migrated database of the test's own, with nothing seeded and Mcp:Enabled off, and the app on it, with the
    // handler answering the metadata fetch.
    private async Task<WebApplicationFactory<Program>> OnDatabaseOfItsOwnAsync(
        string name, RecordingHttpHandler handler, string origin = Grow2NotesFactory.Origin)
    {
        var connectionString = await sqlServer.CreateDatabaseAsync(name, Ct);
        return factory.WithWebHostBuilder(builder => builder
            .ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(
                [new("ConnectionStrings:Grow2Notes", connectionString), new("App:Origin", origin)]))
            .ConfigureTestServices(services => services.AddHttpClient<ClientMetadataDocuments>()
                .ConfigurePrimaryHttpMessageHandler(() => handler)));
    }

    // An organisation is the tenant, not tenant-owned, so this needs no tenant.
    private static async Task<Guid> AddOrganisationAsync(WebApplicationFactory<Program> app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        var organisation = new Organisation
        {
            Name = "Approved assistants test",
            CreatedAtUtc = new DateTime(2026, 10, 26, 0, 0, 0, DateTimeKind.Utc),
        };
        db.Organisations.Add(organisation);
        await db.SaveChangesAsync(Ct);
        return organisation.Id;
    }

    // A valid authorisation and a refresh token of the client, as T31 will make them, through OpenIddict's managers.
    private static async Task AddAuthorisationAndTokenAsync(
        WebApplicationFactory<Program> app, string clientId, bool withAuthorisation = true)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var applications = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var application = await applications.FindByClientIdAsync(clientId, Ct);
        var applicationId = await applications.GetIdAsync(application!, Ct);
        var now = new DateTime(2026, 10, 26, 0, 0, 0, DateTimeKind.Utc);

        string? authorisationId = null;
        if (withAuthorisation)
        {
            var authorisation = await scope.ServiceProvider.GetRequiredService<IOpenIddictAuthorizationManager>()
                .CreateAsync(new OpenIddictAuthorizationDescriptor
                {
                    ApplicationId = applicationId,
                    Subject = Guid.NewGuid().ToString(),
                    Status = Statuses.Valid,
                    Type = AuthorizationTypes.Permanent,
                    CreationDate = now,
                }, Ct);
            authorisationId = await scope.ServiceProvider.GetRequiredService<IOpenIddictAuthorizationManager>()
                .GetIdAsync(authorisation, Ct);
        }

        await scope.ServiceProvider.GetRequiredService<IOpenIddictTokenManager>().CreateAsync(
            new OpenIddictTokenDescriptor
            {
                ApplicationId = applicationId,
                AuthorizationId = authorisationId,
                Subject = Guid.NewGuid().ToString(),
                Status = Statuses.Valid,
                Type = TokenTypeHints.RefreshToken,
                CreationDate = now,
                ExpirationDate = now.AddDays(1),
            }, Ct);
    }

    // The refusal's output, its reason, and that nothing was fetched or written.
    private static async Task AssertRefusedAsync(
        WebApplicationFactory<Program> app,
        RecordingHttpHandler handler,
        AdminCommandRun run,
        string reason,
        int requests)
    {
        Assert.Equal((1, ""), (run.ExitCode, run.Output));
        Assert.StartsWith("Refused: ", run.Error, StringComparison.Ordinal);
        Assert.Contains(reason, run.Error, StringComparison.Ordinal);
        Assert.Contains(" Nothing was written.", run.Error, StringComparison.Ordinal);
        Assert.Equal(requests, handler.Requests.Count);
        Assert.Empty(await StoredApplicationsAsync(app));
        Assert.Empty(await StoredEventsAsync(app));
    }

    private static async Task<List<OpenIddictEntityFrameworkCoreApplication<Guid>>> StoredApplicationsAsync(
        WebApplicationFactory<Program> app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>()
            .Set<OpenIddictEntityFrameworkCoreApplication<Guid>>().AsNoTracking().ToListAsync(Ct);
    }

    // Read in SQL, so that neither the tenant filter nor a tracked row decides what is seen.
    private static async Task<List<StoredEvent>> StoredEventsAsync(WebApplicationFactory<Program> app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Database
            .SqlQuery<StoredEvent>($"""
                SELECT [OrganisationId], [ActorUserId], [EventType], [EntityType], [EntityId], [ParticipantId],
                       [Details], [IpAddress]
                FROM [AuditEvent]
                WHERE [EventType] LIKE 'admin.mcp_client_%'
                """)
            .ToListAsync(Ct);
    }

    // One event of the type for each organisation, with only the client and its name, and nothing about who or what.
    private static async Task AssertEventsAsync(
        WebApplicationFactory<Program> app, string eventType, Guid[] organisations, string clientId, string name)
    {
        var events = (await StoredEventsAsync(app)).Where(stored => stored.EventType == eventType).ToList();
        Assert.Equal(organisations.Order(), events.Select(stored => stored.OrganisationId).Order());
        var details = new JsonObject { ["client"] = clientId, ["name"] = name }.ToJsonString();
        Assert.All(events, stored =>
        {
            Assert.Null(stored.ActorUserId);
            Assert.Null(stored.EntityType);
            Assert.Null(stored.EntityId);
            Assert.Null(stored.ParticipantId);
            Assert.Null(stored.IpAddress);
            Assert.Equal(details, stored.Details);
        });
    }

    private sealed record StoredEvent(
        Guid OrganisationId,
        Guid? ActorUserId,
        string EventType,
        string? EntityType,
        Guid? EntityId,
        Guid? ParticipantId,
        string? Details,
        string? IpAddress);

    // A body that says no length, as a chunked response does, so that only the client's limit stops it.
    private sealed class UnknownLengthContent(byte[] bytes) : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            stream.WriteAsync(bytes).AsTask();

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }
}
