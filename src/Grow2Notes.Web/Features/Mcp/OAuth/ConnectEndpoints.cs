using System.Security.Claims;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;

namespace Grow2Notes.Web.Features.Mcp.OAuth;

/// <summary>
/// <c>GET</c> and <c>POST /api/connect</c> (mcp-server.md §4.4): what the Connect screen shows for an authorise request
/// that is waiting for consent, and the person's answer to it. Mapped only where <c>Mcp:Enabled</c> is true, so with
/// the switch off both fall to the <c>/api</c> catch-all's 404.
/// </summary>
/// <remarks>
/// Both carry the <see cref="Policies.SignedIn"/> policy, as a worker has to reach the screen to see why they cannot
/// allow, and a request with no session gets the session cookie's <c>401</c>. A handle that is missing, unreadable or
/// older than <see cref="ConsentRequests.Lifetime"/>, or that names a client that is no longer registered, is a plain
/// <c>404</c>. The details come from the operator's registration of the assistant, never from the assistant, and
/// nothing is fetched. The POST writes nothing: the answer is the same-origin address that issues the code or the
/// refusal (<see cref="AuthoriseEndpoint"/>), with the signed decision on the end, and that navigation writes the
/// audit event.
/// </remarks>
internal static class ConnectEndpoints
{
    /// <summary>Maps both endpoints in the <c>/api</c> group that <see cref="ApiGroup.MapApiGroup"/> returns.</summary>
    public static RouteGroupBuilder MapConnectEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/connect", GetAsync).RequireAuthorization(Policies.SignedIn);
        api.MapPost("/connect", PostAsync).RequireAuthorization(Policies.SignedIn);
        return api;
    }

    private static async Task<Results<Ok<ConnectDetails>, NotFound>> GetAsync(
        string? request,
        ConsentRequests consents,
        IOpenIddictApplicationManager applications,
        CancellationToken cancellationToken)
    {
        if (request is null || consents.Read(request) is not { } consent)
        {
            return TypedResults.NotFound();
        }

        var parameters = QueryHelpers.ParseQuery(consent.Query);
        if (await applications.FindByClientIdAsync(parameters["client_id"].ToString(), cancellationToken)
            is not { } application)
        {
            return TypedResults.NotFound();
        }

        var clientId = (await applications.GetClientIdAsync(application, cancellationToken))!;
        var properties = await applications.GetPropertiesAsync(application, cancellationToken);
        var registeredUris = await applications.GetRedirectUrisAsync(application, cancellationToken);
        return TypedResults.Ok(new ConnectDetails(
            AssistantName: await applications.GetDisplayNameAsync(application, cancellationToken) ?? "",
            Company: Property(ApprovedAssistantProperties.Company),
            DataLocation: Property(ApprovedAssistantProperties.DataLocation),
            ClientIdHost: HostOf(clientId) ?? clientId,
            RedirectHost: HostOf(parameters["redirect_uri"].ToString()) ?? "",
            LoopbackOnly: registeredUris.All(McpClientCommand.IsLoopback)));

        // A client registered by other means may have neither property.
        string Property(string key) => properties.TryGetValue(key, out var value) ? value.GetString() ?? "" : "";
    }

    private static Results<Ok<ConnectNext>, NotFound, ProblemHttpResult> PostAsync(
        ConnectRequest body,
        ClaimsPrincipal user,
        ConsentRequests consents,
        IOptions<IdentityOptions> identityOptions)
    {
        if (consents.Read(body.Request!) is not { } consent)
        {
            return TypedResults.NotFound();
        }

        var allow = body.Allow!.Value;
        if (allow && !user.IsInRole(nameof(UserRole.Manager)) && !user.IsInRole(nameof(UserRole.Support)))
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status403Forbidden);
        }

        var decision = consents.Decide(consent, Actor.InApp(user, identityOptions.Value).UserId, allow);
        return TypedResults.Ok(new ConnectNext($"/oauth/authorize?{consent.Query}&decision={decision}"));
    }

    private static string? HostOf(string address) =>
        Uri.TryCreate(address, UriKind.Absolute, out var uri) ? uri.Host : null;
}

/// <summary>What the Connect screen shows about the assistant, from its registration (mcp-server.md §4.4).</summary>
/// <param name="AssistantName">The name the operator gave the assistant.</param>
/// <param name="Company">The company behind it, in the operator's words.</param>
/// <param name="DataLocation">Where it keeps the data it is given, in the operator's words.</param>
/// <param name="ClientIdHost">The host the client ID names, or the client ID itself when it names none.</param>
/// <param name="RedirectHost">The host the person is sent back to.</param>
/// <param name="LoopbackOnly">Whether every registered redirect URI is on this computer.</param>
internal sealed record ConnectDetails(
    string AssistantName,
    string Company,
    string DataLocation,
    string ClientIdHost,
    string RedirectHost,
    bool LoopbackOnly);

/// <summary>The answer to <c>POST /api/connect</c>: the same-origin address to navigate to.</summary>
internal sealed record ConnectNext(string Next);
