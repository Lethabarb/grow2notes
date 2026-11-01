using Grow2Notes.Web.Data;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;

namespace Grow2Notes.Web.Features.Auth;

/// <summary>
/// <c>POST /api/auth/setup/password-only</c> (design.md §6.2, §8.1 steps 4 and 5, D97): finishes setup with a
/// password and nothing else, for the user whose <see cref="EnrolmentCookie"/> the request carries; signs the user in,
/// and answers their <see cref="Me"/>.
/// </summary>
/// <remarks>
/// It is <c>AllowAnonymous</c>, as the person has no session yet, and checks the enrolment cookie itself, answering
/// <c>401</c> without a valid one. Identity's password validators check the password before completion's transaction,
/// as <see cref="SetupPasswordEndpoint"/>'s do, and one that they refuse gets <c>422 validation.failed</c> for
/// <c>password</c> and changes nothing. In the transaction (<see cref="SetupCompletion"/>), the password is added and
/// two-factor sign-in stays off, so the person signs in later with the password alone (D97); a refusal there, such as
/// the concurrency failure of a completion that another device committed first, throws.
/// </remarks>
internal static class SetupPasswordOnlyEndpoint
{
    /// <summary>Maps the endpoint in the <c>/api</c> group, which <see cref="ApiGroup.MapApiGroup"/> returns.</summary>
    public static RouteGroupBuilder MapSetupPasswordOnlyEndpoint(this RouteGroupBuilder api)
    {
        api.MapPost("/auth/setup/password-only", CompleteAsync).AllowAnonymous();
        return api;
    }

    private static async Task<Results<Ok<Me>, UnauthorizedHttpResult, ProblemHttpResult>> CompleteAsync(
        SetupPasswordOnlyRequest request,
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

        if (await SetupPasswordEndpoint.PasswordRefusalAsync(users, invited, request.Password) is { } refusal)
        {
            return refusal;
        }

        var me = await completion.CompleteAsync(
            invited,
            async user => SetupCompletion.ThrowIfRefused(await users.AddPasswordAsync(user, request.Password)),
            cancellationToken);
        return me is null ? TypedResults.Unauthorized() : TypedResults.Ok(me);
    }
}
