using System.ComponentModel;
using System.Diagnostics;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Features.GuidePrompts;
using Grow2Notes.Web.Features.Mcp.Contracts;
using Grow2Notes.Web.Platform;
using Grow2Notes.Web.Platform.Audit;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Grow2Notes.Web.Features.Mcp.Tools;

/// <summary>
/// The tools that read and replace the organisation's guide prompts (mcp-server.md §5.2), through the handler that
/// <c>GET</c> and <c>PUT /api/admin/guide-prompts</c> call, as <see cref="Actor.InMcp"/>. The prompts are one value of
/// the organisation, so a read reports no ID to <see cref="McpCall"/> and its audit row has a count of 0, and a change
/// writes its own event through the handler, or the same row when it changes nothing.
/// </summary>
[McpServerToolType]
internal sealed class GuidePromptsTools(GuidePromptsHandler prompts, IHttpContextAccessor httpContextAccessor)
{
    private const string GuidePromptsHelpText =
        "Changes apply straight away to every empty Guided notes box, including open drafts. Prompts are a guide " +
        "only. They disappear as soon as someone starts typing and are not saved into notes.";

    private const string GuidePromptsChanged =
        "The guide prompts were changed since you looked them up. Look them up again with get_guide_prompts, " +
        "then retry.";

    [McpServerTool(
        Name = "get_guide_prompts",
        Title = "Get the guide prompts",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true,
        OutputSchemaType = typeof(StoredGuidePrompts))]
    [Description(
        "The guide prompts of this organisation, exactly as stored with their line breaks, and the expectedVersion " +
        "that a later change needs. The text is empty when there are no prompts. " +
        ToolResults.TypedByPeople)]
    public async Task<CallToolResult> GetGuidePromptsAsync(CancellationToken cancellationToken = default)
    {
        var stored = await prompts.GetAsync(ToolActor.Of(httpContextAccessor), cancellationToken);
        return ToolResults.Of(new StoredGuidePrompts(stored.Text, stored.Token));
    }

    [McpServerTool(
        Name = "set_guide_prompts",
        Title = "Replace the guide prompts",
        ReadOnly = false,
        Destructive = true,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true,
        OutputSchemaType = typeof(GuidePromptsChange))]
    [Description(
        "Replaces the guide prompts, the grey text in an empty Guided notes box. Needs the expectedVersion that " +
        "get_guide_prompts returned. Line breaks are kept, the ends are trimmed, and empty text means no prompts. " +
        "At most 1,000 characters. Text that is already stored answers changed: false. The result holds the new " +
        "expectedVersion, and its helpText says what a change does.")]
    public async Task<CallToolResult> SetGuidePromptsAsync(
        [Description("The new prompts: at most 1,000 characters, line breaks allowed. Empty text means no prompts.")]
        string text,
        [Description("The expectedVersion that get_guide_prompts returned.")]
        string? expectedVersion = null,
        CancellationToken cancellationToken = default)
    {
        if (TextInput.Refusal("text", "Guide prompts", text, Limits.GuidePrompts, allowLineBreaks: true) is { } refusal)
        {
            return refusal.ToResult();
        }

        var result = await prompts.SaveAsync(
            ToolActor.Of(httpContextAccessor),
            IfMatch.FromExpectedVersion(expectedVersion),
            text.Trim(),
            cancellationToken);
        if (!result.Succeeded)
        {
            return ErrorOf(result.Error).ToResult();
        }

        return ToolResults.Of(new GuidePromptsChange(result.Changed, result.Token, GuidePromptsHelpText));
    }

    // The refusal that the handler gave with a design.md §6.9 code, in words for the model. A code that this tool
    // cannot get is a bug, and McpAuditFilter turns it into unexpected.
    private static ToolError ErrorOf(ErrorCode code)
    {
        if (code == ErrorCode.PreconditionRequired)
        {
            return ToolError.PreconditionRequired;
        }

        if (code == ErrorCode.PreconditionFailed)
        {
            return new(code.Code, GuidePromptsChanged);
        }

        throw new UnreachableException($"A guide prompts tool cannot be refused with {code.Code}.");
    }
}
