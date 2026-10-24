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
        return ReportLayout.DailyReport(model) with { Blocks = blocks };
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
            new ReportBlock(ReportBlockKind.Line, "Written by Priya Nair"));

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
}
