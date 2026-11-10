using System.Text;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Features.Reports;
using UglyToad.PdfPig;

namespace Grow2Notes.Tests.Features.Reports;

public sealed class PdfReportTests
{
    private const string Phone = "\U0001F4DD";
    private const string Smile = "\U0001F600";
    private const string Replacement = "\uFFFD";

    private static readonly string[] Lines =
    [
        "Boxes ☐ ☑ and \uFFFD and quotes ‘ ’ “ ”",
        "Dashes – — and … and a middle dot · and a no-break\u00A0space",
        "Names é è ë ï ö ü ā ē ī ō ū ñ ç",
        $"Emoji {Phone} and {Smile} end",
    ];

    private static readonly DateTime AsAtUtc = new(2026, 10, 1, 23, 14, 0, DateTimeKind.Utc);

    // The exporter's name holds an emoji, so that the furniture is also checked to be drawn by DejaVu Sans.
    private static ReportDocument Document(string exportedBy, params ReportBlock[] blocks)
    {
        var model = new DailyReportModel("Example Care", new DateOnly(2026, 10, 1), AsAtUtc, exportedBy, []);
        return ReportLayout.DailyReport(model, null) with { Blocks = blocks };
    }

    private static ReportDocument Document(params ReportBlock[] blocks) => Document($"Jo {Phone} Smith", blocks);

    private static byte[] Render() =>
        PdfReport.Render(Document([.. Lines.Select(line => new ReportBlock(ReportBlockKind.Line, line))]));

    [Fact]
    public void The_font_is_the_approved_one_and_draws_the_boxes_but_not_emoji()
    {
        Assert.Equal(757_076, DejaVuSans.Bytes.Length);
        Assert.True(DejaVuSans.Draws(new Rune(0x2610)));
        Assert.True(DejaVuSans.Draws(new Rune(0x2611)));
        Assert.True(DejaVuSans.Draws(new Rune(0xFFFD)));
        Assert.False(DejaVuSans.Draws(new Rune(0x1F4DD)));
        Assert.False(DejaVuSans.Draws(new Rune(0x1F600)));
    }

    [Fact]
    public void Every_character_is_drawn_by_DejaVu_Sans_and_no_other_font_is_used()
    {
        using var document = PdfDocument.Open(Render());

        var letters = document.GetPages().SelectMany(page => page.Letters).ToList();

        Assert.NotEmpty(letters);
        Assert.All(letters, letter =>
        {
            Assert.All(letter.Value.EnumerateRunes(), rune => Assert.True(DejaVuSans.Draws(rune)));
            Assert.Contains("DejaVu Sans", letter.FontName);
        });
    }

    [Fact]
    public void The_text_is_the_input_with_each_emoji_replaced_by_the_replacement_character()
    {
        var expected = string.Join(' ', Lines).Replace(Phone, Replacement).Replace(Smile, Replacement);

        var actual = string.Join(' ', ReportText.PdfWords(Render()));

        Assert.Equal(ReportText.Normalised(expected), ReportText.Normalised(actual));
    }

    [Fact]
    public void The_page_is_A4_portrait()
    {
        using var document = PdfDocument.Open(Render());

        var page = document.GetPage(1);

        Assert.InRange(page.Width, 594, 596);
        Assert.InRange(page.Height, 841, 843);
    }

    // U+0009 has no glyph, so a tab that reached the font would print as the replacement character.
    [Fact]
    public void A_tab_is_a_tab_stop_and_not_a_replacement_character()
    {
        var pdf = PdfReport.Render(Document(new ReportBlock(ReportBlockKind.Line, "Go\tOn")));

        Assert.Equal(["Go", "On"], ReportText.PdfWords(pdf));
    }

    [Fact]
    public void Line_breaks_are_kept_and_a_blank_line_leaves_a_gap()
    {
        var pdf = PdfReport.Render(
            Document(new ReportBlock(ReportBlockKind.Line, "First line\nSecond line\n\nFourth line")));
        using var document = PdfDocument.Open(pdf);

        var words = document.GetPage(1).GetWords().ToList();
        double Baseline(string word) => words.First(w => w.Text == word).Letters[0].StartBaseLine.Y;
        var (first, second, fourth) = (Baseline("First"), Baseline("Second"), Baseline("Fourth"));

        Assert.True(first > second && second > fourth);
        Assert.True(second - fourth > 1.5 * (first - second));
    }

