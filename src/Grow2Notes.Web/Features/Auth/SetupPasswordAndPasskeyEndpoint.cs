using Grow2Notes.Web.Data;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;

namespace Grow2Notes.Web.Features.Auth;

/// <summary>
/// <c>POST /api/auth/setup/password-and-passkey</c> (design.md §6.2, §8.1 steps 4 and 5, D97): finishes setup with a
/// password and the passkey that the browser made from <c>POST /api/auth/setup/passkey/options</c>' options, for the
/// user whose <see cref="EnrolmentCookie"/> the request carries; signs the user in, and answers their
/// <see cref="Me"/>.
/// </summary>
/// <remarks>
/// It is <c>AllowAnonymous</c>, as the person has no session yet, and checks the enrolment cookie itself, answering
/// <c>401</c> without a valid one. The two checks that follow only read, so they come before completion's transaction:
/// Identity's password validators first, as <see cref="SetupPasswordEndpoint"/>'s do, with <c>422 validation.failed</c>
/// for <c>password</c> for a password they refuse, and then the credential, as <see cref="SetupPasskeyEndpoint"/>'s
/// does, with <c>422 validation.failed</c> for <c>credentialJson</c> for one that Identity refuses, one sent with no
/// ceremony under way, and one made from another user's options. The password goes first so that a refusal of it leaves
/// the ceremony unused. In the one transaction (<see cref="SetupCompletion"/>), the password and the passkey are added
/// and two-factor sign-in turned on, so the person signs in later with the password and then the passkey (D97); a
/// refusal there, such as the concurrency failure of a completion that another device committed first, throws.
/// </remarks>
internal static class SetupPasswordAndPasskeyEndpoint
{
    /// <summary>Maps the endpoint in the <c>/api</c> group, which <see cref="ApiGroup.MapApiGroup"/> returns.</summary>
    public static RouteGroupBuilder MapSetupPasswordAndPasskeyEndpoint(this RouteGroupBuilder api)
    {
        api.MapPost("/auth/setup/password-and-passkey", CompleteAsync).AllowAnonymous();
        return api;
    }

    private static async Task<Results<Ok<Me>, UnauthorizedHttpResult, ProblemHttpResult>> CompleteAsync(
        SetupPasswordAndPasskeyRequest request,
        HttpContext context,
        SignInManager<ApplicationUser> signInManager,
        SetupCompletion completion,
        CancellationToken cancellationToken)
    {
        var users = signInManager.UserManager;

        // With no body, which the status code pages make problem details with no design.md §6.9 code.
        if (await EnrolmentCookie.FindInvitedUserAsync(context, users) is not { } invited)
        {
            return TypedResults.Unauthorized();
        }

        if (await SetupPasswordEndpoint.PasswordRefusalAsync(users, invited, request.Password) is { } refusal)
        {
            return refusal;
        }

        if (await SetupPasskeyEndpoint.AttestAsync(signInManager, request.CredentialJson, invited) is not { } passkey)
        {
            return SetupPasskeyEndpoint.PasskeyRefused();
        }

        var me = await completion.CompleteAsync(
            invited,
            async user =>
            {
                SetupCompletion.ThrowIfRefused(await users.AddPasswordAsync(user, request.Password));
                SetupCompletion.ThrowIfRefused(await users.AddOrUpdatePasskeyAsync(user, passkey));
                SetupCompletion.ThrowIfRefused(await users.SetTwoFactorEnabledAsync(user, enabled: true));
            },
            cancellationToken);
        return me is null ? TypedResults.Unauthorized() : TypedResults.Ok(me);
    }
}
