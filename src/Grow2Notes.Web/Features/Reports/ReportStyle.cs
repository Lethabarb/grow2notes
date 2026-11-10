namespace Grow2Notes.Web.Features.Reports;

/// <summary>
/// The sizes and distances of a report file's styles (design.md §11.5), which both renderers read, so that the Word and
/// the PDF file cannot drift apart (A162). The Word renderer turns them into half-points and twips and the PDF
/// renderer uses them as points and millimetres.
/// </summary>
internal static class ReportStyle
{
    /// <summary>The title's size, in points.</summary>
    public const int TitlePoints = 20;

    /// <summary>The size of a heading 1, in points.</summary>
    public const int Heading1Points = 14;

    /// <summary>The size of a heading 2, in points.</summary>
    public const int Heading2Points = 12;

    /// <summary>The size of a heading 3, in points. It is bold and not italic.</summary>
    public const int Heading3Points = 11;

    /// <summary>The size of the body text, in points.</summary>
    public const int BodyPoints = 11;

    /// <summary>The size of the running header and the footer, in points.</summary>
    public const int FurniturePoints = 8;

    /// <summary>How far the running header and the footer are from the page's edge, in millimetres.</summary>
    public const int FurnitureDistanceMillimetres = 10;

    /// <summary>The page margin on every side, in millimetres.</summary>
    public const int MarginMillimetres = 20;

    /// <summary>A size in points, as Word's half-points.</summary>
    public static int HalfPoints(int points) => points * 2;

    /// <summary>A distance in millimetres, as Word's twentieths of a point.</summary>
    public static int Twips(int millimetres) => (int)Math.Round(millimetres * 1440 / 25.4);
}
