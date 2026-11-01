using System.Net.Mime;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;

namespace Grow2Notes.Web.Features.Auth;

/// <summary>
/// <c>POST /api/auth/login/passkey/options</c> (design.md §6.2, §8.2): the WebAuthn request options for the passkey
/// step of a sign-in with a password and a passkey (D97), which the SPA hands to the browser's passkey prompt once
/// <c>POST /api/auth/login</c> has accepted the password and answered <c>{"next":"passkey"}</c>. They list only that
/// account's passkeys, and the credential then goes to <c>POST /api/auth/passkey</c>, which signs the person in.
/// </summary>
/// <remarks>
/// Anyone may call it, as whoever asks is not signed in yet; it has no body, as the account is the one in Identity's
/// two-factor cookie, which the password step set, read before anything else. Without one, because no password step
/// came first, its 5 minutes have passed or a passkey ceremony has already replaced it, the answer is the <c>401</c>
/// with nothing written. Otherwise the account uses a permit of the code step's limit of 10 a minute (design.md §9.9,
/// <see cref="AccountRateLimiter"/>), which both second steps share, and a request over it is a <c>429</c>. An account
/// that holds no passkey (<see cref="SecondStep"/>) gets the <c>401</c> too, and its cookie stays as it is, so a person
/// with an authenticator app can still take the code step. The options are Identity's own for that user: it keeps the
/// ceremony's state, the user's ID included, in the two-factor cookie, which replaces the password step, so each
/// accepted password yields one set of options, and the credential of another account fails the check in
/// <c>POST /api/auth/passkey</c>. The 5 minutes of the cookie start again here. It writes nothing and reads only
/// Identity's user and passkey tables, which no tenant filters, so it sets no tenant.
/// </remarks>
internal static class PasskeyStepOptionsEndpoint
{
    /// <summary>Maps the endpoint in the <c>/api</c> group, which <see cref="ApiGroup.MapApiGroup"/> returns.</summary>
    public static RouteGroupBuilder MapPasskeyStepOptionsEndpoint(this RouteGroupBuilder api)
    {
        api.MapPost("/auth/login/passkey/options", MakeAsync).AllowAnonymous();
        return api;
    }

    private static async Task<Results<ContentHttpResult, UnauthorizedHttpResult, StatusCodeHttpResult>> MakeAsync(
        AccountRateLimiter accounts,
        HttpResponse response,
        SignInManager<ApplicationUser> signInManager)
    {
        if (await signInManager.GetTwoFactorAuthenticationUserAsync() is not { } user)
        {
            return TypedResults.Unauthorized();
        }

        if (!accounts.TryAcquireCodeStep(user.Id))
        {
            return RateLimits.TooManyRequests(response);
        }

        return await SecondStep.IsPasskeyAsync(signInManager.UserManager, user)
            ? TypedResults.Content(
                await signInManager.MakePasskeyRequestOptionsAsync(user), MediaTypeNames.Application.Json)
            : TypedResults.Unauthorized();
    }
}
