using System.ComponentModel;
using System.Security.Claims;
using System.Text.Json;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Features.GuidePrompts;
using Grow2Notes.Web.Features.Mcp.Contracts;
using Grow2Notes.Web.Platform;
using Grow2Notes.Web.Platform.Audit;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;
using OpenIddict.Abstractions;
using ModelContextProtocol.Server;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Grow2Notes.Tests.Features.Mcp;

/// <summary>
/// Tools that only the tests have, so that they can see a call as the server runs it: the app ships no tool in T32, and
/// <see cref="AssistantFlow.StartAsync"/> adds these to its app unless it is asked for the app as production has it.
/// </summary>
[McpServerToolType]
internal sealed class ProbeTools
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>The result of <c>probe_whoami</c> that <paramref name="result"/> holds.</summary>
    public static WhoAmI ReadWhoAmI(CallToolResult result)
    {
        Assert.NotEqual(true, result.IsError);
        var text = Assert.Single(result.Content.OfType<TextContentBlock>()).Text;
        return JsonSerializer.Deserialize<WhoAmI>(text, Json)!;
    }

    [McpServerTool(Name = "probe_whoami", ReadOnly = true)]
    [Description("Test stand-in: who the call is for, what the call's principal holds and what the tenant sees.")]
    public static async Task<WhoAmI> WhoAmIAsync(
        IServiceProvider services, [Description("Echoed back.")] string? echo = null, CancellationToken ct = default)
    {
        var user = services.GetRequiredService<IHttpContextAccessor>().HttpContext!.User;
        return new(
            Guid.Parse(user.FindFirstValue(Claims.Subject)!),
            services.GetRequiredService<ITenantContext>().OrganisationId,
            [.. user.Claims.Select(claim => new ClaimView(claim.Type, claim.Value))],
            [.. user.GetAudiences()],
            await ReturnParticipantsAsync(services, ct),
            echo);
    }

    [McpServerTool(Name = "probe_set_guide_prompts")]
    [Description("Test stand-in: replaces the guide prompts as Release 1's handler does, as the person of the token.")]
    public static async Task<string> SetGuidePromptsAsync(
        IServiceProvider services, [Description("The new prompts.")] string text, CancellationToken ct = default)
    {
        var handler = services.GetRequiredService<GuidePromptsHandler>();
        var actor = ActorOf(services);
        var version = await handler.GetAsync(actor, ct);
        var saved = await handler.SaveAsync(actor, IfMatch.ETag(version.Token), text, ct);
        return saved.Succeeded ? "stored" : saved.Error.ToString()!;
    }

    [McpServerTool(Name = "probe_refuse")]
    [Description("Test stand-in: refuses with the not_found error.")]
    public static CallToolResult Refuse() => new ToolError("not_found", "Nothing here.").ToResult();

    [McpServerTool(Name = "probe_fail")]
    [Description("Test stand-in: changes the guide prompts in a transaction, then throws with the canary.")]
    public static async Task FailAsync(
        IServiceProvider services, [Description("Goes into the exception's message.")] string canary,
        CancellationToken ct = default)
    {
        var handler = services.GetRequiredService<GuidePromptsHandler>();
        var actor = ActorOf(services);
        await services.GetRequiredService<Grow2NotesDbContext>().InTransactionAsync(
            async token =>
            {
                var version = await handler.GetAsync(actor, token);
                await handler.SaveAsync(actor, IfMatch.ETag(version.Token), "Changed, then rolled back.", token);
                throw new InvalidOperationException($"The canary {canary} must reach no log.");
            },
            ct);
    }

    [McpServerTool(Name = "probe_abort", ReadOnly = true)]
    [Description("Test stand-in: looks up the participants, then drops the connection before the answer is sent.")]
    public static async Task AbortAsync(IServiceProvider services, CancellationToken ct = default)
    {
        await ReturnParticipantsAsync(services, ct);
        services.GetRequiredService<IHttpContextAccessor>().HttpContext!.Abort();
    }

    private static Actor ActorOf(IServiceProvider services) =>
        Actor.InMcp(services.GetRequiredService<IHttpContextAccessor>().HttpContext!.User);

    // The look-up of a tool: what the tenant-filtered context returns, reported as every look-up reports it.
    private static async Task<List<Guid>> ReturnParticipantsAsync(IServiceProvider services, CancellationToken ct)
    {
        var participantIds = await services.GetRequiredService<Grow2NotesDbContext>().Participants
            .Select(participant => participant.Id).OrderBy(id => id).ToListAsync(ct);
        services.GetRequiredService<McpCall>().Returned(McpCall.ParticipantIds, participantIds);
        return participantIds;
    }
}

/// <summary>
/// What <c>probe_whoami</c> answers: the person the call is for, the organisation the call runs in, every claim of the
/// principal, the audiences of the token (which OpenIddict holds in a private claim, not in <c>aud</c>), and the
/// participants that the call's database context can see.
/// </summary>
public sealed record WhoAmI(
    Guid UserId,
    Guid OrganisationId,
    IReadOnlyList<ClaimView> Claims,
    IReadOnlyList<string> Audiences,
    IReadOnlyList<Guid> ParticipantIds,
    string? Echo)
{
    /// <summary>The values of the principal's claims of any of <paramref name="types"/>.</summary>
    public string[] Values(params string[] types) =>
        [.. Claims.Where(claim => types.Contains(claim.Type)).Select(claim => claim.Value)];
}

/// <summary>A claim of the principal that a call runs as.</summary>
public sealed record ClaimView(string Type, string Value);
