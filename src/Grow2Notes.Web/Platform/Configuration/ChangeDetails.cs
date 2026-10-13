using System.Text.Json.Nodes;

namespace Grow2Notes.Web.Platform.Configuration;

/// <summary>
/// The audit <c>Details</c> of a change to a configuration row (design.md §5.3, §6.6): <c>{"old": {…}, "new": {…}}</c>
/// with only the values that changed, or, for a new row, <c>{"new": {…}}</c> with all of them. Each value is named and
/// written as the row's list in §6.6 gives it, such as <c>text</c>, <c>sortOrder</c> and <c>archivedAtUtc</c>, never
/// <c>id</c> or <c>rowVersion</c>, and a null value is written as JSON <c>null</c>, so archiving gives
/// <c>"old": {"archivedAtUtc": null}</c> and every new row of a type has the same keys.
/// </summary>
internal static class ChangeDetails
{
    /// <summary>
    /// The details of the change from <paramref name="before"/> to <paramref name="after"/>, or <see langword="null"/>
    /// when no value changed, so there is nothing to save and no event to write (D84). The values are compared as JSON.
    /// </summary>
    /// <param name="before">The row's values as it was loaded, or <see langword="null"/> for a new row.</param>
    /// <param name="after">
    /// The row's values as it is to be saved, naming the same values as <paramref name="before"/>.
    /// </param>
    /// <param name="extraDetails">
    /// Other keys, which go beside <c>old</c> and <c>new</c> and are not compared, such as a reorder's <c>groupId</c>
    /// and Release 2's <c>"via": "mcp"</c> (mcp-server.md §3.5 convention 3, §9).
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="before"/> and <paramref name="after"/> name different values, so a change could go unrecorded;
    /// or <paramref name="extraDetails"/> has an <c>old</c> or <c>new</c> key.
    /// </exception>
    public static JsonObject? Of(JsonObject? before, JsonObject after, JsonObject? extraDetails = null)
    {
        if (extraDetails is not null && (extraDetails.ContainsKey("old") || extraDetails.ContainsKey("new")))
        {
            throw new ArgumentException(
                "Extra details go beside the old and new values, so cannot be named old or new.",
                nameof(extraDetails));
        }

        JsonObject details;
        if (before is null)
        {
            details = new JsonObject { ["new"] = after.DeepClone() };
        }
        else
        {
            if (before.Count != after.Count || after.Any(value => !before.ContainsKey(value.Key)))
            {
                throw new ArgumentException(
                    "The values before and after a change must name the same values.", nameof(after));
            }

            var changed = after.Where(value => !JsonNode.DeepEquals(before[value.Key], value.Value))
                .Select(value => value.Key)
                .ToList();
            if (changed.Count == 0)
            {
                return null;
            }

            details = new JsonObject { ["old"] = Values(before, changed), ["new"] = Values(after, changed) };
        }

        foreach (var (key, value) in extraDetails ?? [])
        {
            details.Add(key, value?.DeepClone());
        }

        return details;
    }

    // Copies, since a JSON node belongs to one parent, and the caller keeps its own.
    private static JsonObject Values(JsonObject values, List<string> keys) =>
        new(keys.Select(key => KeyValuePair.Create(key, values[key]?.DeepClone())));
}
