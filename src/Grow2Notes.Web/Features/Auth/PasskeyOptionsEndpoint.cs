using System.Net.Mime;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;

namespace Grow2Notes.Web.Features.Auth;

/// <summary>
/// <c>POST /api/auth/passkey/options</c> (design.md §6.2): the WebAuthn request options that the SPA hands to the
/// browser's passkey prompt to sign in. They name no user and list no credential, so the browser offers any passkey it
/// holds for the app's domain, and no email is needed.
/// </summary>
/// <remarks>
/// Anyone may call it, as whoever asks is not signed in yet. Identity keeps the ceremony's state, the challenge
/// included, in its two-factor cookie, which the answer sets, for the passkey sign-in to check the credential against.
/// The body is Identity's options JSON itself, not that JSON as a JSON string, as SimpleWebAuthn's
/// <c>startAuthentication({ optionsJSON })</c> takes it.
/// </remarks>
internal static class PasskeyOptionsEndpoint
{
    /// <summary>Maps the endpoint in the <c>/api</c> group, which <see cref="ApiGroup.MapApiGroup"/> returns.</summary>
    public static RouteGroupBuilder MapPasskeyOptionsEndpoint(this RouteGroupBuilder api)
    {
        api.MapPost("/auth/passkey/options", MakeAsync).AllowAnonymous();
        return api;
    }

    private static async Task<ContentHttpResult> MakeAsync(SignInManager<ApplicationUser> signInManager) =>
        TypedResults.Content(
            await signInManager.MakePasskeyRequestOptionsAsync(user: null), MediaTypeNames.Application.Json);
}
