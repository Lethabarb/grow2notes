using System.Text;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Features.Reports;
using UglyToad.PdfPig;

namespace Grow2Notes.Tests.Features.Reports;

public sealed class PdfReportTests
{
    private const string Phone = "\U0001F4DD";
    private const string Smile = "\U0001F600";

    private static readonly string[] Lines =
    [
        "Boxes ☐ ☑ and � and quotes ‘ ’ “ ”",
        "Dashes – — and … and a middle dot · and a no-break space",
        "Names é è ë ï ö ü ā ē ī ō ū ñ ç",
        $"Emoji {Phone} and {Smile} end",
    ];

    private static byte[] Render() =>
        PdfReport.Render([.. Lines.Select(line => new ReportBlock(ReportBlockKind.Line, line))]);

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
            Assert.Contains("DejaVuSans", letter.FontName);
        });
    }

    [Fact]
    public void The_text_is_the_input_with_each_emoji_replaced_by_the_replacement_character()
    {
        var expected = string.Join(' ', Lines).Replace(Phone, "�").Replace(Smile, "�");

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

    [Fact]
    public void Line_breaks_are_kept_and_a_blank_line_leaves_a_gap()
    {
        var pdf = PdfReport.Render([new ReportBlock(ReportBlockKind.Line, "First line\nSecond line\n\nFourth line")]);
        using var document = PdfDocument.Open(pdf);

        var words = document.GetPage(1).GetWords().ToList();
        double Baseline(string word) => words.First(w => w.Text == word).Letters[0].StartBaseLine.Y;
        var (first, second, fourth) = (Baseline("First"), Baseline("Second"), Baseline("Fourth"));

        Assert.True(first > second && second > fourth);
        Assert.True(second - fourth > 1.5 * (first - second));
    }
}
