using System.Net.Mime;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;

namespace Grow2Notes.Web.Features.Auth;

/// <summary>
/// <c>POST /api/auth/setup/passkey/options</c> (design.md §6.2, §8.1 step 4): the WebAuthn creation options that the
/// SPA hands to the browser's passkey prompt to make the passkey that setup stores, for the user whose
/// <see cref="EnrolmentCookie"/> the request carries.
/// </summary>
/// <remarks>
/// It is <c>AllowAnonymous</c>, as the person has no session yet, and checks the enrolment cookie itself, answering
/// <c>401</c> without a valid one. The user entity is the user's ID, email and display name, with no organisation
/// name (D42), and the options ask for user verification and a discoverable credential for the app's own domain
/// (<see cref="IdentityPasskeyOptions"/> in Program.cs). Identity keeps the ceremony's state, the challenge included,
/// in its two-factor cookie, which the answer sets, for the completion to check the passkey against. Nothing here
/// changes the user's row, so the security stamp, and with it the setup link, stays as it was. The body is Identity's
/// options JSON itself, not that JSON as a JSON string, as SimpleWebAuthn's <c>startRegistration({ optionsJSON })</c>
/// takes it.
/// </remarks>
internal static class SetupPasskeyOptionsEndpoint
{
    /// <summary>Maps the endpoint in the <c>/api</c> group, which <see cref="ApiGroup.MapApiGroup"/> returns.</summary>
    public static RouteGroupBuilder MapSetupPasskeyOptionsEndpoint(this RouteGroupBuilder api)
    {
        api.MapPost("/auth/setup/passkey/options", MakeAsync).AllowAnonymous();
        return api;
    }

    private static async Task<Results<ContentHttpResult, UnauthorizedHttpResult>> MakeAsync(
        HttpContext context, SignInManager<ApplicationUser> signInManager)
    {
        // With no body, which the status code pages make problem details with no design.md §6.9 code.
        if (await EnrolmentCookie.FindInvitedUserAsync(context, signInManager.UserManager) is not { } user)
        {
            return TypedResults.Unauthorized();
        }

        // RequireUniqueEmail refuses an account without an address, so every user has one.
        var options = await signInManager.MakePasskeyCreationOptionsAsync(new()
        {
            Id = user.Id.ToString(),
            Name = user.Email!,
            DisplayName = user.DisplayName,
        });
        return TypedResults.Content(options, MediaTypeNames.Application.Json);
    }
}
