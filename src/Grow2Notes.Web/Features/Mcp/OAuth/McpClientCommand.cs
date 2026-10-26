using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Grow2Notes.Web.Platform.Audit;
using Grow2Notes.Web.Platform.OperatorCommands;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Grow2Notes.Web.Features.Mcp.OAuth;

/// <summary>
/// <c>admin mcp-client</c> (mcp-server.md §11.2, design.md §7.4): approves an AI assistant by registering it as a
/// public OpenIddict client, lists the approved assistants, and removes one with its authorisations and tokens. It
/// works whether or not <c>Mcp:Enabled</c> is on, because OpenIddict's core and stores are always registered. Every
/// check, and the one fetch of a metadata document, comes before the transaction, so that a refused run has written
/// nothing and a retried transaction never fetches again. The registration change and one
/// <c>admin.mcp_client_*</c> event for each organisation commit together, and the result is printed once they have.
/// It logs nothing, because the output holds the result and nothing else.
/// </summary>
internal sealed class McpClientCommand(
    Grow2NotesDbContext db,
    IOpenIddictApplicationManager applications,
    ClientMetadataDocuments documents,
    IAuditWriter audit,
    ITenantContext tenant,
    IConfiguration configuration)
{
    /// <summary>The command's name, after <c>admin</c>.</summary>
    public const string Name = "mcp-client";

    /// <summary>How the command is used, as the operator types it.</summary>
    public const string Usage =
        "Usage: admin mcp-client add (--metadata-url <url> | --client-id <id> --redirect-uri <uri> " +
        "[--redirect-uri <uri> ...]) --name \"<name>\" --company \"<company>\" --data-location \"<where>\"; " +
        "admin mcp-client list; admin mcp-client remove <client-id>";

    private const string MetadataUrlOption = "--metadata-url";
    private const string ClientIdOption = "--client-id";
    private const string RedirectUriOption = "--redirect-uri";
    private const string NameOption = "--name";
    private const string CompanyOption = "--company";
    private const string DataLocationOption = "--data-location";

    // The options that give a text of the registration, all required, and the longest value of each.
    private static readonly (string Name, int MaxLength)[] TextOptions =
    [
        (NameOption, Limits.McpAssistantName),
        (CompanyOption, Limits.McpCompany),
        (DataLocationOption, Limits.McpDataLocation),
    ];

    private static readonly string[] LoopbackHosts = ["127.0.0.1", "[::1]", "localhost"];

    private static readonly string[] OtherOptions = [MetadataUrlOption, ClientIdOption, RedirectUriOption];

    /// <summary>
    /// Runs the subcommand that <paramref name="arguments"/> names, <c>add</c>, <c>list</c> or <c>remove</c>. A refused
    /// run says why on <paramref name="error"/> and has written nothing. <c>add</c> refuses a wrong argument, an
    /// <c>App:Origin</c> that cannot name the resource, a metadata document that fails its checks, and a database
    /// with no organisation. <c>remove</c> refuses an unknown client ID.
    /// </summary>
    /// <param name="arguments">The subcommand and then its arguments, as typed after <c>admin mcp-client</c>.</param>
    /// <param name="output">Where the result is written, once it has committed.</param>
    /// <param name="error">Where the reason for a refusal is written.</param>
    /// <param name="cancellationToken">Cancels the fetch and the database work.</param>
    /// <returns><see cref="AdminCommands.Done"/>, or <see cref="AdminCommands.Refused"/>.</returns>
    public async Task<int> RunAsync(
        IReadOnlyList<string> arguments, TextWriter output, TextWriter error, CancellationToken cancellationToken) =>
        arguments switch
        {
            ["add", ..] => await AddAsync([.. arguments.Skip(1)], output, error, cancellationToken),
            ["list"] => await ListAsync(output, cancellationToken),
            ["list", var extra, ..] => await AdminCommands.RefuseAsync(
                error, $"list takes no arguments, but \"{extra}\" was given.", Usage),
            ["remove"] => await AdminCommands.RefuseAsync(error, "No client ID was given.", Usage),
            ["remove", var clientId] => await RemoveAsync(clientId, output, error, cancellationToken),
            ["remove", _, var extra, ..] => await AdminCommands.RefuseAsync(
                error, $"remove takes one client ID, but \"{extra}\" was also given.", Usage),
            [var subcommand, ..] => await AdminCommands.RefuseAsync(
                error, $"\"{subcommand}\" is not a subcommand of admin mcp-client.", Usage),
            _ => await AdminCommands.RefuseAsync(error, "No subcommand was given.", Usage),
        };

    private async Task<int> AddAsync(
        IReadOnlyList<string> options, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        if (!TryRead(options, out var values, out var problem))
        {
            return await AdminCommands.RefuseAsync(error, problem, Usage);
        }

        if (AdminCommands.FindOriginProblem(configuration, "the resource that the assistant is given cannot be named.")
            is { } originProblem)
        {
            return await AdminCommands.RefuseAsync(error, originProblem, usage: null);
        }

        var registration = values.Registration;
        string? documentName = null;
        if (values.MetadataUrl is { } metadataUrl)
        {
            if (MetadataUrlProblem(metadataUrl) is { } urlProblem)
            {
                return await AdminCommands.RefuseAsync(error, urlProblem, usage: null);
            }

            var fetched = await documents.FetchAsync(new Uri(metadataUrl), cancellationToken);
            if (fetched.Refusal is not null)
            {
                return await AdminCommands.RefuseAsync(error, fetched.Refusal, usage: null);
            }

            if (!TryReadDocument(fetched.Document!, metadataUrl, out var document, out var documentProblem))
            {
                return await AdminCommands.RefuseAsync(error, documentProblem, usage: null);
            }

            registration = registration with { RedirectUris = document.RedirectUris };
            documentName = document.Name;
        }

        var descriptor = Describe(registration, McpSetup.Resource(configuration));

        // Set by each attempt, as a transient failure runs the whole work again, so that it holds the outcome of the
        // attempt that committed.
        Outcome outcome = null!;
        await db.InTransactionAsync(async ct =>
        {
            var organisations = await db.Organisations.Select(row => row.Id).ToListAsync(ct);
            if (organisations.Count == 0)
            {
                outcome = Outcome.Refused("There is no organisation yet: run admin bootstrap first.");
                return;
            }

            var existing = await applications.FindByClientIdAsync(registration.ClientId, ct);
            if (existing is not null && await applications.GetClientIdAsync(existing, ct) != registration.ClientId)
            {
                // The database ignores case, so it finds a client ID that differs only in case, which is another one.
                outcome = Outcome.Refused("A client ID that differs from it only in letter case is already approved.");
                return;
            }

            if (existing is null)
            {
                await applications.CreateAsync(descriptor, ct);
            }
            else
            {
                await applications.UpdateAsync(existing, descriptor, ct);
            }

            var eventType = existing is null
                ? AuditEventTypes.Admin.McpClientAdded
                : AuditEventTypes.Admin.McpClientUpdated;
            await WriteAuditAsync(organisations, eventType, registration.ClientId, registration.Name, ct);
            outcome = Outcome.Done(existing is null ? "Added" : "Updated");
        }, cancellationToken);

        if (outcome.Refusal is not null)
        {
            return await AdminCommands.RefuseAsync(error, outcome.Refusal, usage: null);
        }

        await output.WriteLineAsync($"{outcome.Verb} {registration.ClientId}");
        foreach (var line in Block(registration))
        {
            await output.WriteLineAsync(line);
        }

        if (documentName is not null)
        {
            await output.WriteLineAsync($"Document client_name: {documentName}");
        }

        return AdminCommands.Done;
    }

    private async Task<int> ListAsync(TextWriter output, CancellationToken cancellationToken)
    {
        var registrations = new List<Registration>();
        await foreach (var application in applications.ListAsync(count: null, offset: null, cancellationToken))
        {
            registrations.Add(await ReadAsync(application, cancellationToken));
        }

        // Ordered here, not by the database, which ignores case.
        var first = true;
        foreach (var registration in registrations.OrderBy(row => row.ClientId, StringComparer.Ordinal))
        {
            if (!first)
            {
                await output.WriteLineAsync();
            }

            first = false;
            foreach (var line in Block(registration))
            {
                await output.WriteLineAsync(line);
            }
        }

        return AdminCommands.Done;
    }

    private async Task<int> RemoveAsync(
        string clientId, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        Outcome outcome = null!;
        await db.InTransactionAsync(async ct =>
        {
            var existing = await applications.FindByClientIdAsync(clientId, ct);
            if (existing is null || await applications.GetClientIdAsync(existing, ct) != clientId)
            {
                outcome = Outcome.Refused($"No approved assistant has the client ID \"{clientId}\".");
                return;
            }

            var organisations = await db.Organisations.Select(row => row.Id).ToListAsync(ct);
            var name = await applications.GetDisplayNameAsync(existing, ct);

            // OpenIddict deletes the client's tokens and authorisations first, in this transaction.
            await applications.DeleteAsync(existing, ct);
            await WriteAuditAsync(organisations, AuditEventTypes.Admin.McpClientRemoved, clientId, name, ct);
            outcome = Outcome.Done("Removed");
        }, cancellationToken);

        if (outcome.Refusal is not null)
        {
            return await AdminCommands.RefuseAsync(error, outcome.Refusal, usage: null);
        }

        await output.WriteLineAsync($"{outcome.Verb} {clientId}");
        return AdminCommands.Done;
    }

    // One event for each organisation, as admin signout-all writes them, because an approved assistant is for every
    // organisation's managers. The event names the client and its name, never a redirect URI.
    private async Task WriteAuditAsync(
        List<Guid> organisations, string eventType, string clientId, string? name, CancellationToken cancellationToken)
    {
        foreach (var organisationId in organisations)
        {
            using (tenant.Use(organisationId))
            {
                await audit.WriteAsync(
                    organisationId, actorUserId: null, eventType, entityType: null, entityId: null, participantId: null,
                    new JsonObject { ["client"] = clientId, ["name"] = name }, cancellationToken);
            }
        }
    }

    // By hand, since six options need no command-line library. An option's value is the argument after it, whatever it
    // is, so a value left out shows as a missing option or as an argument that is not an option. Only --redirect-uri
    // may be given more than once.
    private static bool TryRead(
        IReadOnlyList<string> options,
        [NotNullWhen(true)] out AddValues? values,
        [NotNullWhen(false)] out string? problem)
    {
        values = null;
        var given = new Dictionary<string, string>(StringComparer.Ordinal);
        var redirectUris = new List<string>();
        for (var i = 0; i < options.Count; i += 2)
        {
            var option = options[i];
            if (!OtherOptions.Contains(option) && !TextOptions.Any(known => known.Name == option))
            {
                problem = $"\"{option}\" is not an option of admin mcp-client add.";
                return false;
            }

            if (i + 1 == options.Count)
            {
                problem = $"{option} has no value.";
                return false;
            }

            if (option == RedirectUriOption)
            {
                redirectUris.Add(options[i + 1]);
            }
            else if (!given.TryAdd(option, options[i + 1]))
            {
                problem = $"{option} is given more than once.";
                return false;
            }
        }

        given.TryGetValue(MetadataUrlOption, out var metadataUrl);
        given.TryGetValue(ClientIdOption, out var givenClientId);
        problem = (metadataUrl, givenClientId, redirectUris) switch
        {
            (null, null, _) => $"One of {MetadataUrlOption} and {ClientIdOption} is needed.",
            (not null, not null, _) => $"{MetadataUrlOption} and {ClientIdOption} cannot both be given.",
            (not null, null, [_, ..]) =>
                $"{RedirectUriOption} is only for {ClientIdOption}: the document gives the redirect URIs.",
            (null, not null, []) => $"{ClientIdOption} needs at least one {RedirectUriOption}.",
            _ => null,
        };
        if (problem is not null)
        {
            return false;
        }

        // Spaces alone are empty too, as admin bootstrap has them.
        var texts = new List<string>();
        foreach (var (option, maxLength) in TextOptions)
        {
            var text = given.GetValueOrDefault(option)?.Trim();
            problem = text switch
            {
                null => $"{option} is missing.",
                "" => $"{option} is empty.",
                { Length: var length } when length > maxLength => $"{option} is longer than {maxLength} characters.",
                _ when HasControlOrFormat(text) => $"{option} has a control or formatting character.",
                _ => null,
            };
            if (problem is not null)
            {
                return false;
            }

            texts.Add(text!);
        }

        // Taken exactly as given, as it is the client ID, which OpenIddict compares exactly.
        var (idOption, id) = metadataUrl is null ? (ClientIdOption, givenClientId!) : (MetadataUrlOption, metadataUrl);
        problem = id switch
        {
            _ when string.IsNullOrWhiteSpace(id) => $"{idOption} is empty.",
            { Length: > Limits.McpClientId } => $"{idOption} is longer than {Limits.McpClientId} characters.",
            _ when id.Any(char.IsWhiteSpace) => $"{idOption} has white space.",
            _ when HasControlOrFormat(id) => $"{idOption} has a control or formatting character.",
            _ => redirectUris.Select(RedirectUriProblem).FirstOrDefault(found => found is not null),
        };
        if (problem is not null)
        {
            return false;
        }

        values = new(metadataUrl, new(id, texts[0], texts[1], texts[2], redirectUris));
        return true;
    }

    private static string? MetadataUrlProblem(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var url))
        {
            return "The metadata URL is not an absolute address.";
        }

        return url switch
        {
            { Scheme: not "https" } => "The metadata URL must be https.",
            { AbsolutePath: "/" } => "The metadata URL must have a path.",
            { UserInfo.Length: > 0 } => "The metadata URL must have no user information.",
            _ when value.Contains('#') => "The metadata URL must have no fragment.",

            // AbsoluteUri is the URL in plain form: a dot segment, a default port or capitals in the host change it.
            _ when url.AbsoluteUri != value => "The metadata URL must be in plain form, with no dot segments.",
            _ => null,
        };
    }

    private static bool TryReadDocument(
        JsonObject document,
        string metadataUrl,
        [NotNullWhen(true)] out MetadataDocument? read,
        [NotNullWhen(false)] out string? problem)
    {
        read = null;
        if (Text(document["client_id"]) != metadataUrl)
        {
            problem = "The document's client_id is not exactly the URL.";
            return false;
        }

        if (Text(document["client_name"]) is not { } name || string.IsNullOrWhiteSpace(name))
        {
            problem = "The document has no client_name.";
            return false;
        }

        if (HasControlOrFormat(name))
        {
            problem = "The document's client_name has a control or formatting character.";
            return false;
        }

        if (document["redirect_uris"] is not JsonArray { Count: > 0 } array)
        {
            problem = "The document's redirect_uris is missing or empty.";
            return false;
        }

        var redirectUris = array.Select(Text).ToList();
        if (redirectUris.Contains(null))
        {
            problem = "The document's redirect_uris holds something that is not a string.";
            return false;
        }

        problem = redirectUris.Select(uri => RedirectUriProblem(uri!)).FirstOrDefault(found => found is not null);
        if (problem is null
            && document.TryGetPropertyValue("token_endpoint_auth_method", out var method)
            && Text(method) != "none")
        {
            problem = "The document's token_endpoint_auth_method is not none.";
        }

        if (problem is not null)
        {
            return false;
        }

        read = new(name, redirectUris!);
        return true;
    }

    private static string? Text(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    // The rule for every redirect URI, from the operator or from a document (RFC 8252 §7.3): https, or http on a
    // loopback host. It is stored as given, as OpenIddict compares redirect URIs exactly.
    private static string? RedirectUriProblem(string value)
    {
        if (HasControlOrFormat(value))
        {
            return "A redirect URI has a control or formatting character.";
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return $"The redirect URI \"{value}\" is not an absolute address.";
        }

        return uri switch
        {
            { UserInfo.Length: > 0 } => $"The redirect URI \"{value}\" has user information.",
            _ when value.Contains('#') => $"The redirect URI \"{value}\" has a fragment.",
            { Scheme: "https" } => null,
            { Scheme: "http" } when IsLoopback(value) => null,
            _ => $"The redirect URI \"{value}\" must be https, or http on 127.0.0.1, [::1] or localhost.",
        };
    }

    // Decided on the host as it is written, since Uri turns "loopback" into "localhost" and other spellings into
    // 127.0.0.1, and Uri.IsLoopback accepts all of 127.0.0.0/8: RFC 8252 §7.3 names these three hosts only.
    private static bool IsLoopback(string value)
    {
        var schemeEnd = value.IndexOf("://", StringComparison.Ordinal);
        if (schemeEnd < 0)
        {
            return false;
        }

        var authority = value[(schemeEnd + 3)..];
        var end = authority.IndexOfAny(['/', '?', '#']);
        if (end >= 0)
        {
            authority = authority[..end];
        }

        var host = authority.StartsWith('[')
            ? authority[..(authority.IndexOf(']') + 1)]
            : authority.Split(':')[0];
        return LoopbackHosts.Contains(host, StringComparer.OrdinalIgnoreCase);
    }

    // Controls include ANSI escapes, and formatting characters include the bidirectional overrides: neither belongs in
    // a value that is stored and printed. Runes, not chars, so that the invisible tag characters, which are outside the
    // basic plane, are found too.
    private static bool HasControlOrFormat(string value) =>
        value.EnumerateRunes().Any(
            rune => Rune.GetUnicodeCategory(rune) is UnicodeCategory.Control or UnicodeCategory.Format);

    private static OpenIddictApplicationDescriptor Describe(Registration registration, string resource)
    {
        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = registration.ClientId,
            ClientType = ClientTypes.Public,
            DisplayName = registration.Name,
            Properties =
            {
                [ApprovedAssistantProperties.Company] = JsonSerializer.SerializeToElement(registration.Company),
                [ApprovedAssistantProperties.DataLocation] =
                    JsonSerializer.SerializeToElement(registration.DataLocation),
            },
            Permissions =
            {
                Permissions.Endpoints.Authorization,
                Permissions.Endpoints.Token,
                Permissions.Endpoints.Revocation,
                Permissions.GrantTypes.AuthorizationCode,
                Permissions.GrantTypes.RefreshToken,
                Permissions.ResponseTypes.Code,
                Permissions.Prefixes.Scope + McpSetup.AdminScope,
                Permissions.Prefixes.Resource + resource,
            },
        };

        // Native turns on OpenIddict's loopback matching, which ignores the port. Nothing reads "web".
        if (registration.RedirectUris.Any(uri => IsLoopback(uri)))
        {
            descriptor.ApplicationType = ApplicationTypes.Native;
        }

        foreach (var uri in registration.RedirectUris)
        {
            descriptor.RedirectUris.Add(new Uri(uri));
        }

        return descriptor;
    }

    private async Task<Registration> ReadAsync(object application, CancellationToken cancellationToken)
    {
        var properties = await applications.GetPropertiesAsync(application, cancellationToken);
        return new(
            (await applications.GetClientIdAsync(application, cancellationToken))!,
            (await applications.GetDisplayNameAsync(application, cancellationToken))!,
            properties[ApprovedAssistantProperties.Company].GetString()!,
            properties[ApprovedAssistantProperties.DataLocation].GetString()!,
            await applications.GetRedirectUrisAsync(application, cancellationToken));
    }

    private static IEnumerable<string> Block(Registration registration) =>
    [
        $"Client ID: {registration.ClientId}",
        $"Name: {registration.Name}",
        $"Company: {registration.Company}",
        $"Data location: {registration.DataLocation}",
        .. registration.RedirectUris.Select(uri => $"Redirect URI: {uri}"),
    ];

    private sealed record Registration(
        string ClientId, string Name, string Company, string DataLocation, IReadOnlyList<string> RedirectUris);

    private sealed record AddValues(string? MetadataUrl, Registration Registration);

    private sealed record MetadataDocument(string Name, IReadOnlyList<string> RedirectUris);

    private sealed record Outcome(string? Verb, string? Refusal)
    {
        public static Outcome Done(string verb) => new(verb, Refusal: null);

        public static Outcome Refused(string reason) => new(Verb: null, reason);
    }
}
