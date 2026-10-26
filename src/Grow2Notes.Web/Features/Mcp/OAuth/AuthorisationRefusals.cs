using System.Globalization;
using Grow2Notes.Web.Data;
using Microsoft.AspNetCore;
using OpenIddict.Server;
using static OpenIddict.Server.OpenIddictServerEvents;

namespace Grow2Notes.Web.Features.Mcp.OAuth;

/// <summary>
/// What becomes of an authorisation request that OpenIddict refuses (mcp-server.md §4.1). One whose client and
/// redirect URI were accepted has somewhere safe to send the error, so it is sent there, with <c>state</c> and
/// <c>iss</c>, by OpenIddict itself. Any other, such as an unknown client or a redirect URI that is not registered,
/// must not be sent to the assistant: the client ID is logged, and the person's browser goes to the general error on
/// the Connect screen.
/// </summary>
internal sealed partial class AuthorisationRefusals(ILoggerFactory loggers)
    : IOpenIddictServerHandler<ApplyAuthorizationResponseContext>
{
    /// <summary>
    /// The transaction property that holds the redirect URI once the client and the URI are accepted. A request
    /// that is refused before that never gets it.
    /// </summary>
    public const string ValidatedRedirectUri = "grow2notes.validated_redirect_uri";

    public static OpenIddictServerHandlerDescriptor Descriptor { get; } =
        OpenIddictServerHandlerDescriptor.CreateBuilder<ApplyAuthorizationResponseContext>()
            .UseSingletonHandler<AuthorisationRefusals>()
            .SetOrder(OpenIddictServerHandlers.Authentication.AttachRedirectUri.Descriptor.Order + 500)
            .SetType(OpenIddictServerHandlerType.Custom)
            .Build();

    // The authorise step's messages share one category, that of the endpoint.
    private readonly ILogger logger = loggers.CreateLogger(typeof(AuthoriseEndpoint));

    public ValueTask HandleAsync(ApplyAuthorizationResponseContext context)
    {
        // An error that the app raised itself already has the redirect URI, which OpenIddict attached.
        if (string.IsNullOrEmpty(context.Response.Error) || !string.IsNullOrEmpty(context.RedirectUri))
        {
            return ValueTask.CompletedTask;
        }

        if (context.Transaction.Properties.TryGetValue(ValidatedRedirectUri, out var validated)
            && validated is string redirectUri)
        {
            context.RedirectUri = redirectUri;
        }
        else if (context.Transaction.GetHttpRequest() is { } request)
        {
            LogRefused(logger, ForLog(context.Request?.ClientId), context.Response.Error);
            request.HttpContext.Response.Redirect(AuthoriseEndpoint.ConnectPath);
            context.HandleRequest();
        }

        return ValueTask.CompletedTask;
    }

    // The client ID is whatever the caller sent: without control or line-separating characters, so that it cannot
    // forge a line of the log, and cut, so that it cannot fill it, though never in the middle of a surrogate pair.
    private static string ForLog(string? clientId)
    {
        if (string.IsNullOrEmpty(clientId))
        {
            return "(none)";
        }

        var clean = string.Concat(clientId.Where(character => !char.IsControl(character)
            && char.GetUnicodeCategory(character)
                is not (UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator)));
        if (clean.Length <= Limits.McpClientId)
        {
            return clean;
        }

        var end = char.IsHighSurrogate(clean[Limits.McpClientId - 1]) ? Limits.McpClientId - 1 : Limits.McpClientId;
        return clean[..end] + "…";
    }

    [LoggerMessage(
        Level = LogLevel.Warning, Message = "Refused an authorisation request from client {ClientId}: {Error}")]
    private static partial void LogRefused(ILogger logger, string clientId, string? error);
}
