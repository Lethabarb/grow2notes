using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;

namespace Grow2Notes.Web.Features.Auth;

/// <summary>
/// <c>POST /api/auth/setup/start</c> (design.md §6.2, §8.1 step 3): checks a setup link's user ID and token, then
/// issues the <see cref="EnrolmentCookie"/> for the user, and answers who the link is for, as a
/// <see cref="SetupStartResponse"/>.
/// </summary>
/// <remarks>
/// Anyone may call it, since the person opening the link has no session. A link that does not check gets the one
/// <c>410 auth.setup_link_invalid</c>, whatever the reason: an ID that is not a GUID or names no user, a token that
/// does not verify, or a user who is not Invited; so the answer never says whether an account exists. The token holds
/// the user's security stamp, which nothing here changes, so the same link starts setup again, as when the enrolment
/// cookie has ended, until setup completes or the token's 7 days pass. Users are not tenant-owned, so it needs no
/// tenant.
/// </remarks>
internal static class SetupStartEndpoint
{
    /// <summary>Maps the endpoint in the <c>/api</c> group, which <see cref="ApiGroup.MapApiGroup"/> returns.</summary>
    public static RouteGroupBuilder MapSetupStartEndpoint(this RouteGroupBuilder api)
    {
        api.MapPost("/auth/setup/start", StartAsync).AllowAnonymous();
        return api;
    }

    private static async Task<Results<Ok<SetupStartResponse>, ProblemHttpResult>> StartAsync(
        SetupStartRequest request, HttpContext context, UserManager<ApplicationUser> users)
    {
        // FindByIdAsync throws on an ID that is not a GUID, so it is parsed first.
        if (!Guid.TryParse(request.UserId, out var userId)
            || await users.FindByIdAsync(userId.ToString()) is not { Status: UserStatus.Invited } user
            || !await users.VerifyUserTokenAsync(
                user, SetupTokenProvider.ProviderName, SetupTokenProvider.Purpose, request.Token))
        {
            return ErrorCode.SetupLinkInvalid.Problem();
        }

        await EnrolmentCookie.SignInAsync(context, users, user);

        // RequireUniqueEmail refuses an account without an address, so every user has one.
        return TypedResults.Ok(new SetupStartResponse(user.Email!, user.DisplayName));
    }
}
