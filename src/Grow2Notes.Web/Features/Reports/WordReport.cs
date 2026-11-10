using System.Globalization;
using System.Text;
using System.Xml;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.ExtendedProperties;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace Grow2Notes.Web.Features.Reports;

/// <summary>
/// Renders a report document as a Word file: Arial 11 pt on A4 with 20 mm margins, the Title and Subtitle styles, real
/// headings (so each participant is in Word's Navigation pane), the goals and common items as a list with no visible
/// marker, a row of boxes for a <see cref="ReportBlockKind.TickRow"/>, a heading that starts a new page and is
/// bookmarked, an index whose page numbers are PAGEREF fields (A156), a running header from page 2 and a footer with
/// the page number on every page. The sizes and distances are <see cref="ReportStyle"/>'s.
/// </summary>
internal static class WordReport
{
    private const string TickFont = "Segoe UI Symbol";
    private const string TextFont = "Arial";
    private const int TickListId = 1;
    private const int PageWidth = 11906;
    private const int PageHeight = 16838;

    // What an index entry shows until a viewer computes the field. Word on the web and phones do not, and a dash is
    // honest where a stale number would not be.
    private const string UncomputedPage = "–";

    public static byte[] Render(ReportDocument report)
    {
        var margin = ReportStyle.Twips(ReportStyle.MarginMillimetres);
        var furnitureDistance = ReportStyle.Twips(ReportStyle.FurnitureDistanceMillimetres);
        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var main = document.AddMainDocumentPart();
            main.AddNewPart<StyleDefinitionsPart>().Styles = Styles();
            main.AddNewPart<NumberingDefinitionsPart>().Numbering = TickList();

            // Word asks once to update the fields, which fills in the index's page numbers.
            main.AddNewPart<DocumentSettingsPart>().Settings = new Settings(new UpdateFieldsOnOpen { Val = true });

            // With titlePg set and no first-page header, page 1 has no header; the footer is on every page.
            var headerPart = main.AddNewPart<HeaderPart>();
            headerPart.Header = new Header(new Paragraph(
                new ParagraphProperties(new ParagraphStyleId { Val = "Header" }), Lines(report.Title)));

            var body = new Body();
            for (var i = 0; i < report.Blocks.Count; i++)
            {
                body.Append(Paragraph(report.Blocks[i], bookmarkId: i + 1, textWidth: PageWidth - (2 * margin)));
            }

            body.Append(new SectionProperties(
                new HeaderReference { Type = HeaderFooterValues.Default, Id = main.GetIdOfPart(headerPart) },
                AddFooter(main, report.Footer, HeaderFooterValues.Default),
                AddFooter(main, report.Footer, HeaderFooterValues.First),
                new PageSize { Width = PageWidth, Height = PageHeight },
                new PageMargin
                {
                    Top = margin,
                    Right = (uint)margin,
                    Bottom = margin,
                    Left = (uint)margin,
                    Header = (uint)furnitureDistance,
                    Footer = (uint)furnitureDistance,
                    Gutter = 0,
                },
                new TitlePage()));
            main.Document = new Document(body);

            // Word has no organisation field of its own, so Company holds it. No author is named.
            document.PackageProperties.Title = report.Title;
            document.AddExtendedFilePropertiesPart().Properties =
                new Properties(new Company(Valid(report.OrganisationName)));
        }

