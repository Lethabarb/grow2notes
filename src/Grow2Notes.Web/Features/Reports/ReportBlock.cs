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
}

/// <summary>
/// One paragraph of a report file. Both renderers read the same list of blocks, so the Word and PDF files cannot say
/// different things. For a <see cref="ReportBlockKind.Tick"/> block, <paramref name="Text"/> is the goal or common item
/// text only and each renderer prints the box before it; for a <see cref="ReportBlockKind.Line"/> block it may hold any
/// line endings.
/// </summary>
internal sealed record ReportBlock(ReportBlockKind Kind, string Text, bool IsTicked = false);
