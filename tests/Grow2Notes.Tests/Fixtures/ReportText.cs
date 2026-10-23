using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using UglyToad.PdfPig;

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

    /// <summary>The words of every page of a PDF file, in order.</summary>
    public static IReadOnlyList<string> PdfWords(byte[] pdf)
    {
        using var document = PdfDocument.Open(pdf);
        return [.. document.GetPages().SelectMany(page => page.GetWords()).Select(word => word.Text)];
    }

    /// <summary>
    /// <paramref name="text"/> with each no-break space as a space and each run of white space as one space, so that a
    /// line wrapped at a different place compares equal.
    /// </summary>
    public static string Normalised(string text) => WhiteSpace().Replace(text.Replace('\u00A0', ' '), " ").Trim();

    private static string ParagraphText(Paragraph paragraph) => string.Concat(paragraph.Descendants<OpenXmlElement>()
        .Select(element => element switch
        {
            Text text => text.Text,
            Break => "\n",
            _ => string.Empty,
        }));

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhiteSpace();
}
