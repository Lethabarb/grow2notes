using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;

namespace Grow2Notes.Web.Features.Auth;

/// <summary>
/// <c>POST /api/auth/setup/passkey</c> (design.md §6.2, §8.1 steps 4 and 5): finishes setup with the passkey that the
/// browser made from <c>POST /api/auth/setup/passkey/options</c>' options, for the user whose
/// <see cref="EnrolmentCookie"/> the request carries, signs the user in, and answers their <see cref="Me"/>.
/// </summary>
/// <remarks>
/// It is <c>AllowAnonymous</c>, as the person has no session yet, and checks the enrolment cookie itself, answering
/// <c>401</c> without a valid one. Identity checks the credential against the ceremony that the options started, which
/// it keeps in its two-factor cookie and ends here, so each options object serves one attempt. A credential that
/// Identity refuses, one sent with no ceremony under way, and one made from another user's options get
/// <c>422 validation.failed</c> for <c>credentialJson</c> and change nothing: a <c>400</c> with no code would be taken
/// by the SPA's <c>api()</c> wrapper for a stale antiforgery token and sent again (S00.04.02's Notes). The passkey is
/// stored in completion's transaction (<see cref="SetupCompletion"/>).
/// </remarks>
internal static class SetupPasskeyEndpoint
{
    // The name of every passkey that setup stores (D70), which no screen shows or asks for, whatever name the request
    // sends.
    private const string PasskeyName = "Grow2Notes";

    /// <summary>Maps the endpoint in the <c>/api</c> group, which <see cref="ApiGroup.MapApiGroup"/> returns.</summary>
    public static RouteGroupBuilder MapSetupPasskeyEndpoint(this RouteGroupBuilder api)
    {
        api.MapPost("/auth/setup/passkey", CompleteAsync).AllowAnonymous();
        return api;
    }

    private static async Task<Results<Ok<Me>, UnauthorizedHttpResult, ProblemHttpResult>> CompleteAsync(
        SetupPasskeyRequest request,
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

        PasskeyAttestationResult attestation;
        try
        {
            attestation = await signInManager.PerformPasskeyAttestationAsync(request.CredentialJson);
        }
        catch (InvalidOperationException)
        {
            // Identity throws, rather than failing, when its two-factor cookie holds no passkey creation: none was
            // started in this browser, its 5 minutes have passed, an earlier attempt used it, or a sign-in replaced it.
            return PasskeyRefused();
        }

        // The user entity is the one the options were made for, which Identity kept with the ceremony; another user's,
        // when the browser started setup with a second link after asking for options with the first.
        if (!attestation.Succeeded || attestation.UserEntity.Id != invited.Id.ToString())
        {
            return PasskeyRefused();
        }

        attestation.Passkey.Name = PasskeyName;
        return await completion.CompleteAsync(
            invited,
            async user =>
            {
                SetupCompletion.ThrowIfRefused(await users.AddOrUpdatePasskeyAsync(user, attestation.Passkey));
                return null;
            },
            cancellationToken);
    }

    // The SPA shows its own words for it (sign-in.md's Passkey not set up), not these.
    private static ProblemHttpResult PasskeyRefused() =>
        ValidationProblems.Field(nameof(SetupPasskeyRequest.CredentialJson), "The passkey was not set up.");
}
