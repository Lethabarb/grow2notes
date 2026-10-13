namespace Grow2Notes.Web.Platform.Configuration;

/// <summary>
/// A row of a list that managers put in order (design.md §5.3): a participant's goals, the common item groups other
/// than Every note, or one group's items. <c>SortOrder</c> is not unique, so a reorder needs no temporary values, and
/// two rows added at once can have the same one; the list reads those in <c>CreatedAtUtc</c>, then <c>Id</c>, order.
/// <see cref="ListOrder"/> puts the rows in order.
/// </summary>
internal interface IOrderedRow
{
    Guid Id { get; }

    int SortOrder { get; set; }
}
