namespace Grow2Notes.Web.Features.Mcp.Contracts;

/// <summary>A common item group that a tool returned, with its items.</summary>
/// <param name="GroupId">The group's ID.</param>
/// <param name="Name">As typed.</param>
/// <param name="IsEveryNote">Whether this is the Every note group, which is fixed and always first.</param>
/// <param name="Archived">Whether the group is archived.</param>
/// <param name="ExpectedVersion">The group's version, the app's ETag token, which a later change needs.</param>
/// <param name="Items">The group's items in the app's order, the archived ones after the active ones.</param>
internal sealed record CommonItemGroupEntry(
    Guid GroupId,
    string Name,
    bool IsEveryNote,
    bool Archived,
    string ExpectedVersion,
    IReadOnlyList<CommonItemEntry> Items);
