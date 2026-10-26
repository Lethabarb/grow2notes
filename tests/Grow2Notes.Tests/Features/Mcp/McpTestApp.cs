using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Features.Mcp;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Grow2Notes.Tests.Features.Mcp;

/// <summary>
/// The app with the AI-assistant access switched on, on a migrated database of the test's own, and the approved
/// assistants that its tests register. A client is registered through <see cref="IOpenIddictApplicationManager"/> with
/// the permissions that the operator command gives, which this repeats rather than calls.
/// </summary>
internal static class McpTestApp
{
    public const string Resource = Grow2NotesFactory.Origin + "/mcp";

    /// <summary>
    /// The app with <c>Mcp:Enabled</c> on, on a database that only the caller uses. <see cref="McpSetup.AddMcp"/> reads
    /// the setting while the app is built, so it is a host setting and not a service.
    /// </summary>
    public static async Task<WebApplicationFactory<Program>> OnDatabaseOfItsOwnAsync(
        this Grow2NotesFactory factory, SqlServerFixture sqlServer, string name)
    {
        var connectionString = await sqlServer.CreateDatabaseAsync(name, TestContext.Current.CancellationToken);
        return factory.WithWebHostBuilder(builder => builder
            .UseSetting(McpSetup.EnabledKey, "true")
            .ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(
                [new("ConnectionStrings:Grow2Notes", connectionString)])));
    }

    /// <summary>
    /// A client that calls the app by its origin, over HTTPS, as an assistant does. It follows no redirect, so a test
    /// sees the status that the app answered with.
    /// </summary>
    public static HttpClient CreateAssistantClient(this WebApplicationFactory<Program> app) =>
        app.CreateClient(new() { BaseAddress = new(Grow2NotesFactory.Origin), AllowAutoRedirect = false });

    /// <summary>
    /// Registers a public client with <paramref name="redirectUris"/>, as the operator command does, with
    /// <paramref name="applicationType"/>, <see cref="ApplicationTypes.Native"/> for the loopback matching or none.
    /// </summary>
    public static async Task RegisterClientAsync(
        this WebApplicationFactory<Program> app, string clientId, string? applicationType, params string[] redirectUris)
    {
        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = clientId,
            ClientType = ClientTypes.Public,
            ApplicationType = applicationType,
            DisplayName = "Test assistant",
            Permissions =
            {
                Permissions.Endpoints.Authorization,
                Permissions.Endpoints.Token,
                Permissions.Endpoints.Revocation,
                Permissions.GrantTypes.AuthorizationCode,
                Permissions.GrantTypes.RefreshToken,
                Permissions.ResponseTypes.Code,
                Permissions.Prefixes.Scope + McpSetup.AdminScope,
                Permissions.Prefixes.Resource + Resource,
            },
        };
        foreach (var redirectUri in redirectUris)
        {
            descriptor.RedirectUris.Add(new(redirectUri));
        }

        await using var scope = app.Services.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        await manager.CreateAsync(descriptor, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// The query of an authorisation request that is valid for a client registered with
    /// <see cref="RegisterClientAsync"/>, with <paramref name="change"/> applied to the parameter it names: the value
    /// replaces the parameter's, or removes it when it is null.
    /// </summary>
    public static string AuthorizeQuery(
        string clientId, string redirectUri, (string Name, string? Value)? change = null)
    {
        var parameters = new Dictionary<string, string?>
        {
            ["client_id"] = clientId,
            ["response_type"] = "code",
            ["redirect_uri"] = redirectUri,
            ["code_challenge"] = "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM",
            ["code_challenge_method"] = "S256",
            ["resource"] = Resource,
            ["scope"] = $"{McpSetup.AdminScope} {Scopes.OfflineAccess}",
            ["state"] = "state-of-the-test",
        };
        if (change is { } changed)
        {
            parameters[changed.Name] = changed.Value;
        }

        return string.Join("&", parameters
            .Where(parameter => parameter.Value is not null)
            .Select(parameter => $"{parameter.Key}={Uri.EscapeDataString(parameter.Value!)}"));
    }
}
