using System.ComponentModel;
using System.Security.Claims;
using System.Text.Json;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
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
        var db = services.GetRequiredService<Grow2NotesDbContext>();
        return new(
            Guid.Parse(user.FindFirstValue(Claims.Subject)!),
            services.GetRequiredService<ITenantContext>().OrganisationId,
            [.. user.Claims.Select(claim => new ClaimView(claim.Type, claim.Value))],
            [.. user.GetAudiences()],
            await db.Participants.Select(participant => participant.Id).OrderBy(id => id).ToListAsync(ct),
            echo);
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
