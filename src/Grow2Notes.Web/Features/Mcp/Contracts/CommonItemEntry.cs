namespace Grow2Notes.Web.Features.Mcp.Contracts;

/// <summary>A common item that a tool returned.</summary>
/// <param name="ItemId">The item's ID.</param>
/// <param name="Text">As typed.</param>
/// <param name="Archived">Whether the item is archived.</param>
/// <param name="ExpectedVersion">The item's version, the app's ETag token, which a later change needs.</param>
internal sealed record CommonItemEntry(Guid ItemId, string Text, bool Archived, string ExpectedVersion);
