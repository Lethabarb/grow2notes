namespace Grow2Notes.Web.Features.CommonItems;

/// <summary>
/// The body of <c>POST /api/admin/common-item-groups/{groupId}/items</c> (design.md §6.6): a common item's text as
/// typed, and whether ticking it asks for details (A151). The text may be missing, so that
/// <see cref="Platform.Configuration.ItemText.CommonItem"/> refuses it as it refuses an empty one, in the screen's
/// words. A request that leaves <see cref="NeedsDetail"/> out adds an item that does not ask.
/// </summary>
internal sealed record CommonItemTextRequest(string? Text, bool? NeedsDetail = null);
