using MigraDoc.DocumentObjectModel;
using MigraDoc.Rendering;
using PdfSharp.Fonts;

namespace Grow2Notes.Web.Features.Reports;

/// <summary>
/// Renders a report document as a PDF: DejaVu Sans 11 pt embedded in the file, on A4 portrait with 20 mm margins. Only
/// a heading keeps with the next paragraph; a heading that asks for it starts a new page and is bookmarked, and the
/// index's page numbers are page references to those bookmarks, which MigraDoc fills in exactly (A156). The running
/// header starts on page 2; the footer is on every page. The sizes and distances are <see cref="ReportStyle"/>'s.
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
        section.PageSetup.TopMargin = Unit.FromMillimeter(ReportStyle.MarginMillimetres);
        section.PageSetup.RightMargin = Unit.FromMillimeter(ReportStyle.MarginMillimetres);
        section.PageSetup.BottomMargin = Unit.FromMillimeter(ReportStyle.MarginMillimetres);
        section.PageSetup.LeftMargin = Unit.FromMillimeter(ReportStyle.MarginMillimetres);
        section.PageSetup.HeaderDistance = Unit.FromMillimeter(ReportStyle.FurnitureDistanceMillimetres);
        section.PageSetup.FooterDistance = Unit.FromMillimeter(ReportStyle.FurnitureDistanceMillimetres);
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
                ReportBlockKind.TickRow => StyleNames.Normal,
                ReportBlockKind.IndexEntry => "IndexEntry",
                _ => throw new ArgumentOutOfRangeException(nameof(block)),
            };

            if (block.PageBreakBefore)
            {
                paragraph.Format.PageBreakBefore = true;
            }

            // The bookmark comes first, so that it is on the page that the heading starts.
            if (block.Anchor is { } anchor && block.Kind == ReportBlockKind.Heading1)
            {
                paragraph.AddBookmark(anchor);
            }

            AddBlockText(paragraph, block);
        }

        var renderer = new PdfDocumentRenderer { Document = document };
        renderer.RenderDocument();
        using var stream = new MemoryStream();
        renderer.Save(stream, false);
        return stream.ToArray();
    }

    // An index entry is the name, a tab with a dot leader and the page of its anchor, which MigraDoc resolves when it
    // lays the document out. A row of boxes is its boxes and labels, two spaces apart.
    private static void AddBlockText(Paragraph paragraph, ReportBlock block)
    {
        switch (block.Kind)
        {
            case ReportBlockKind.IndexEntry:
                AddText(paragraph, block.Text);
                paragraph.AddTab();
                paragraph.AddPageRefField(block.Anchor!);
                break;
            case ReportBlockKind.TickRow:
                AddText(paragraph, string.Join("  ", block.Ticks.Select(tick => Box(tick.IsTicked) + " " + tick.Text)));
                break;
            case ReportBlockKind.Tick:
                AddText(paragraph, Box(block.IsTicked) + " " + block.Text);
                break;
            default:
                AddText(paragraph, block.Text);
                break;
        }
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
        normal.Font.Size = ReportStyle.BodyPoints;
        normal.ParagraphFormat.SpaceAfter = Unit.FromPoint(6);

        var title = document.Styles.AddStyle("Title", StyleNames.Normal);
        title.Font.Size = ReportStyle.TitlePoints;
        title.Font.Bold = true;

        document.Styles.AddStyle("Subtitle", StyleNames.Normal).ParagraphFormat.SpaceAfter = Unit.FromPoint(3);

        var heading1 = document.Styles[StyleNames.Heading1]!;
        heading1.BaseStyle = StyleNames.Normal;
        heading1.Font.Size = ReportStyle.Heading1Points;
        heading1.Font.Bold = true;
        heading1.ParagraphFormat.SpaceBefore = Unit.FromPoint(18);
        heading1.ParagraphFormat.SpaceAfter = Unit.FromPoint(6);
        heading1.ParagraphFormat.KeepWithNext = true;

        var heading2 = document.Styles[StyleNames.Heading2]!;
        heading2.BaseStyle = StyleNames.Normal;
        heading2.Font.Size = ReportStyle.Heading2Points;
        heading2.Font.Bold = true;
        heading2.ParagraphFormat.SpaceBefore = Unit.FromPoint(12);
        heading2.ParagraphFormat.SpaceAfter = Unit.FromPoint(6);
        heading2.ParagraphFormat.KeepWithNext = true;

        var heading3 = document.Styles[StyleNames.Heading3]!;
        heading3.BaseStyle = StyleNames.Normal;
        heading3.Font.Size = ReportStyle.Heading3Points;
        heading3.Font.Bold = true;
        heading3.ParagraphFormat.SpaceBefore = Unit.FromPoint(6);
        heading3.ParagraphFormat.SpaceAfter = Unit.FromPoint(3);
        heading3.ParagraphFormat.KeepWithNext = true;

        foreach (var name in new[] { StyleNames.Header, StyleNames.Footer })
        {
            var furniture = document.Styles[name]!;
            furniture.BaseStyle = StyleNames.Normal;
            furniture.Font.Size = ReportStyle.FurniturePoints;
            furniture.ParagraphFormat.SpaceBefore = Unit.FromPoint(0);
            furniture.ParagraphFormat.SpaceAfter = Unit.FromPoint(0);
        }

        document.Styles.AddStyle("Tick", StyleNames.Normal).ParagraphFormat.LeftIndent = Unit.FromMillimeter(5);

        // The entry runs out to the right margin: the page is 210 mm wide.
        var indexEntry = document.Styles.AddStyle("IndexEntry", StyleNames.Normal);
        indexEntry.ParagraphFormat.TabStops.AddTabStop(
            Unit.FromMillimeter(210 - (2 * ReportStyle.MarginMillimetres)), TabAlignment.Right, TabLeader.Dots);
    }
}
