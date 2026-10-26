using System.Net;
using System.Text.Json;
using Grow2Notes.Tests.Fixtures;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Core;
using OpenIddict.Server;
using OpenIddict.Server.DataProtection;

namespace Grow2Notes.Tests.Features.Mcp;

/// <summary>
/// The authorisation server that <c>Mcp:Enabled</c> adds (mcp-server.md §4.1, §4.2): what its discovery document says,
/// what its token endpoint refuses, and that no authorisation request gets a code before the consent step exists. The
/// app runs on a database of its own, because the tests register an assistant. The host starting at all is the
/// evidence that OpenIddict 7.7.1 accepts the keys and the endpoints chosen for it.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class AuthorisationServerTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    : IClassFixture<Grow2NotesFactory>, IAsyncLifetime
{
    private const string ClientId = "https://assistant.example/client.json";
    private const string RedirectUri = "https://assistant.example/callback";

    private WebApplicationFactory<Program> app = null!;

    public static TheoryData<string> DiscoveryLocations { get; } =
    [
        "/.well-known/oauth-authorization-server",
        "/.well-known/openid-configuration",
    ];

    public static TheoryData<string> UnsupportedGrants { get; } =
    [
        "password",
        "client_credentials",
        "urn:ietf:params:oauth:grant-type:device_code",
        "urn:ietf:params:oauth:grant-type:token-exchange",
    ];

    // Each changes one parameter of the valid request, or removes it.
    public static TheoryData<string, string?> RefusedParameters { get; } = new()
    {
        { "code_challenge", null },
        { "code_challenge_method", "plain" },
        { "response_type", "token" },
        { "scope", "openid" },
        { "resource", "https://grow2notes.example/other" },
        { "redirect_uri", "https://assistant.example/elsewhere" },
        { "client_id", "https://unknown.example/client.json" },
    };

    public async ValueTask InitializeAsync()
    {
        app = await factory.OnDatabaseOfItsOwnAsync(sqlServer, "McpAuthorisationServer");
        await app.RegisterClientAsync(ClientId, applicationType: null, RedirectUri);
    }

    public async ValueTask DisposeAsync() => await app.DisposeAsync();

    [Theory]
    [MemberData(nameof(DiscoveryLocations))]
    public async Task Discovery_describes_a_server_of_authorisation_code_with_PKCE_S256_for_public_clients(string path)
    {
        using var client = app.CreateAssistantClient();

        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        AssertCarriesTheHeadersOfDesignMd(response);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken));
        var metadata = document.RootElement;
        Assert.Equal("https://grow2notes.example/", metadata.GetProperty("issuer").GetString());
        Assert.Equal(
            "https://grow2notes.example/oauth/authorize", metadata.GetProperty("authorization_endpoint").GetString());
        Assert.Equal("https://grow2notes.example/oauth/token", metadata.GetProperty("token_endpoint").GetString());
        AssertExactly(metadata, "grant_types_supported", "authorization_code", "refresh_token");
        AssertExactly(metadata, "response_types_supported", "code");
        AssertExactly(metadata, "code_challenge_methods_supported", "S256");
        AssertExactly(metadata, "scopes_supported", "grow2notes.admin", "offline_access");
        AssertExactly(metadata, "token_endpoint_auth_methods_supported", "none");
        Assert.True(metadata.GetProperty("authorization_response_iss_parameter_supported").GetBoolean());
        Assert.True(metadata.GetProperty("client_id_metadata_document_supported").GetBoolean());
        Assert.False(metadata.TryGetProperty("registration_endpoint", out _));
        Assert.False(metadata.TryGetProperty("jwks_uri", out _));
    }

    [Fact]
    public void Tokens_are_in_Data_Protection_format_and_no_certificate_is_used()
    {
        var services = app.Services;

        var dataProtection = services.GetRequiredService<IOptions<OpenIddictServerDataProtectionOptions>>().Value;
        var server = services.GetRequiredService<IOptions<OpenIddictServerOptions>>().Value;

        Assert.Same(services.GetRequiredService<IDataProtectionProvider>(), dataProtection.DataProtectionProvider);
        var preferences = typeof(OpenIddictServerDataProtectionOptions).GetProperties()
            .Where(property => property.Name.StartsWith("PreferDefault", StringComparison.Ordinal))
            .ToList();
        Assert.NotEmpty(preferences);
        Assert.All(
            preferences, preference => Assert.False((bool)preference.GetValue(dataProtection)!, preference.Name));
        Assert.NotEmpty(server.SigningCredentials);
        Assert.NotEmpty(server.EncryptionCredentials);
        Assert.All(
            server.SigningCredentials, credential => Assert.IsNotAssignableFrom<X509SecurityKey>(credential.Key));
        Assert.All(
            server.EncryptionCredentials, credential => Assert.IsNotAssignableFrom<X509SecurityKey>(credential.Key));
        Assert.Equal([McpTestApp.Resource], server.Resources.Select(resource => resource.AbsoluteUri));
        Assert.True(services.GetRequiredService<IOptions<OpenIddictCoreOptions>>().Value.DisableEntityCaching);
    }

    [Theory]
    [MemberData(nameof(UnsupportedGrants))]
    public async Task The_token_endpoint_refuses_any_other_grant_and_is_never_cached(string grantType)
    {
        using var client = app.CreateAssistantClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/oauth/token")
        {
            Content = new FormUrlEncodedContent([new("grant_type", grantType), new("client_id", ClientId)]),
        };

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString());
        using var error = JsonDocument.Parse(await response.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken));
        Assert.Equal("unsupported_grant_type", error.RootElement.GetProperty("error").GetString());
    }

    [Theory]
    [InlineData("GET", "/.well-known/oauth-protected-resource")]
    [InlineData("POST", "/oauth/revoke")]
    public async Task What_a_later_task_adds_is_still_a_404_problem(string method, string path)
    {
        using var client = app.CreateAssistantClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), path);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        await response.ReadProblemAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_valid_authorisation_request_is_passed_on_and_is_a_404_with_no_code()
    {
        using var client = app.CreateAssistantClient();

        using var response = await client.GetAsync(
            "/oauth/authorize?" + McpTestApp.AuthorizeQuery(ClientId, RedirectUri),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.DoesNotContain("code=", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [MemberData(nameof(RefusedParameters))]
    public async Task An_authorisation_request_that_breaks_a_rule_is_refused_and_gets_no_code(
        string parameter, string? value)
    {
        using var client = app.CreateAssistantClient();

        using var response = await client.GetAsync(
            "/oauth/authorize?" + McpTestApp.AuthorizeQuery(ClientId, RedirectUri, (parameter, value)),
            TestContext.Current.CancellationToken);

        Assert.NotEqual(HttpStatusCode.NotFound, response.StatusCode);
        if (response.Headers.Location is { } location)
        {
            var returned = QueryHelpers.ParseQuery(location.Query + "&" + location.Fragment.TrimStart('#'));
            Assert.DoesNotContain("code", returned.Keys);
            Assert.True(returned.ContainsKey("error"), location.ToString());
            Assert.Equal("https://grow2notes.example/", returned["iss"]);
        }
    }

    private static void AssertExactly(JsonElement metadata, string name, params string[] expected) =>
        Assert.Equal(
            expected.Order(StringComparer.Ordinal),
            metadata.GetProperty(name).EnumerateArray().Select(value => value.GetString()!)
                .Order(StringComparer.Ordinal));

    // Every value of each header, exactly as the app sent them, so a second one would show. HSTS is never sent to
    // localhost, so the client calls the app by its origin.
    private static void AssertCarriesTheHeadersOfDesignMd(HttpResponseMessage response) =>
        Assert.All(DesignedSecurityHeaders.Read(), header =>
        {
            string[] sent = response.Headers.NonValidated.TryGetValues(header.Key, out var values) ? [.. values] : [];
            Assert.Equal(header.Value, Assert.Single(sent));
        });
}
