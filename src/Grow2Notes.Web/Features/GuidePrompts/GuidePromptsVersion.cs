using Grow2Notes.Web.Platform.Configuration;

namespace Grow2Notes.Web.Features.GuidePrompts;

/// <summary>
/// The guide prompts as stored, with the version token of the organisation row that holds them
/// (<see cref="RowVersionToken.Of"/>), which a change sends back as its <c>If-Match</c> (design.md §6.6, §6.7).
/// </summary>
internal sealed record GuidePromptsVersion(string Text, string Token);
