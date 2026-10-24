using MigraDoc.DocumentObjectModel;
using MigraDoc.Rendering;
using PdfSharp.Fonts;

namespace Grow2Notes.Web.Features.Reports;

/// <summary>
/// Renders a report document as a PDF: DejaVu Sans 11 pt embedded in the file, on A4 portrait with 20 mm margins.
/// Heading 1 carries a 0.5 pt rule above it, which is the thin line between notes. Only a heading keeps with the next
/// paragraph, and no page break is forced. The running header starts on page 2; the footer is on every page.
/// </summary>
internal static class PdfReport
{
    private const string FontName = "DejaVu Sans";

    static PdfReport()
    {
        // PDFsharp takes one font resolver for the whole process, and this is the only place that sets it.
        GlobalFontSettings.FontResolver = new DejaVuFontResolver();
    }

    public static byte[] Render(ReportDocument report)
    {
        var document = new Document();
        // PDF text strings read byte A0 as the euro sign, so the title's no-break space becomes an ordinary one.
        document.Info.Title = report.Title.Replace('\u00A0', ' ');
        document.Info.Subject = report.OrganisationName;
        DefineStyles(document);

        var section = document.AddSection();
        section.PageSetup.PageFormat = PageFormat.A4;
        section.PageSetup.Orientation = Orientation.Portrait;
        section.PageSetup.TopMargin = Unit.FromMillimeter(20);
        section.PageSetup.RightMargin = Unit.FromMillimeter(20);
        section.PageSetup.BottomMargin = Unit.FromMillimeter(20);
        section.PageSetup.LeftMargin = Unit.FromMillimeter(20);
        section.PageSetup.HeaderDistance = Unit.FromMillimeter(10);
        section.PageSetup.FooterDistance = Unit.FromMillimeter(6);
        section.PageSetup.DifferentFirstPageHeaderFooter = true;

        // The first-page header is left empty, so that page 1 has no running header.
        var header = section.Headers.Primary.AddParagraph();
        header.Style = StyleNames.Header;
        AddText(header, report.Title);
        AddFooter(section.Footers.Primary, report.Footer);
        AddFooter(section.Footers.FirstPage, report.Footer);

        foreach (var block in report.Blocks)
        {
            var paragraph = section.AddParagraph();
            paragraph.Style = block.Kind switch
            {
                ReportBlockKind.Title => "Title",
                ReportBlockKind.Subtitle => "Subtitle",
                ReportBlockKind.Heading1 => StyleNames.Heading1,
                ReportBlockKind.Heading2 => StyleNames.Heading2,
                ReportBlockKind.Heading3 => StyleNames.Heading3,
                ReportBlockKind.Line => StyleNames.Normal,
                ReportBlockKind.Tick => "Tick",
                _ => throw new ArgumentOutOfRangeException(nameof(block)),
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

    // The sentence, then "Page n of N" from the page and page-count fields.
    private static void AddFooter(HeaderFooter footer, string text)
    {
        var paragraph = footer.AddParagraph();
        paragraph.Style = StyleNames.Footer;
        AddText(paragraph, text + ReportLayout.FooterPage);
        paragraph.AddPageField();
        AddText(paragraph, ReportLayout.FooterOf);
        paragraph.AddNumPagesField();
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

        var heading3 = document.Styles[StyleNames.Heading3]!;
        heading3.BaseStyle = StyleNames.Normal;
        heading3.Font.Size = 11;
        heading3.Font.Bold = true;
        heading3.Font.Italic = true;
        heading3.ParagraphFormat.SpaceBefore = Unit.FromPoint(6);
        heading3.ParagraphFormat.SpaceAfter = Unit.FromPoint(3);
        heading3.ParagraphFormat.KeepWithNext = true;

        foreach (var name in new[] { StyleNames.Header, StyleNames.Footer })
        {
            var furniture = document.Styles[name]!;
            furniture.BaseStyle = StyleNames.Normal;
            furniture.Font.Size = 8;
            furniture.ParagraphFormat.SpaceBefore = Unit.FromPoint(0);
            furniture.ParagraphFormat.SpaceAfter = Unit.FromPoint(0);
        }

        document.Styles.AddStyle("Tick", StyleNames.Normal).ParagraphFormat.LeftIndent = Unit.FromMillimeter(5);
    }
}