    [Fact]
    public void A_report_of_several_pages_has_the_running_header_from_page_2_page_n_of_N_on_every_page_and_a_footer_clear_of_the_body()
    {
        var exportedBy = string.Join(' ', Enumerable.Repeat("Chris", Limits.DisplayName))[..Limits.DisplayName];
        var report = Document(
            exportedBy,
            [.. Enumerable.Range(1, 150).Select(i => new ReportBlock(ReportBlockKind.Line, $"Line {i}"))]);

        var pdf = PdfReport.Render(report);

        var furniture = ReportText.PdfHeadersAndFooters(pdf);
        var pages = furniture.Count;
        Assert.True(pages >= 3);
        Assert.Equal(string.Empty, furniture[0].Header);
        Assert.All(furniture.Skip(1), page => Assert.Equal(ReportText.Normalised(report.Title), page.Header));
        for (var page = 1; page <= pages; page++)
        {
            Assert.Equal(
                ReportText.Normalised(report.Footer + ReportLayout.FooterPage + page + ReportLayout.FooterOf + pages),
                furniture[page - 1].Footer);
        }

        using var document = PdfDocument.Open(pdf);
        Assert.All(document.GetPages(), page =>
        {
            var words = page.GetWords().ToList();
            var lowestBody = words.Where(word => !ReportText.IsFurniture(word)).Min(word => word.BoundingBox.Bottom);
            var highestFooter = words.Where(ReportText.IsFurniture)
                .Where(word => word.BoundingBox.Bottom < page.Height / 2)
                .Max(word => word.BoundingBox.Top);
            Assert.True(lowestBody > highestFooter, $"Page {page.Number}: the body reaches the footer.");
        });
    }

    [Fact]
    public void The_document_information_holds_the_title_and_the_organisation_only()
    {
        var report = Document(
            "Jo Smith",
            new ReportBlock(ReportBlockKind.Heading1, "Zoë Adams"),
            new ReportBlock(ReportBlockKind.Line, "Note written by: Priya Nair"));

        using var document = PdfDocument.Open(PdfReport.Render(report));

        var information = document.Information;
        Assert.Equal(ReportText.Normalised(report.Title), information.Title);
        Assert.Equal(report.OrganisationName, information.Subject);
        Assert.True(string.IsNullOrEmpty(information.Author));
        Assert.True(string.IsNullOrEmpty(information.Keywords));
        var everything = string.Join(
            ' ',
            information.Title,
            information.Subject,
            information.Author,
            information.Keywords,
            information.Creator,
            information.Producer);
        Assert.DoesNotContain("Adams", everything, StringComparison.Ordinal);
        Assert.DoesNotContain("Nair", everything, StringComparison.Ordinal);
        Assert.DoesNotContain("Smith", everything, StringComparison.Ordinal);
    }

