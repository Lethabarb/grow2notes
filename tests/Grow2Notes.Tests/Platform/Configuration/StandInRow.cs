using Grow2Notes.Web.Platform.Configuration;

namespace Grow2Notes.Tests.Platform.Configuration;

/// <summary>A row of an ordered list, such as a participant's goal, for the list helpers' unit tests.</summary>
internal sealed class StandInRow(Guid id, int sortOrder) : IOrderedRow
{
    public Guid Id { get; } = id;

    public int SortOrder { get; set; } = sortOrder;

    public DateTime? ArchivedAtUtc { get; set; }
}
