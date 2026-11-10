namespace Grow2Notes.Web.Features.Reports;

/// <summary>What a <see cref="ReportBlock"/> is, which each renderer maps to a style of its own.</summary>
internal enum ReportBlockKind
{
    Title,
    Subtitle,
    Heading1,
    Heading2,
    Heading3,
    Line,
    Tick,

    /// <summary>A participant in the index: the name, a dot leader and the page their block starts on.</summary>
    IndexEntry,

    /// <summary>A row of boxes, each with its label, side by side: the attendance ticks.</summary>
    TickRow,
}

/// <summary>A box in a <see cref="ReportBlockKind.TickRow"/> and the label it is printed with.</summary>
internal sealed record ReportTick(string Text, bool IsTicked);

/// <summary>
/// One paragraph of a report file. Both renderers read the same list of blocks, so the Word and PDF files cannot say
/// different things. For a <see cref="ReportBlockKind.Tick"/> block, <paramref name="Text"/> is the goal or common item
/// text only and each renderer prints the box before it; for a <see cref="ReportBlockKind.Line"/> block it may hold any
/// line endings. A <see cref="ReportBlockKind.TickRow"/> holds its boxes in <see cref="Ticks"/> and no text. A
/// <see cref="ReportBlockKind.Heading1"/> may start a new page and may name a place that an
/// <see cref="ReportBlockKind.IndexEntry"/> points at by <see cref="Anchor"/>.
/// </summary>
internal sealed record ReportBlock(ReportBlockKind Kind, string Text, bool IsTicked = false)
{
    /// <summary>Whether a heading starts a new page.</summary>
    public bool PageBreakBefore { get; init; }

    /// <summary>
    /// The name of the place that a heading marks and that an index entry's page number is read from. It is made of
    /// letters and digits only, never of a participant's name.
    /// </summary>
    public string? Anchor { get; init; }

    /// <summary>The boxes of a <see cref="ReportBlockKind.TickRow"/>.</summary>
    public IReadOnlyList<ReportTick> Ticks { get; init; } = [];

    /// <summary>Two blocks are equal when they say the same, the boxes of a tick row included.</summary>
    public bool Equals(ReportBlock? other)
        => other is not null
            && Kind == other.Kind
            && Text == other.Text
            && IsTicked == other.IsTicked
            && PageBreakBefore == other.PageBreakBefore
            && Anchor == other.Anchor
            && Ticks.SequenceEqual(other.Ticks);

    public override int GetHashCode() => HashCode.Combine(Kind, Text, IsTicked, PageBreakBefore, Anchor, Ticks.Count);
}
