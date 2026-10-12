using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;

namespace Grow2Notes.Web.Features.Auth;

/// <summary>
/// <c>POST /api/auth/setup/password</c> (design.md §6.2, §8.1 steps 4 and 5): finishes setup with a password and the
/// authenticator app that holds the key the invite stored, for the user whose <see cref="EnrolmentCookie"/> the
/// request carries, once the app's code checks; signs the user in, and answers their <see cref="Me"/>.
/// </summary>
/// <remarks>
/// It is <c>AllowAnonymous</c>, as the person has no session yet, and checks the enrolment cookie itself, answering
/// <c>401</c> without a valid one. The code is read by the one code check (<see cref="OneTimeCode"/>) and verified with
/// Identity's authenticator provider, as Identity's own template does, so a wrong code counts towards no lockout and
/// writes no audit event, design.md §5.4's <c>auth.mfa_failed</c> being a sign-in's. A badly formed code and a wrong
/// one get the same <c>422 validation.failed</c> for <c>totpCode</c> (D70) and change nothing. Then Identity's password
/// validators check the password, and one that they refuse gets <c>422 validation.failed</c> for <c>password</c> and
/// changes nothing. Both checks come before completion's transaction, as they only read. In the transaction
/// (<see cref="SetupCompletion"/>), the password is added and two-factor sign-in turned on, so no password ever signs
/// anyone in alone (§8.1's invariant); a refusal there, such as the concurrency failure of a completion that another
/// device committed first, throws, as the passkey path's does. No recovery codes are made (A22).
/// </remarks>
internal static class SetupPasswordEndpoint
{
    /// <summary>Maps the endpoint in the <c>/api</c> group, which <see cref="ApiGroup.MapApiGroup"/> returns.</summary>
    public static RouteGroupBuilder MapSetupPasswordEndpoint(this RouteGroupBuilder api)
    {
        api.MapPost("/auth/setup/password", CompleteAsync).AllowAnonymous();
        return api;
    }

    private static async Task<Results<Ok<Me>, UnauthorizedHttpResult, ProblemHttpResult>> CompleteAsync(
        SetupPasswordRequest request,
        HttpContext context,
        UserManager<ApplicationUser> users,
        SetupCompletion completion,
        CancellationToken cancellationToken)
    {
        // With no body, which the status code pages make problem details with no design.md §6.9 code.
        if (await EnrolmentCookie.FindInvitedUserAsync(context, users) is not { } invited)
        {
            return TypedResults.Unauthorized();
        }

        // The SPA shows its own words for either refusal (sign-in.md's Setup states), not these.
        if (!OneTimeCode.TryRead(request.TotpCode, out var code)
            || !await users.VerifyTwoFactorTokenAsync(invited, users.Options.Tokens.AuthenticatorTokenProvider, code))
        {
            return ValidationProblems.Field(nameof(SetupPasswordRequest.TotpCode), "The code did not work.");
        }

        // UserManager's own check, which AddPasswordAsync makes again in the transaction, is protected, so its
        // validators run here as it runs them. Only their refusal is the password's: AddPasswordAsync's others, such as
        // another completion committed since the transaction loaded the user, are no reason to ask for a new password.
        List<IdentityResult> checks = [];
        foreach (var validator in users.PasswordValidators)
        {
            checks.Add(await validator.ValidateAsync(users, invited, request.Password));
        }

        if (!checks.TrueForAll(check => check.Succeeded))
        {
            return ValidationProblems.Field(
                nameof(SetupPasswordRequest.Password),
                string.Join(" ", checks.SelectMany(check => check.Errors).Select(error => error.Description)));
        }

        var me = await completion.CompleteAsync(
            invited,
            async user =>
            {
                SetupCompletion.ThrowIfRefused(await users.AddPasswordAsync(user, request.Password));
                SetupCompletion.ThrowIfRefused(await users.SetTwoFactorEnabledAsync(user, enabled: true));
            },
            cancellationToken);
        return me is null ? TypedResults.Unauthorized() : TypedResults.Ok(me);
    }
}
