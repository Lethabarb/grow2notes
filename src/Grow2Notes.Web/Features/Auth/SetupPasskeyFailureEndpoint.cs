using Grow2Notes.Web.Data;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;

namespace Grow2Notes.Web.Features.Auth;

/// <summary>
/// <c>POST /api/auth/setup/passkey/failure</c> (design.md §6.2, D103): logs why the browser made no passkey at setup,
/// for the user whose <see cref="EnrolmentCookie"/> the request carries, and answers <c>204</c>. A passkey prompt
/// that fails, on the device or as the person dismisses it, never reaches the server otherwise, so this is the only
/// record of it.
/// </summary>
/// <remarks>
/// It is <c>AllowAnonymous</c>, as the person has no session yet, and checks the enrolment cookie itself, answering
/// <c>401</c> and logging nothing without a valid one, so only someone with a setup link under way can write to the
/// log. It changes nothing and writes no audit event. The log entry holds the browser's words, which name no person,
/// with control characters made spaces, so that a value cannot start a line of its own in the console log.
/// </remarks>
internal static partial class SetupPasskeyFailureEndpoint
{
    /// <summary>Maps the endpoint in the <c>/api</c> group, which <see cref="ApiGroup.MapApiGroup"/> returns.</summary>
    public static RouteGroupBuilder MapSetupPasskeyFailureEndpoint(this RouteGroupBuilder api)
    {
        api.MapPost("/auth/setup/passkey/failure", RecordAsync).AllowAnonymous();
        return api;
    }

    private static async Task<Results<NoContent, UnauthorizedHttpResult>> RecordAsync(
        SetupPasskeyFailureRequest request,
        HttpContext context,
        UserManager<ApplicationUser> users,
        ILoggerFactory loggers)
    {
        // With no body, which the status code pages make problem details with no design.md §6.9 code.
        if (await EnrolmentCookie.FindInvitedUserAsync(context, users) is null)
        {
            return TypedResults.Unauthorized();
        }

        LogFailure(
            loggers.CreateLogger(typeof(SetupPasskeyFailureEndpoint)),
            OneLine(request.Name),
            OneLine(request.Code),
            OneLine(request.Message));
        return TypedResults.NoContent();
    }

    private static string OneLine(string? text) =>
        string.Create(text?.Length ?? 0, text, (span, value) =>
        {
            for (var i = 0; i < span.Length; i++)
            {
                span[i] = char.IsControl(value![i]) ? ' ' : value[i];
            }
        });

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Passkey setup failed in the browser: {ErrorName}, code {ErrorCode}: {ErrorMessage}")]
    private static partial void LogFailure(ILogger logger, string errorName, string errorCode, string errorMessage);
}
