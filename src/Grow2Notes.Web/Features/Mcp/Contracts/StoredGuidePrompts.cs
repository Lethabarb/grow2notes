namespace Grow2Notes.Web.Features.Mcp.Contracts;

/// <summary>What <c>get_guide_prompts</c> returns (mcp-server.md §5.2): the organisation's guide prompts.</summary>
/// <param name="Text">Exactly as stored, line breaks kept; empty when there are no prompts.</param>
/// <param name="ExpectedVersion">The prompts' version, which a later change needs.</param>
internal sealed record StoredGuidePrompts(string Text, string ExpectedVersion);
