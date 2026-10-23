using System.Text;
using System.Xml;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace Grow2Notes.Web.Features.Reports;

/// <summary>
/// Renders report blocks as a Word file: Arial 11 pt on A4 with 20 mm margins, the Title and Subtitle styles, real
/// headings (so each participant is in Word's Navigation pane) and the goals as a list with no visible marker.
/// </summary>
internal static class WordReport
{
    private const string TickFont = "Segoe UI Symbol";
    private const string TextFont = "Arial";
    private const int GoalListId = 1;

    public static byte[] Render(IReadOnlyList<ReportBlock> blocks)
    {
        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var main = document.AddMainDocumentPart();
            main.AddNewPart<StyleDefinitionsPart>().Styles = Styles();
            main.AddNewPart<NumberingDefinitionsPart>().Numbering = GoalList();

            var body = new Body();
            foreach (var block in blocks)
            {
                body.Append(Paragraph(block));
            }

            body.Append(new SectionProperties(
                new PageSize { Width = 11906, Height = 16838 },
                new PageMargin
                {
                    Top = 1134,
                    Right = 1134,
                    Bottom = 1134,
                    Left = 1134,
                    Header = 567,
                    Footer = 567,
                    Gutter = 0,
                }));
            main.Document = new Document(body);
        }

        return stream.ToArray();
    }

    private static Paragraph Paragraph(ReportBlock block) => block.Kind switch
    {
        ReportBlockKind.Title => Styled("Title", block.Text),
        ReportBlockKind.Subtitle => Styled("Subtitle", block.Text),
        ReportBlockKind.Heading1 => Styled("Heading1", block.Text),
        ReportBlockKind.Heading2 => Styled("Heading2", block.Text),
        ReportBlockKind.Line => new Paragraph(Lines(block.Text)),
        ReportBlockKind.Tick => Goal(block),
        _ => throw new ArgumentOutOfRangeException(nameof(block)),
    };

    private static Paragraph Styled(string styleId, string text) =>
        new(new ParagraphProperties(new ParagraphStyleId { Val = styleId }), Lines(text));

    // The goals are text, not form controls, so the file reads as a record. The boxes come from a symbol font because
    // Arial has no ☐ or ☑.
    private static Paragraph Goal(ReportBlock block) => new(
        new ParagraphProperties(new NumberingProperties(
            new NumberingLevelReference { Val = 0 },
            new NumberingId { Val = GoalListId })),
        new Run(
            new RunProperties(new RunFonts
            {
                Ascii = TickFont,
                HighAnsi = TickFont,
                ComplexScript = TickFont,
                EastAsia = TickFont,
            }),
            new Text(block.IsTicked ? "☑" : "☐")),
        Lines(" " + block.Text));

    // One run, with a line break between the lines of the text.
    private static Run Lines(string text)
    {
        var run = new Run();
        var lines = Valid(text).ReplaceLineEndings("\n").Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (i > 0)
            {
                run.Append(new Break());
            }

            run.Append(new Text(lines[i]) { Space = SpaceProcessingModeValues.Preserve });
        }

        return run;
    }

    // XML 1.0 cannot hold most control characters, so one stray character must not turn a download into an error. The
    // text is otherwise kept as written, emoji included.
    private static string Valid(string text)
    {
        var valid = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            if (i + 1 < text.Length && XmlConvert.IsXmlSurrogatePair(text[i + 1], text[i]))
            {
                valid.Append(text[i]).Append(text[++i]);
            }
            else
            {
                valid.Append(XmlConvert.IsXmlChar(text[i]) ? text[i] : '�');
            }
        }

        return valid.ToString();
    }

    private static Styles Styles() => new(
        new DocDefaults(
            new RunPropertiesDefault(new RunPropertiesBaseStyle(
                new RunFonts { Ascii = TextFont, HighAnsi = TextFont, ComplexScript = TextFont, EastAsia = TextFont },
                new FontSize { Val = "22" },
                new FontSizeComplexScript { Val = "22" },
                new Languages { Val = "en-AU" })),
            new ParagraphPropertiesDefault(new ParagraphPropertiesBaseStyle(
                new SpacingBetweenLines { After = "120" }))),
        new Style(new StyleName { Val = "Normal" }, new PrimaryStyle())
        {
            Type = StyleValues.Paragraph,
            StyleId = "Normal",
            Default = true,
        },
        new Style(
            new StyleName { Val = "Title" },
            new BasedOn { Val = "Normal" },
            new NextParagraphStyle { Val = "Normal" },
            new PrimaryStyle(),
            new StyleRunProperties(new Bold(), new FontSize { Val = "40" }, new FontSizeComplexScript { Val = "40" }))
        {
            Type = StyleValues.Paragraph,
            StyleId = "Title",
        },
        new Style(
            new StyleName { Val = "Subtitle" },
            new BasedOn { Val = "Normal" },
            new NextParagraphStyle { Val = "Normal" },
            new PrimaryStyle(),
            new StyleParagraphProperties(new SpacingBetweenLines { After = "60" }))
        {
            Type = StyleValues.Paragraph,
            StyleId = "Subtitle",
        },
        Heading("Heading1", "heading 1", "28", 0, before: "360"),
        Heading("Heading2", "heading 2", "24", 1, before: "240"));

    // The only paragraphs that keep with the next one: a heading must not end a page (§11.5).
    private static Style Heading(string styleId, string name, string size, int outlineLevel, string before) => new(
        new StyleName { Val = name },
        new BasedOn { Val = "Normal" },
        new NextParagraphStyle { Val = "Normal" },
        new PrimaryStyle(),
        new StyleParagraphProperties(
            new KeepNext(),
            new SpacingBetweenLines { Before = before, After = "120" },
            new OutlineLevel { Val = outlineLevel }),
        new StyleRunProperties(new Bold(), new FontSize { Val = size }, new FontSizeComplexScript { Val = size }))
    {
        Type = StyleValues.Paragraph,
        StyleId = styleId,
    };

    // A real list whose items show no number or bullet, because the box in front of each goal is its marker.
    private static Numbering GoalList() => new(
        new AbstractNum(
            new MultiLevelType { Val = MultiLevelValues.SingleLevel },
            new Level(
                new NumberingFormat { Val = NumberFormatValues.None },
                new LevelText { Val = string.Empty },
                new PreviousParagraphProperties(new Indentation { Left = "284" }))
            {
                LevelIndex = 0,
            })
        {
            AbstractNumberId = 0,
        },
        new NumberingInstance(new AbstractNumId { Val = 0 }) { NumberID = GoalListId });
}
