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

    /// <summary>
    /// The <c>SortOrder</c> of a row put at the end of the list, as an added row, a restored one and an item moved to
    /// another group are (A2, A42): one more than the highest active <c>SortOrder</c>, or 0 when the list has no active
    /// row. Archiving leaves gaps, which only a reorder closes, so a row put at the count of active rows could share
    /// its <c>SortOrder</c> with one already there.
    /// </summary>
    /// <param name="rows">The list's rows, in any order; the archived ones are not counted.</param>
    public static int End(IEnumerable<IOrderedRow> rows) =>
        rows.Where(row => row.ArchivedAtUtc is null).Select(row => row.SortOrder + 1).DefaultIfEmpty(0).Max();

    // A list's order as a *.reordered event's Details give it, old and new (design.md §5.4, §6.6).
    private static JsonObject Order(IEnumerable<Guid> ids) =>
        new() { ["order"] = new JsonArray([.. ids.Select(id => JsonValue.Create(id))]) };
}
