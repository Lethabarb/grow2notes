namespace Grow2Notes.Web.Features.Mcp.Contracts;

/// <summary>
/// What <c>set_guide_prompts</c> returns (mcp-server.md §5.2): whether the prompts changed, and their version.
/// </summary>
/// <param name="Changed">Whether the call changed anything; text that is already stored saves nothing.</param>
/// <param name="ExpectedVersion">The prompts' version now, which a later change needs.</param>
/// <param name="HelpText">What a change to the guide prompts does (design.md §4.10).</param>
internal sealed record GuidePromptsChange(bool Changed, string ExpectedVersion, string HelpText);
