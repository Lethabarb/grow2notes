using System.Text.Json.Nodes;

namespace Grow2Notes.Web.Platform.Configuration;

/// <summary>
/// The order of a list that managers put in order by hand (design.md §5.3, §6.6). The list and its reorder read the
/// rows with the same SQL, <c>ORDER BY SortOrder, CreatedAtUtc, Id</c>, and these helpers take them in the order that
/// query gives and never sort them: SQL Server orders <c>uniqueidentifier</c> values differently from .NET's
/// <see cref="Guid"/>, so a sort here could put two rows with the same <c>SortOrder</c> the other way round from the
/// list the manager saw.
/// </summary>
internal static class ListOrder
{
    /// <summary>
    /// Puts the list's active rows in the requested order, giving them <c>SortOrder</c> 0 to n-1, when the requested
    /// IDs are each active row exactly once. A missing ID, an extra one (unknown, another list's or archived) or a
    /// repeated one gives <see cref="ErrorCode.OrderMismatch"/>, and no row changes. The order already stored changes
    /// no row either, even where archiving has left gaps or two rows have the same <c>SortOrder</c>, and its old and
    /// new order are the same, so the save finds nothing to change and writes no event (D84).
    /// </summary>
    /// <param name="activeRows">
    /// The list's active rows, in the order the list's query gives them, which is the list's old order.
    /// </param>
    /// <param name="requestedIds">The IDs of the list's active rows, in their new order.</param>
    public static ReorderResult Reorder(IReadOnlyList<IOrderedRow> activeRows, IReadOnlyList<Guid> requestedIds)
    {
        var storedIds = activeRows.Select(row => row.Id).ToList();

        // The set holds a repeated ID once, so a repeat shows in its count.
        var requested = requestedIds.ToHashSet();
        if (requested.Count != requestedIds.Count || !requested.SetEquals(storedIds))
        {
            return new ReorderResult(ErrorCode.OrderMismatch, Order(storedIds), Order(storedIds));
        }

        if (!requestedIds.SequenceEqual(storedIds))
        {
            var rows = activeRows.ToDictionary(row => row.Id);
            for (var sortOrder = 0; sortOrder < requestedIds.Count; sortOrder++)
            {
                rows[requestedIds[sortOrder]].SortOrder = sortOrder;
            }
        }

        return new ReorderResult(Error: null, Order(storedIds), Order(requestedIds));
    }

    // A list's order as a *.reordered event's Details give it, old and new (design.md §5.4, §6.6).
    private static JsonObject Order(IEnumerable<Guid> ids) =>
        new() { ["order"] = new JsonArray([.. ids.Select(id => JsonValue.Create(id))]) };
}
