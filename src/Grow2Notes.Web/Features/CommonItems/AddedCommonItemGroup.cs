namespace Grow2Notes.Web.Features.CommonItems;

/// <summary>
/// The body of <c>POST /api/admin/common-item-groups</c>'s <c>201</c>, <c>{id}</c>: the new group's ID, by which the
/// screen finds the new group's New item field once it has read the list again (T07's Notes).
/// </summary>
internal sealed record AddedCommonItemGroup(Guid Id);