        return stream.ToArray();
    }

    private static Paragraph Paragraph(ReportBlock block, int bookmarkId, int textWidth) => block.Kind switch
    {
        ReportBlockKind.Title => Styled("Title", block.Text),
        ReportBlockKind.Subtitle => Styled("Subtitle", block.Text),
        ReportBlockKind.Heading1 => Heading1(block, bookmarkId),
        ReportBlockKind.Heading2 => Styled("Heading2", block.Text),
        ReportBlockKind.Heading3 => Styled("Heading3", block.Text),
        ReportBlockKind.Line => new Paragraph(Lines(block.Text)),
        ReportBlockKind.Tick => Tick(block),
        ReportBlockKind.TickRow => TickRow(block),
        ReportBlockKind.IndexEntry => IndexEntry(block, textWidth),
        _ => throw new ArgumentOutOfRangeException(nameof(block)),
    };

    private static Paragraph Styled(string styleId, string text) =>
        new(new ParagraphProperties(new ParagraphStyleId { Val = styleId }), Lines(text));

    // A heading may start a new page, and may be the place that the index's page number is read from, as a bookmark
    // around its text.
    private static Paragraph Heading1(ReportBlock block, int bookmarkId)
    {
        var properties = new ParagraphProperties(new ParagraphStyleId { Val = "Heading1" });
        if (block.PageBreakBefore)
        {
            properties.Append(new PageBreakBefore());
        }

        var paragraph = new Paragraph(properties);
        var id = bookmarkId.ToString(CultureInfo.InvariantCulture);
        if (block.Anchor is { } anchor)
        {
            paragraph.Append(new BookmarkStart { Id = id, Name = anchor });
        }

        paragraph.Append(Lines(block.Text));
        if (block.Anchor is not null)
        {
            paragraph.Append(new BookmarkEnd { Id = id });
        }

        return paragraph;
    }

    // The name, a dot leader out to the right margin and the page of the heading that the anchor marks, as a complex
    // field, which Word fills in once it is asked to (A156).
    private static Paragraph IndexEntry(ReportBlock block, int textWidth) => new(
        new ParagraphProperties(new Tabs(new TabStop
        {
            Val = TabStopValues.Right,
            Leader = TabStopLeaderCharValues.Dot,
            Position = textWidth,
        })),
        Lines(block.Text),
        new Run(new TabChar()),
        new Run(new FieldChar { FieldCharType = FieldCharValues.Begin }),
        new Run(new FieldCode($" PAGEREF {block.Anchor} \\h ") { Space = SpaceProcessingModeValues.Preserve }),
        new Run(new FieldChar { FieldCharType = FieldCharValues.Separate }),
        new Run(new Text(UncomputedPage)),
        new Run(new FieldChar { FieldCharType = FieldCharValues.End }));

    // The goals and common items are text, not form controls, so the file reads as a record. The boxes come from a
    // symbol font because Arial has no ☐ or ☑.
    private static Paragraph Tick(ReportBlock block) => new(
        new ParagraphProperties(new NumberingProperties(
            new NumberingLevelReference { Val = 0 },
            new NumberingId { Val = TickListId })),
        Box(block.IsTicked),
        Lines(" " + block.Text));

    // The boxes of a row, each with its label, two spaces apart, on a line of their own.
    private static Paragraph TickRow(ReportBlock block)
    {
        var paragraph = new Paragraph();
        for (var i = 0; i < block.Ticks.Count; i++)
        {
            var tick = block.Ticks[i];
            paragraph.Append(Box(tick.IsTicked));
            paragraph.Append(Lines(i < block.Ticks.Count - 1 ? $" {tick.Text}  " : $" {tick.Text}"));
        }

        return paragraph;
    }

    private static Run Box(bool isTicked) => new(
        new RunProperties(new RunFonts
        {
            Ascii = TickFont,
            HighAnsi = TickFont,
            ComplexScript = TickFont,
            EastAsia = TickFont,
        }),
        new Text(isTicked ? "☑" : "☐"));

    // The same footer on page 1 and on the other pages: the sentence, then "Page n of N" from two fields. A field has
    // no stored result, because a viewer that does not compute fields would show that one number on every page.
    private static FooterReference AddFooter(MainDocumentPart main, string footer, HeaderFooterValues type)
    {
        var part = main.AddNewPart<FooterPart>();
        part.Footer = new Footer(new Paragraph(
            new ParagraphProperties(new ParagraphStyleId { Val = "Footer" }),
            Lines(footer + ReportLayout.FooterPage),
            new SimpleField { Instruction = " PAGE " },
            Lines(ReportLayout.FooterOf),
            new SimpleField { Instruction = " NUMPAGES " }));
        return new FooterReference { Type = type, Id = main.GetIdOfPart(part) };
    }

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
                valid.Append(XmlConvert.IsXmlChar(text[i]) ? text[i] : '\uFFFD');
            }
        }

        return valid.ToString();
    }

    // A size in points as the half-points that Word's style properties are written in.
    private static string Half(int points) => ReportStyle.HalfPoints(points).ToString(CultureInfo.InvariantCulture);

    private static Styles Styles() => new(
        new DocDefaults(
            new RunPropertiesDefault(new RunPropertiesBaseStyle(
                new RunFonts { Ascii = TextFont, HighAnsi = TextFont, ComplexScript = TextFont, EastAsia = TextFont },
                new FontSize { Val = Half(ReportStyle.BodyPoints) },
                new FontSizeComplexScript { Val = Half(ReportStyle.BodyPoints) },
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
            new StyleRunProperties(
                new Bold(),
                new FontSize { Val = Half(ReportStyle.TitlePoints) },
                new FontSizeComplexScript { Val = Half(ReportStyle.TitlePoints) }))
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
        Heading("Heading1", "heading 1", ReportStyle.Heading1Points, 0, before: "360"),
        Heading("Heading2", "heading 2", ReportStyle.Heading2Points, 1, before: "240"),
        Heading("Heading3", "heading 3", ReportStyle.Heading3Points, 2, before: "120"),
        Furniture("Header", "header"),
        Furniture("Footer", "footer"));

    // The only paragraphs that keep with the next one: a heading must not end a page (§11.5).
    private static Style Heading(string styleId, string name, int points, int outlineLevel, string before) => new(
        new StyleName { Val = name },
        new BasedOn { Val = "Normal" },
        new NextParagraphStyle { Val = "Normal" },
        new PrimaryStyle(),
        new StyleParagraphProperties(
            new KeepNext(),
            new SpacingBetweenLines { Before = before, After = "120" },
            new OutlineLevel { Val = outlineLevel }),
        new StyleRunProperties(
            new Bold(),
            new FontSize { Val = Half(points) },
            new FontSizeComplexScript { Val = Half(points) }))
    {
        Type = StyleValues.Paragraph,
        StyleId = styleId,
    };

    // The running header and the footer are small, so that they stay out of the way of the body.
    private static Style Furniture(string styleId, string name) => new(
        new StyleName { Val = name },
        new BasedOn { Val = "Normal" },
        new PrimaryStyle(),
        new StyleParagraphProperties(new SpacingBetweenLines { After = "0" }),
        new StyleRunProperties(
            new FontSize { Val = Half(ReportStyle.FurniturePoints) },
            new FontSizeComplexScript { Val = Half(ReportStyle.FurniturePoints) }))
    {
        Type = StyleValues.Paragraph,
        StyleId = styleId,
    };

    // A real list whose items show no number or bullet, because the box in front of each item is its marker.
    private static Numbering TickList() => new(
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
        new NumberingInstance(new AbstractNumId { Val = 0 }) { NumberID = TickListId });
}
