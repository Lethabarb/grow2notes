using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Features.Reports;

namespace Grow2Notes.Tests.Features.Reports;

public sealed class WordReportTests
{
    private static byte[] Render(params ReportBlock[] blocks) => WordReport.Render(blocks);

    private static T Read<T>(byte[] docx, Func<WordprocessingDocument, T> read)
    {
        using var stream = new MemoryStream(docx);
        using var document = WordprocessingDocument.Open(stream, false);
        return read(document);
    }

    private static Body BodyOf(WordprocessingDocument document) => document.MainDocumentPart!.Document!.Body!;

    private static Style StyleOf(WordprocessingDocument document, string styleId) => document.MainDocumentPart!
        .StyleDefinitionsPart!.Styles!.Elements<Style>().Single(style => style.StyleId?.Value == styleId);

    [Fact]
    public void The_styles_define_the_title_the_subtitle_and_two_outline_headings_that_keep_with_the_next_paragraph()
    {
        var docx = Render(
            new ReportBlock(ReportBlockKind.Title, "Organisation"),
            new ReportBlock(ReportBlockKind.Subtitle, "Daily progress notes"),
            new ReportBlock(ReportBlockKind.Heading1, "Zoë Adams"),
            new ReportBlock(ReportBlockKind.Heading2, "Goals"));

        var styles = Read(docx, document => new[] { "Title", "Subtitle", "Heading1", "Heading2" }
            .ToDictionary(id => id, id => StyleOf(document, id).CloneNode(true)));

        Assert.Equal(0, styles["Heading1"].GetFirstChild<StyleParagraphProperties>()!.OutlineLevel!.Val!.Value);
        Assert.Equal(1, styles["Heading2"].GetFirstChild<StyleParagraphProperties>()!.OutlineLevel!.Val!.Value);
        Assert.All(
            new[] { "Title", "Subtitle", "Heading1", "Heading2" },
            id => Assert.Equal(
                id.StartsWith("Heading", StringComparison.Ordinal),
                styles[id].GetFirstChild<StyleParagraphProperties>()?.KeepNext is not null));
    }

    [Fact]
    public void A_participant_heading_holds_the_participant_name()
    {
        var docx = Render(
            new ReportBlock(ReportBlockKind.Heading1, "Zoë Adams"),
            new ReportBlock(ReportBlockKind.Heading2, "Goals"));

        var headings = Read(docx, document => BodyOf(document).Elements<Paragraph>()
            .Where(p => p.ParagraphProperties?.ParagraphStyleId?.Val?.Value == "Heading1")
            .Select(p => p.InnerText)
            .ToList());

        Assert.Equal(["Zoë Adams"], headings);
    }

    [Fact]
    public void The_text_is_Arial_at_11_points()
    {
        var docx = Render(new ReportBlock(ReportBlockKind.Line, "Text"));

        var defaults = Read(docx, document => document.MainDocumentPart!.StyleDefinitionsPart!.Styles!.DocDefaults!
            .RunPropertiesDefault!.RunPropertiesBaseStyle!.CloneNode(true));

        Assert.Equal("Arial", defaults.GetFirstChild<RunFonts>()!.Ascii!.Value);
        Assert.Equal("22", defaults.GetFirstChild<FontSize>()!.Val!.Value);
    }

    [Fact]
    public void The_page_is_A4_with_20_mm_margins()
    {
        var docx = Render(new ReportBlock(ReportBlockKind.Line, "Text"));

        var section = Read(docx, document => BodyOf(document).Elements<SectionProperties>().Single().CloneNode(true));

        var size = section.GetFirstChild<PageSize>()!;
        var margin = section.GetFirstChild<PageMargin>()!;
        Assert.Equal((11906u, 16838u), (size.Width!.Value, size.Height!.Value));
        Assert.Equal(
            (1134, 1134u, 1134, 1134u),
            (margin.Top!.Value, margin.Left!.Value, margin.Bottom!.Value, margin.Right!.Value));
    }

    [Fact]
    public void A_goal_is_a_list_item_that_starts_with_its_box_in_a_symbol_font()
    {
        var docx = Render(
            new ReportBlock(ReportBlockKind.Tick, "Done", IsTicked: true),
            new ReportBlock(ReportBlockKind.Tick, "Not done"));

        var goals = Read(docx, document => BodyOf(document).Elements<Paragraph>()
            .Select(p => (
                IsListItem: p.ParagraphProperties?.NumberingProperties is not null,
                Font: p.Elements<Run>().First().RunProperties!.RunFonts!.Ascii!.Value,
                Box: p.Elements<Run>().First().InnerText))
            .ToList());

        Assert.Equal(
            [(true, "Segoe UI Symbol", "☑"), (true, "Segoe UI Symbol", "☐")],
            goals);
        Assert.Equal(["☑ Done", "☐ Not done"], ReportText.WordParagraphs(docx));
    }

    [Fact]
    public void A_line_of_three_lines_is_one_paragraph_with_two_line_breaks()
    {
        var docx = Render(new ReportBlock(ReportBlockKind.Line, "One\r\nTwo\nThree"));

        var breaks = Read(docx, document => BodyOf(document).Descendants<Break>().Count());

        Assert.Equal(2, breaks);
        Assert.Equal(["One\nTwo\nThree"], ReportText.WordParagraphs(docx));
    }

    [Fact]
    public void Emoji_are_kept_as_written()
    {
        var docx = Render(new ReportBlock(ReportBlockKind.Line, "Note \U0001F4DD and \U0001F600"));

        Assert.Equal(["Note \U0001F4DD and \U0001F600"], ReportText.WordParagraphs(docx));
    }

    [Fact]
    public void A_character_xml_cannot_hold_becomes_the_replacement_character_and_the_file_still_saves()
    {
        var docx = Render(new ReportBlock(ReportBlockKind.Line, "a\u0001b"));

        Assert.Equal(["a\uFFFDb"], ReportText.WordParagraphs(docx));
    }
}
