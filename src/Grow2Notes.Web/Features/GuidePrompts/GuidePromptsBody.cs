using System.ComponentModel.DataAnnotations;
using Grow2Notes.Web.Data;

namespace Grow2Notes.Web.Features.GuidePrompts;

/// <summary>
/// The body of <c>GET</c> and <c>PUT /api/admin/guide-prompts</c> alike, <c>{text}</c> (design.md §6.6): the guide
/// prompts as stored, or as the Guide prompts screen sends them, trimmed at their ends (guide-prompts.md <i>Save</i>).
/// </summary>
/// <remarks>
/// <see cref="Text"/> is required but may be empty, which leaves the Guided notes box with no placeholder (design.md
/// §4.10 <i>States</i>), and is at most <see cref="Limits.GuidePrompts"/> characters, line breaks included (A6). So a
/// <c>PUT</c> whose body has no <c>text</c>, a <c>null</c> one or a longer one gets <c>422 validation.failed</c> on
/// <c>text</c> before the endpoint runs, and never reaches the column, which is not null. The text is stored as sent:
/// the screen trims it, and §6.6 gives the server no trimming of its own. Public, as the validation source generator
/// describes public types only.
/// </remarks>
public sealed record GuidePromptsBody(
    [property: Required(AllowEmptyStrings = true), MaxLength(Limits.GuidePrompts)] string Text);
