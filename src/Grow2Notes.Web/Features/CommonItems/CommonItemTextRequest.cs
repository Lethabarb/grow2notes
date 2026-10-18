namespace Grow2Notes.Web.Features.CommonItems;

/// <summary>
/// The body of <c>POST /api/admin/common-item-groups/{groupId}/items</c> (design.md §6.6): a common item's text as
/// typed. It may be missing, so that <see cref="Platform.Configuration.ItemText.CommonItem"/> refuses it as it refuses
/// an empty one, in the screen's words.
/// </summary>
internal sealed record CommonItemTextRequest(string? Text);
