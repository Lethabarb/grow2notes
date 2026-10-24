using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace Grow2Notes.Tests.Fixtures;

/// <summary>
/// Reads the text back out of a Word or PDF report, so that a test can compare what a file says with what it should
/// say without depending on how either format lays it out.
/// </summary>
internal static partial class ReportText
{
    /// <summary>
    /// The text of each body paragraph of a Word file, in order: <c>w:t</c> as text and <c>w:br</c> as a line feed. A
    /// goal reads as its box, a space and its text.
    /// </summary>
    public static IReadOnlyList<string> WordParagraphs(byte[] docx)
    {
        using var stream = new MemoryStream(docx);
        using var document = WordprocessingDocument.Open(stream, false);
        var body = document.MainDocumentPart!.Document!.Body!;
        return [.. body.Elements<Paragraph>().Select(ParagraphText)];
    }

    /// <summary>
    /// The words of the body of every page of a PDF file, in order, without the running header and the footer.
    /// </summary>
    public static IReadOnlyList<string> PdfWords(byte[] pdf)
    {
        using var document = PdfDocument.Open(pdf);
        return [.. document.GetPages()
            .SelectMany(page => page.GetWords())
            .Where(word => !IsFurniture(word))
            .Select(word => word.Text)];
    }

    /// <summary>
    /// Whether a word belongs to the running header or the footer. Both are set at 8 pt and the body is 11 pt or
    /// larger, so the split needs no geometry.
    /// </summary>
    public static bool IsFurniture(Word word) => Math.Abs(word.Letters[0].PointSize) < 10;

    /// <summary>
    /// The running header and the footer of each page of a PDF file, as <see cref="Normalised"/> text: the furniture
    /// words in the upper half of the page are the header and those in the lower half are the footer.
    /// </summary>
    public static IReadOnlyList<(string Header, string Footer)> PdfHeadersAndFooters(byte[] pdf)
    {
        using var document = PdfDocument.Open(pdf);
        return [.. document.GetPages().Select(page =>
        {
            var furniture = page.GetWords().Where(IsFurniture).ToList();
            string TextOf(IEnumerable<Word> words) => Normalised(string.Join(' ', words.Select(word => word.Text)));
            return (
                TextOf(furniture.Where(word => word.BoundingBox.Bottom > page.Height / 2)),
                TextOf(furniture.Where(word => word.BoundingBox.Bottom <= page.Height / 2)));
        })];
    }

    /// <summary>
    /// The running header and the footer of a Word file. A page number field reads as <c>{PAGE}</c> and the page count
    /// as <c>{NUMPAGES}</c>. A header or footer the file does not have is <c>null</c>.
    /// </summary>
    public static WordFurniture WordHeadersAndFooters(byte[] docx)
    {
        using var stream = new MemoryStream(docx);
        using var document = WordprocessingDocument.Open(stream, false);
        var main = document.MainDocumentPart!;
        var section = main.Document!.Body!.Elements<SectionProperties>().Single();

        string? HeaderOf(HeaderFooterValues type)
        {
            var reference = section.Elements<HeaderReference>().SingleOrDefault(r => r.Type?.Value == type);
            return reference is null
                ? null
                : PartText(((HeaderPart)main.GetPartById(reference.Id!.Value!)).Header!.Elements<Paragraph>());
        }

        string? FooterOf(HeaderFooterValues type)
        {
            var reference = section.Elements<FooterReference>().SingleOrDefault(r => r.Type?.Value == type);
            return reference is null
                ? null
                : PartText(((FooterPart)main.GetPartById(reference.Id!.Value!)).Footer!.Elements<Paragraph>());
        }

        return new WordFurniture(
            section.Elements<TitlePage>().Any(),
            HeaderOf(HeaderFooterValues.First),
            HeaderOf(HeaderFooterValues.Default),
            FooterOf(HeaderFooterValues.First),
            FooterOf(HeaderFooterValues.Default));
    }

    /// <summary>
    /// <paramref name="text"/> with each no-break space as a space and each run of white space as one space, so that a
    /// line wrapped at a different place compares equal.
    /// </summary>
    public static string Normalised(string text) => WhiteSpace().Replace(text.Replace('\u00A0', ' '), " ").Trim();

    private static string PartText(IEnumerable<Paragraph> paragraphs) =>
        string.Join('\n', paragraphs.Select(ParagraphText));

    private static string ParagraphText(Paragraph paragraph) => string.Concat(paragraph.Descendants<OpenXmlElement>()
        .Select(element => element switch
        {
            Text text => text.Text,
            SimpleField field => "{" + field.Instruction!.Value!.Trim() + "}",
            Break => "\n",
            _ => string.Empty,
        }));

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhiteSpace();
}

/// <summary>What <see cref="ReportText.WordHeadersAndFooters"/> reads from a Word file.</summary>
internal sealed record WordFurniture(
    bool TitlePage,
    string? FirstHeader,
    string? DefaultHeader,
    string? FirstFooter,
    string? DefaultFooter);
