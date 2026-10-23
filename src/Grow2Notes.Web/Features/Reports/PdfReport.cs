using MigraDoc.DocumentObjectModel;
using MigraDoc.Rendering;
using PdfSharp.Fonts;

namespace Grow2Notes.Web.Features.Reports;

/// <summary>
/// Renders report blocks as a PDF: DejaVu Sans 11 pt embedded in the file, on A4 portrait with 20 mm margins. Heading 1
/// carries a 0.5 pt rule above it, which is the thin line between notes. Only a heading keeps with the next paragraph,
/// and no page break is forced.
/// </summary>
internal static class PdfReport
{
    private const string FontName = "DejaVu Sans";

    static PdfReport()
    {
        // PDFsharp takes one font resolver for the whole process, and this is the only place that sets it.
        GlobalFontSettings.FontResolver = new DejaVuFontResolver();
    }

    public static byte[] Render(IReadOnlyList<ReportBlock> blocks)
    {
        var document = new Document();
        DefineStyles(document);

        var section = document.AddSection();
        section.PageSetup.PageFormat = PageFormat.A4;
        section.PageSetup.Orientation = Orientation.Portrait;
        section.PageSetup.TopMargin = Unit.FromMillimeter(20);
        section.PageSetup.RightMargin = Unit.FromMillimeter(20);
        section.PageSetup.BottomMargin = Unit.FromMillimeter(20);
        section.PageSetup.LeftMargin = Unit.FromMillimeter(20);

        foreach (var block in blocks)
        {
            var paragraph = section.AddParagraph();
            paragraph.Style = block.Kind switch
            {
                ReportBlockKind.Title => "Title",
                ReportBlockKind.Subtitle => "Subtitle",
                ReportBlockKind.Heading1 => StyleNames.Heading1,
                ReportBlockKind.Heading2 => StyleNames.Heading2,
                ReportBlockKind.Line => StyleNames.Normal,
                ReportBlockKind.Tick => "Tick",
                _ => throw new ArgumentOutOfRangeException(nameof(blocks)),
            };

            var isTick = block.Kind == ReportBlockKind.Tick;
            AddText(paragraph, isTick ? Box(block.IsTicked) + " " + block.Text : block.Text);
        }

        var renderer = new PdfDocumentRenderer { Document = document };
        renderer.RenderDocument();
        using var stream = new MemoryStream();
        renderer.Save(stream, false);
        return stream.ToArray();
    }

    private static string Box(bool isTicked) => isTicked ? "☑" : "☐";

    // Lines are joined with line breaks, so a blank line stays; a tab is a tab stop, since U+0009 has no glyph.
    private static void AddText(Paragraph paragraph, string text)
    {
        var lines = text.ReplaceLineEndings("\n").Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (i > 0)
            {
                paragraph.AddLineBreak();
            }

            var parts = lines[i].Split('\t');
            for (var j = 0; j < parts.Length; j++)
            {
                if (j > 0)
                {
                    paragraph.AddTab();
                }

                paragraph.AddText(DejaVuSans.Drawable(parts[j]));
            }
        }
    }

    private static void DefineStyles(Document document)
    {
        var normal = document.Styles[StyleNames.Normal]!;
        normal.Font.Name = FontName;
        normal.Font.Size = 11;
        normal.ParagraphFormat.SpaceAfter = Unit.FromPoint(6);

        var title = document.Styles.AddStyle("Title", StyleNames.Normal);
        title.Font.Size = 18;
        title.Font.Bold = true;

        document.Styles.AddStyle("Subtitle", StyleNames.Normal).ParagraphFormat.SpaceAfter = Unit.FromPoint(3);

        var heading1 = document.Styles[StyleNames.Heading1]!;
        heading1.BaseStyle = StyleNames.Normal;
        heading1.Font.Size = 14;
        heading1.Font.Bold = true;
        heading1.ParagraphFormat.SpaceBefore = Unit.FromPoint(18);
        heading1.ParagraphFormat.SpaceAfter = Unit.FromPoint(6);
        heading1.ParagraphFormat.KeepWithNext = true;
        heading1.ParagraphFormat.Borders.Top.Width = Unit.FromPoint(0.5);
        heading1.ParagraphFormat.Borders.DistanceFromTop = Unit.FromPoint(6);

        var heading2 = document.Styles[StyleNames.Heading2]!;
        heading2.BaseStyle = StyleNames.Normal;
        heading2.Font.Size = 11;
        heading2.Font.Bold = true;
        heading2.ParagraphFormat.SpaceBefore = Unit.FromPoint(12);
        heading2.ParagraphFormat.SpaceAfter = Unit.FromPoint(6);
        heading2.ParagraphFormat.KeepWithNext = true;

        document.Styles.AddStyle("Tick", StyleNames.Normal).ParagraphFormat.LeftIndent = Unit.FromMillimeter(5);
    }
}
