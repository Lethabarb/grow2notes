using Grow2Notes.Web.Data;

namespace Grow2Notes.Web.Features.CommonItems;

/// <summary>
/// The body of <c>PUT /api/admin/common-items/{id}</c> (design.md §6.6), <c>{text, groupId}</c>: a common item's text
/// as typed, which may be missing, so that <see cref="Platform.Configuration.ItemText.CommonItem"/> refuses it as it
/// refuses an empty one, in the screen's words; and the group to put it in. The screen always sends both; a missing or
/// <c>null</c> <c>groupId</c> keeps the item in its own group, as T06's <c>{text}</c> rewords it (T07's Notes).
/// </summary>
internal sealed record CommonItemUpdateRequest(string? Text, Guid? GroupId)
{
    /// <summary>
    /// This request with the stored item's text where it sent none, for a tool whose <c>text</c> is optional so that
    /// it can move an item alone (A132). The app's request is not completed, so its missing text is still refused.
    /// </summary>
    public CommonItemUpdateRequest OrStored(CommonItem stored) => this with { Text = Text ?? stored.Text };
}
