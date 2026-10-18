namespace Grow2Notes.Web.Features.CommonItems;

/// <summary>
/// What <see cref="CommonItemGroupChanges.AddAsync"/> did: added the group, whose new ID is <paramref name="Id"/> and
/// whose <paramref name="Errors"/> are <see langword="null"/>; or refused the name, with its message in
/// <paramref name="Errors"/>, and added nothing, its <paramref name="Id"/> then <see cref="Guid.Empty"/>.
/// </summary>
internal sealed record AddCommonItemGroupResult(Guid Id, IReadOnlyDictionary<string, string[]>? Errors);