    // T39: the PDF reads the sizes from ReportStyle, as the Word file does. A word of each style is found by its text.
    [Fact]
    public void The_title_and_the_headings_are_set_in_ReportStyle_s_sizes_and_the_third_heading_is_not_italic()
    {
        var pdf = PdfReport.Render(Document(
            new ReportBlock(ReportBlockKind.Title, "TitleWord"),
            new ReportBlock(ReportBlockKind.Heading1, "FirstWord"),
            new ReportBlock(ReportBlockKind.Heading2, "SecondWord"),
            new ReportBlock(ReportBlockKind.Heading3, "ThirdWord"),
            new ReportBlock(ReportBlockKind.Line, "BodyWord")));
        using var document = PdfDocument.Open(pdf);
        var words = document.GetPage(1).GetWords().ToList();

        double SizeOf(string text) => Math.Abs(words.First(word => word.Text == text).Letters[0].PointSize);

        Assert.Equal(ReportStyle.TitlePoints, SizeOf("TitleWord"), 0.1);
        Assert.Equal(ReportStyle.Heading1Points, SizeOf("FirstWord"), 0.1);
        Assert.Equal(ReportStyle.Heading2Points, SizeOf("SecondWord"), 0.1);
        Assert.Equal(ReportStyle.Heading3Points, SizeOf("ThirdWord"), 0.1);
        Assert.Equal(ReportStyle.BodyPoints, SizeOf("BodyWord"), 0.1);
        var third = words.First(word => word.Text == "ThirdWord").Letters[0].FontName;
        Assert.DoesNotContain("Italic", third, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Oblique", third, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void The_footer_is_10_mm_from_the_bottom_edge()
    {
        var pdf = PdfReport.Render(Document(new ReportBlock(ReportBlockKind.Line, "Body")));
        using var document = PdfDocument.Open(pdf);
        var page = document.GetPage(1);

        var lowest = page.GetWords()
            .Where(ReportText.IsFurniture)
            .Where(word => word.BoundingBox.Bottom < page.Height / 2)
            .Min(word => word.BoundingBox.Bottom);

        const double TenMillimetres = 10 * 72 / 25.4;
        Assert.InRange(lowest, TenMillimetres - (1.5 * 72 / 25.4), TenMillimetres + (1.5 * 72 / 25.4));
    }

    // The curly quotes, the en dash of the week and the boxes are all in DejaVu Sans, so none becomes a replacement.
    [Fact]
    public void The_quotes_the_en_dash_and_the_boxes_are_drawn_and_the_title_property_keeps_the_en_dash()
    {
        var model = new DailyReportModel("Example Care", new DateOnly(2026, 10, 1), AsAtUtc, "Jo Smith", []);
        var report = ReportLayout.DailyReport(model, "A") with
        {
            Blocks =
            [
                new ReportBlock(ReportBlockKind.Title, "SUPPORT NOTES – WEEK A"),
                new ReportBlock(ReportBlockKind.TickRow, string.Empty)
                {
                    Ticks = [new("Not In", false), new("Late Notice", true), new("Sick", false)],
                },
                new ReportBlock(ReportBlockKind.Tick, "Said ‘hi’ and “bye”", IsTicked: true),
            ],
        };

        var pdf = PdfReport.Render(report);

        using var document = PdfDocument.Open(pdf);
        var letters = document.GetPages().SelectMany(page => page.Letters).Select(letter => letter.Value);
        Assert.DoesNotContain(Replacement, letters);
        Assert.Equal(
            ReportText.Normalised(
                "SUPPORT NOTES – WEEK A ☐ Not In ☑ Late Notice ☐ Sick ☑ Said ‘hi’ "
                + "and “bye”"),
            ReportText.Normalised(string.Join(' ', ReportText.PdfWords(pdf))));
        Assert.Equal("SUPPORT NOTES – WEEK A: Thursday 01.10.2026", document.Information.Title);
    }

    // The index's numbers are the pages of the headings that the anchors name, which MigraDoc works out.
    [Fact]
    public void An_index_entry_shows_the_page_of_the_heading_with_its_anchor()
    {
        var pdf = PdfReport.Render(Document(
            new ReportBlock(ReportBlockKind.Heading1, "Participants"),
            new ReportBlock(ReportBlockKind.IndexEntry, "ANN") { Anchor = "p1" },
            new ReportBlock(ReportBlockKind.IndexEntry, "BOB") { Anchor = "p2" },
            new ReportBlock(ReportBlockKind.Heading1, "NAME: ANN") { PageBreakBefore = true, Anchor = "p1" },
            new ReportBlock(ReportBlockKind.Line, "First note"),
            new ReportBlock(ReportBlockKind.Heading1, "NAME: BOB") { PageBreakBefore = true, Anchor = "p2" },
            new ReportBlock(ReportBlockKind.Line, "Second note")));

        var text = ReportText.Normalised(ReportText.WithoutLeaders(string.Join(' ', ReportText.PdfWords(pdf))));

        Assert.Equal("Participants ANN 2 BOB 3 NAME: ANN First note NAME: BOB Second note", text);
        using var document = PdfDocument.Open(pdf);
        Assert.Equal(3, document.NumberOfPages);
    }
}
