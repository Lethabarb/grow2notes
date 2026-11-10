using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using DocumentFormat.OpenXml.Wordprocessing;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Features.Reports;

namespace Grow2Notes.Tests.Features.Reports;

public sealed class WordReportTests
{
    private const string Title = "SUPPORT NOTES: Thursday 01.10.2026";
    private const string Organisation = "Example Care";
    private const string Footer = "Confidential: health information. Copy exported from Grow2Notes by Jo Smith, "
        + "Fri 2 Oct 2026, 9:14 am. The record in Grow2Notes is authoritative.";

    private static byte[] Render(params ReportBlock[] blocks) =>
        WordReport.Render(new ReportDocument(Title, Organisation, Footer, blocks));

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
    public void The_styles_define_the_title_the_subtitle_and_three_outline_headings_that_keep_with_the_next_paragraph()
    {
        var docx = Render(
            new ReportBlock(ReportBlockKind.Title, "Organisation"),
            new ReportBlock(ReportBlockKind.Subtitle, "Daily progress notes"),
            new ReportBlock(ReportBlockKind.Heading1, "Zoë Adams"),
            new ReportBlock(ReportBlockKind.Heading2, "Goals"),
            new ReportBlock(ReportBlockKind.Heading3, "Every note"));

        var styles = Read(docx, document => new[] { "Title", "Subtitle", "Heading1", "Heading2", "Heading3" }
            .ToDictionary(id => id, id => StyleOf(document, id).CloneNode(true)));

        Assert.Equal(0, styles["Heading1"].GetFirstChild<StyleParagraphProperties>()!.OutlineLevel!.Val!.Value);
        Assert.Equal(1, styles["Heading2"].GetFirstChild<StyleParagraphProperties>()!.OutlineLevel!.Val!.Value);
        Assert.Equal(2, styles["Heading3"].GetFirstChild<StyleParagraphProperties>()!.OutlineLevel!.Val!.Value);
        Assert.All(
            new[] { "Title", "Subtitle", "Heading1", "Heading2", "Heading3" },
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
    public void A_common_item_group_is_a_Heading_3()
    {
        var docx = Render(
            new ReportBlock(ReportBlockKind.Heading2, "Common items"),
            new ReportBlock(ReportBlockKind.Heading3, "Personal care"),
            new ReportBlock(ReportBlockKind.Tick, "Showered with prompting", IsTicked: true));

        var groups = Read(docx, document => BodyOf(document).Elements<Paragraph>()
            .Where(p => p.ParagraphProperties?.ParagraphStyleId?.Val?.Value == "Heading3")
            .Select(p => p.InnerText)
            .ToList());

        Assert.Equal(["Personal care"], groups);
    }

    [Fact]
    public void Pages_after_the_first_have_the_running_header_and_every_page_has_the_footer()
    {
        var docx = Render(new ReportBlock(ReportBlockKind.Line, "Text"));

        var furniture = ReportText.WordHeadersAndFooters(docx);

        Assert.True(furniture.TitlePage);
        Assert.Null(furniture.FirstHeader);
        Assert.Equal(Title, furniture.DefaultHeader);
        Assert.Equal(Footer + " Page {PAGE} of {NUMPAGES}", furniture.FirstFooter);
        Assert.Equal(furniture.FirstFooter, furniture.DefaultFooter);
    }

    [Fact]
    public void The_properties_hold_the_title_and_the_organisation_only()
    {
        var docx = Render(new ReportBlock(ReportBlockKind.Line, "Text"));

        var (title, others, extended, hasCustom) = Read(docx, document =>
        {
            var core = document.PackageProperties;
            string?[] others =
                [core.Creator, core.LastModifiedBy, core.Subject, core.Keywords, core.Description, core.Category];
            var extended = document.ExtendedFilePropertiesPart!.Properties!.ChildElements
                .Select(element => (element.GetType().Name, element.InnerText))
                .ToList();
            return (core.Title, others, extended, document.CustomFilePropertiesPart is not null);
        });

        Assert.Equal(Title, title);
        Assert.All(others, value => Assert.True(string.IsNullOrEmpty(value)));
        Assert.Equal([("Company", Organisation)], extended);
        Assert.False(hasCustom);
    }

    [Fact]
    public void The_file_is_valid_Open_XML()
    {
        var docx = Render(
            new ReportBlock(ReportBlockKind.Title, "Organisation"),
            new ReportBlock(ReportBlockKind.Subtitle, "Daily progress notes"),
            new ReportBlock(ReportBlockKind.Heading1, "Zoë Adams"),
            new ReportBlock(ReportBlockKind.Heading2, "Common items"),
            new ReportBlock(ReportBlockKind.Heading3, "Every note"),
            new ReportBlock(ReportBlockKind.Tick, "Medication prompted", IsTicked: true),
            new ReportBlock(ReportBlockKind.Line, "One\nTwo"),
            new ReportBlock(ReportBlockKind.IndexEntry, "ZOE ADAMS") { Anchor = "p1" },
            Name("ZOE ADAMS", "p1"),
            new ReportBlock(ReportBlockKind.TickRow, string.Empty) { Ticks = AttendanceTicks });

        var errors = Read(docx, document => new OpenXmlValidator().Validate(document)
            .Select(error => $"{error.Path?.XPath}: {error.Description}")
            .ToList());

        Assert.Empty(errors);
    }

    private static readonly ReportTick[] AttendanceTicks =
        [new("Not In", false), new("Late Notice", true), new("Sick", false)];

    private static ReportBlock Name(string name, string anchor) =>
        new(ReportBlockKind.Heading1, "NAME: " + name) { PageBreakBefore = true, Anchor = anchor };

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
        Assert.Equal((567u, 567u), (margin.Header!.Value, margin.Footer!.Value));
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

    // T39: the Word file reads the sizes from ReportStyle, as the PDF does.
    [Fact]
    public void The_styles_are_ReportStyle_s_and_the_third_heading_is_bold_and_not_italic()
    {
        var docx = Render(new ReportBlock(ReportBlockKind.Line, "Text"));

        var (bodySize, sizes, heading3) = Read(docx, document => (
            document.MainDocumentPart!.StyleDefinitionsPart!.Styles!.DocDefaults!.RunPropertiesDefault!
                .RunPropertiesBaseStyle!.GetFirstChild<FontSize>()!.Val!.Value,
            new[] { "Title", "Heading1", "Heading2", "Heading3", "Header", "Footer" }.ToDictionary(
                id => id, id => StyleOf(document, id).StyleRunProperties!.FontSize!.Val!.Value!),
            StyleOf(document, "Heading3").StyleRunProperties!.CloneNode(true)));

        Assert.Equal((ReportStyle.BodyPoints * 2).ToString(), bodySize);
        Assert.Equal(
            new Dictionary<string, string>
            {
                ["Title"] = (ReportStyle.TitlePoints * 2).ToString(),
                ["Heading1"] = (ReportStyle.Heading1Points * 2).ToString(),
                ["Heading2"] = (ReportStyle.Heading2Points * 2).ToString(),
                ["Heading3"] = (ReportStyle.Heading3Points * 2).ToString(),
                ["Header"] = (ReportStyle.FurniturePoints * 2).ToString(),
                ["Footer"] = (ReportStyle.FurniturePoints * 2).ToString(),
            },
            sizes);
        Assert.NotNull(heading3.GetFirstChild<Bold>());
        Assert.Null(heading3.GetFirstChild<Italic>());
    }

    [Fact]
    public void The_header_and_the_footer_are_10_mm_from_the_edge()
    {
        var docx = Render(new ReportBlock(ReportBlockKind.Line, "Text"));

        var (header, footer) = Read(docx, document =>
        {
            var margin = BodyOf(document).Elements<SectionProperties>().Single().GetFirstChild<PageMargin>()!;
            return (margin.Header!.Value, margin.Footer!.Value);
        });

        Assert.Equal(567u, header);
        Assert.Equal(567u, footer);
        Assert.Equal(567, ReportStyle.Twips(ReportStyle.FurnitureDistanceMillimetres));
    }

    [Fact]
    public void A_heading_that_starts_a_page_has_the_page_break_and_a_bookmark_around_its_text_named_by_position()
    {
        var docx = Render(Name("ZOE ADAMS", "p1"), Name("BOB ADAMS", "p2"));

        var headings = Read(docx, document => BodyOf(document).Elements<Paragraph>()
            .Select(p => (
                Break: p.ParagraphProperties!.PageBreakBefore is not null,
                Style: p.ParagraphProperties.ParagraphStyleId!.Val!.Value,
                Start: p.Elements<BookmarkStart>().Single().Name!.Value,
                StartId: p.Elements<BookmarkStart>().Single().Id!.Value,
                EndId: p.Elements<BookmarkEnd>().Single().Id!.Value,
                Text: p.InnerText))
            .ToList());

        Assert.Equal(
            [(true, "Heading1", "p1", "NAME: ZOE ADAMS"), (true, "Heading1", "p2", "NAME: BOB ADAMS")],
            headings.Select(h => (h.Break, h.Style, h.Start, h.Text)));
        Assert.All(headings, h => Assert.Equal(h.StartId, h.EndId));
        Assert.NotEqual(headings[0].StartId, headings[1].StartId);
    }

    [Fact]
    public void A_heading_that_does_not_start_a_page_has_no_page_break_and_no_bookmark()
    {
        var docx = Render(new ReportBlock(ReportBlockKind.Heading1, "Participants"));

        var (hasBreak, hasBookmark) = Read(docx, document =>
        {
            var paragraph = BodyOf(document).Elements<Paragraph>().Single();
            return (
                paragraph.ParagraphProperties!.PageBreakBefore is not null,
                paragraph.Elements<BookmarkStart>().Any());
        });

        Assert.False(hasBreak);
        Assert.False(hasBookmark);
    }

    // A156: a right tab with a dot leader at the right margin, and a complex PAGEREF field whose stored result is a
    // dash, so a viewer that does not compute fields shows a dash and not a wrong number.
    [Fact]
    public void An_index_entry_is_the_name_a_dot_leader_and_a_PAGEREF_field_with_a_dash_as_its_stored_result()
    {
        var docx = Render(new ReportBlock(ReportBlockKind.IndexEntry, "ZOE ADAMS") { Anchor = "p7" });

        var (tab, fieldChars, code, text) = Read(docx, document =>
        {
            var paragraph = BodyOf(document).Elements<Paragraph>().Single();
            return (
                paragraph.ParagraphProperties!.GetFirstChild<Tabs>()!.GetFirstChild<TabStop>()!.CloneNode(true),
                paragraph.Descendants<FieldChar>().Select(c => c.FieldCharType!.Value).ToList(),
                paragraph.Descendants<FieldCode>().Single().Text,
                paragraph.InnerText);
        });

        var stop = (TabStop)tab;
        Assert.Equal(TabStopValues.Right, stop.Val!.Value);
        Assert.Equal(TabStopLeaderCharValues.Dot, stop.Leader!.Value);
        Assert.Equal(11906 - (2 * 1134), stop.Position!.Value);
        Assert.Equal([FieldCharValues.Begin, FieldCharValues.Separate, FieldCharValues.End], fieldChars);
        Assert.Equal("PAGEREF p7 \\h", code.Trim());
        Assert.Equal("ZOE ADAMS\u2013", text);
        Assert.Equal(["ZOE ADAMS\t{PAGEREF p7 \\h}\u2013"], ReportText.WordParagraphs(docx));
    }

    [Fact]
    public void The_settings_ask_Word_to_update_the_fields_when_the_file_opens()
    {
        var docx = Render(new ReportBlock(ReportBlockKind.Line, "Text"));

        var update = Read(docx, document => document.MainDocumentPart!.DocumentSettingsPart!.Settings!
            .Elements<UpdateFieldsOnOpen>().Single().Val!.Value);

        Assert.True(update);
    }

    [Fact]
    public void A_row_of_ticks_is_one_paragraph_of_boxes_in_a_symbol_font_each_with_its_label()
    {
        var docx = Render(new ReportBlock(ReportBlockKind.TickRow, string.Empty) { Ticks = AttendanceTicks });

        var fonts = Read(docx, document => BodyOf(document).Elements<Paragraph>().Single()
            .Elements<Run>()
            .Where(run => run.RunProperties?.RunFonts is not null)
            .Select(run => (run.RunProperties!.RunFonts!.Ascii!.Value, run.InnerText))
            .ToList());

        Assert.Equal(
            [("Segoe UI Symbol", "\u2610"), ("Segoe UI Symbol", "\u2611"), ("Segoe UI Symbol", "\u2610")],
            fonts);
        Assert.Equal(["\u2610 Not In  \u2611 Late Notice  \u2610 Sick"], ReportText.WordParagraphs(docx));
    }
}
