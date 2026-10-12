using System.Buffers.Text;
using System.Text.Json;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Grow2Notes.Web.Platform.Audit;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;

namespace Grow2Notes.Web.Features.Auth;

/// <summary>
/// <c>POST /api/auth/passkey</c> (design.md §6.2, §8.2): signs a person in with the credential that the browser's
/// passkey prompt gave for the options of <c>POST /api/auth/passkey/options</c>, and answers who they are, as a
/// <see cref="Me"/>. The passkey options require user verification, so the passkey is enough for MFA on its own (D23).
/// Any other outcome is the <c>401</c> of a failed sign-in, which starts no session.
/// </summary>
/// <remarks>
/// Anyone may call it, as whoever asks is not signed in yet. It finds the credential's account first, by the
/// credential's ID, and makes that account's organisation the tenant (§5.9 item 1). Identity's
/// <c>PasskeySignInAsync</c> then checks the credential against the ceremony in its two-factor cookie, saves the
/// passkey's new sign count and sets the session cookie. A ceremony that is missing, expired or another operation's
/// fails that check too (<see cref="Grow2NotesSignInManager.PerformPasskeyAssertionAsync"/>). The audit event, of a
/// success or a failure on the account, comes after that save, with no transaction, as §5.9 lists none for a sign-in:
/// if writing it fails, the exception handler's <c>500</c> clears the response's headers, the session cookie's
/// <c>Set-Cookie</c> among them, so no session starts without its event. A credential of no account is audited
/// nowhere (§5.4: known accounts only); it is counted in telemetry by one log entry, which holds no value of the
/// request (§9.5), and Identity is not asked about it.
/// </remarks>
internal static partial class PasskeySignInEndpoint
{
    /// <summary>Maps the endpoint in the <c>/api</c> group, which <see cref="ApiGroup.MapApiGroup"/> returns.</summary>
    public static RouteGroupBuilder MapPasskeySignInEndpoint(this RouteGroupBuilder api)
    {
        api.MapPost("/auth/passkey", SignInAsync).AllowAnonymous();
        return api;
    }

    private static async Task<Results<Ok<Me>, UnauthorizedHttpResult>> SignInAsync(
        PasskeySignInRequest request,
        SignInManager<ApplicationUser> signInManager,
        ITenantContext tenant,
        IAuditWriter audit,
        Grow2NotesDbContext db,
        MelbourneClock clock,
        ILoggerFactory loggers,
        CancellationToken cancellationToken)
    {
        if (CredentialId(request.CredentialJson) is not { } credentialId
            || await signInManager.UserManager.FindByPasskeyIdAsync(credentialId) is not { } user)
        {
            LogUnknownCredential(loggers.CreateLogger(typeof(PasskeySignInEndpoint)));
            return TypedResults.Unauthorized();
        }

        using (tenant.Use(user.OrganisationId))
        {
            var signedIn = (await signInManager.PasskeySignInAsync(request.CredentialJson)).Succeeded;
            await audit.WriteAsync(
                user.OrganisationId,
                actorUserId: user.Id,
                signedIn ? AuditEventTypes.Auth.SignInSucceeded : AuditEventTypes.Auth.SignInFailed,
                AuditEntityTypes.User,
                entityId: user.Id,
                participantId: null,
                details: null,
                cancellationToken);
            return signedIn
                ? TypedResults.Ok(await MeEndpoint.OfUserAsync(user, db, clock, cancellationToken))
                : TypedResults.Unauthorized();
        }
    }

    // Read as Identity reads the credential, with JSON's web defaults, which match a property name in any letter case
    // and let the last of two that match win. Identity signs in the account whose ID the credential's user handle
    // gives, only if that account holds a passkey with the ID it reads, so the account found here by the same ID is
    // the one it signs in, never another that the audit event and the answer would wrongly name. Null where the
    // credential is not a JSON object with a base64url ID.
    private static byte[]? CredentialId(string credentialJson)
    {
        try
        {
            return JsonSerializer.Deserialize<Credential>(credentialJson, JsonSerializerOptions.Web)?.Id is { } id
                && Base64Url.IsValid(id)
                    ? Base64Url.DecodeFromChars(id)
                    : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // Counted in Log Analytics by its message, so the message never changes and holds no value: not the credential's
    // ID, its user handle or the caller's address.
    [LoggerMessage(Level = LogLevel.Information, Message = "Passkey sign-in with an unknown credential")]
    private static partial void LogUnknownCredential(ILogger logger);

    private sealed record Credential(string? Id);
}
