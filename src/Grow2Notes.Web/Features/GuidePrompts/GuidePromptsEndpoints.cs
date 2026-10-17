using System.Security.Claims;
using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Grow2Notes.Web.Features.GuidePrompts;

/// <summary>
/// <c>GET</c> and <c>PUT /api/admin/guide-prompts</c> (design.md §6.6): the signed-in manager's organisation's guide
/// prompts, as <see cref="GuidePromptsHandler"/> reads and replaces them, for the Guide prompts screen (§4.10). The
/// <c>GET</c> answers <c>{text}</c> with the row's <c>ETag</c>; the <c>PUT</c> of <c>{text}</c> needs that ETag as its
/// <c>If-Match</c>, and answers <c>200</c> with the row's <c>ETag</c> after the save, so the next <c>If-Match</c> needs
/// no refetch, the same one when the text was already stored (D84).
/// </summary>
/// <remarks>
/// Both are mapped in the <c>/api/admin</c> group, whose <see cref="Policies.Manager"/> policy gives a worker a
/// <c>403</c> and a request with no session a <c>401</c>. A <c>PUT</c> whose body breaks the rules of
/// <see cref="GuidePromptsBody"/> gets <c>422 validation.failed</c> before it runs; one with no <c>If-Match</c> gets
/// <c>428 precondition.required</c>, and one whose <c>If-Match</c> is stale, or whose row changed while it was saved,
/// <c>412 precondition.failed</c>, each changing nothing.
/// </remarks>
internal static class GuidePromptsEndpoints
{
    /// <summary>
    /// Maps both in the <c>/api/admin</c> group, which <see cref="ApiGroup.MapAdminGroup"/> returns.
    /// </summary>
    public static RouteGroupBuilder MapGuidePromptsEndpoints(this RouteGroupBuilder admin)
    {
        admin.MapGet("/guide-prompts", GetAsync);
        admin.MapPut("/guide-prompts", PutAsync);
        return admin;
    }

    private static async Task<Ok<GuidePromptsBody>> GetAsync(
        ClaimsPrincipal user,
        IOptions<IdentityOptions> identityOptions,
        HttpResponse response,
        GuidePromptsHandler guidePrompts,
        CancellationToken cancellationToken)
    {
        var stored = await guidePrompts.GetAsync(Actor.InApp(user, identityOptions.Value), cancellationToken);
        response.Headers.ETag = IfMatch.ETag(stored.Token);
        return TypedResults.Ok(new GuidePromptsBody(stored.Text));
    }

    private static async Task<Results<Ok, ProblemHttpResult>> PutAsync(
        GuidePromptsBody body,
        ClaimsPrincipal user,
        IOptions<IdentityOptions> identityOptions,
        HttpRequest request,
        HttpResponse response,
        GuidePromptsHandler guidePrompts,
        CancellationToken cancellationToken)
    {
        var saved = await guidePrompts.SaveAsync(
            Actor.InApp(user, identityOptions.Value), request.Headers.IfMatch, body.Text, cancellationToken);
        if (!saved.Succeeded)
        {
            return saved.Error.Problem();
        }

        response.Headers.ETag = IfMatch.ETag(saved.Token);
        return TypedResults.Ok();
    }
}
