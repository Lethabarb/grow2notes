namespace Grow2Notes.Web.Features.CommonItems;

/// <summary>
/// The body of <c>POST /api/admin/common-item-groups</c> (design.md §6.6): a group's name as typed. It may be missing,
/// so that <see cref="Platform.Configuration.ItemText.GroupName"/> refuses it as it refuses an empty one, in the
/// screen's words. It holds nothing else, so an <c>isEveryNote</c> in the body is never read, and an add always makes
/// an ordinary group (S01.03.01).
/// </summary>
internal sealed record CommonItemGroupNameRequest(string? Name);
