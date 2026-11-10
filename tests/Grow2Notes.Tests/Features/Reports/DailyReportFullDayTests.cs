using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Time.Testing;
using UglyToad.PdfPig;

namespace Grow2Notes.Tests.Features.Reports;

/// <summary>
/// A busy day (T22, D104): 17 participants, a few of whom have a note long enough to fill more than a page, as a
/// manager downloads it. In both formats the blocks are in given-name order and each starts a new page; the PDF's
/// index numbers are the pages the blocks start on; the Word file's index holds a PAGEREF field for each block's
/// bookmark, with a dash for its stored result, and asks Word to update its fields; the running header shows from
/// page 2 and "Page n of N" is on every page; and a note dated the next day is not in the report.
/// </summary>
[Collection<SqlServerCollection>]
public sealed partial class DailyReportFullDayTests(Grow2NotesFactory factory) : IClassFixture<Grow2NotesFactory>
{
    private const string Title = "SUPPORT NOTES: Thursday 01.10.2026";

    // 9:14 am on Friday 2 October 2026 in Melbourne, before the daylight-saving change on the 4th.
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 23, 14, 0, TimeSpan.Zero);

    private static readonly DateOnly NoteDate = new(2026, 10, 1);

    private static readonly DateTime StartedAtUtc = new(2026, 10, 1, 0, 14, 0, DateTimeKind.Utc);

    private static readonly DateTime SubmittedAtUtc = new(2026, 10, 1, 6, 42, 0, DateTimeKind.Utc);

    // Added out of order, and sorted by the test, so that the report's order is proved by its own.
    private static readonly string[] GivenNames =
    [
        "Quin", "Abel", "Maya", "Beth", "Owen", "Cara", "Kira", "Dev", "Pia", "Ella",
        "Jack", "Finn", "Noah", "Gia", "Liam", "Hugo", "Iris",
    ];

    // Notes that are long enough to take a second page.
    private static readonly HashSet<string> LongNotes = ["Beth", "Jack", "Quin"];

    [Fact]
    public async Task A_day_of_17_participants_is_in_order_a_page_each_with_its_index_and_furniture_in_both_formats()
    {
        var organisation = await factory.Services.AddOrganisationAsync();
        foreach (var given in GivenNames)
        {
            var participant = await factory.Services.AddParticipantAsync(
                organisation, givenName: given, familyName: $"Of{given}");
            await factory.Services.AddSubmittedAsync(
                organisation,
                new(participant.Id, NoteDate, organisation.WorkerId, StartedAtUtc, SubmittedAtUtc, NarrativeOf(given)));
        }

        var late = await factory.Services.AddParticipantAsync(organisation, givenName: "Zed", familyName: "Nextday");
        await factory.Services.AddSubmittedAsync(
            organisation,
            new(
                late.Id,
                NoteDate.AddDays(1),
                organisation.WorkerId,
                StartedAtUtc.AddDays(1),
                SubmittedAtUtc.AddDays(1),
                "A note for the next day."));
        await using var app = factory.WithWebHostBuilder(builder => builder.UseClock(new FakeTimeProvider(Now)));
        using var client = app.CreateHttpsClient().SignInAs(organisation.ManagerId);
        string[] expected = [.. GivenNames.Order().Select(given => $"{given} OF{given}".ToUpperInvariant())];

        var docx = await client.GetByteArrayAsync(
            "/api/reports/daily/2026-10-01/export?format=docx", TestContext.Current.CancellationToken);
        var pdf = await client.GetByteArrayAsync(
            "/api/reports/daily/2026-10-01/export?format=pdf", TestContext.Current.CancellationToken);

        AssertWord(docx, expected);
        AssertPdf(pdf, expected);
    }

    // The blocks are in order, each paragraph has its page break and its bookmark, and the index asks for the pages.
    private static void AssertWord(byte[] docx, string[] expected)
    {
        using var stream = new MemoryStream(docx);
        using var document = WordprocessingDocument.Open(stream, false);
        var main = document.MainDocumentPart!;
        var paragraphs = main.Document!.Body!.Elements<Paragraph>().ToList();

        var blocks = paragraphs
            .Where(p => p.ParagraphProperties?.ParagraphStyleId?.Val?.Value == "Heading1"
                && p.InnerText.StartsWith("NAME: ", StringComparison.Ordinal))
            .ToList();
        Assert.Equal(expected.Select(name => $"NAME: {name}"), blocks.Select(block => block.InnerText));
        Assert.All(blocks, block => Assert.NotNull(block.ParagraphProperties!.PageBreakBefore));
        Assert.Equal(
            Enumerable.Range(1, expected.Length).Select(i => $"p{i}"),
            blocks.Select(block => block.Elements<BookmarkStart>().Single().Name!.Value));

        var entries = paragraphs.Where(p => p.Descendants<FieldCode>().Any()).ToList();
        Assert.Equal(expected.Length, entries.Count);
        for (var i = 0; i < entries.Count; i++)
        {
            Assert.Equal($"PAGEREF p{i + 1} \\h", entries[i].Descendants<FieldCode>().Single().Text.Trim());
            Assert.Equal(expected[i] + "–", entries[i].InnerText);
        }

        Assert.True(main.DocumentSettingsPart!.Settings!.Elements<UpdateFieldsOnOpen>().Single().Val!.Value);
        var furniture = ReportText.WordHeadersAndFooters(docx);
        Assert.True(furniture.TitlePage);
        Assert.Null(furniture.FirstHeader);
        Assert.Equal(Title, furniture.DefaultHeader);
        Assert.EndsWith(" Page {PAGE} of {NUMPAGES}", furniture.FirstFooter);
        Assert.Equal(furniture.FirstFooter, furniture.DefaultFooter);
        Assert.DoesNotContain("OFZED", string.Join('\n', ReportText.WordParagraphs(docx)), StringComparison.Ordinal);
    }

    // The block of each participant is the first thing on a page, the index numbers are those pages, and the furniture
    // is there on every page.
    private static void AssertPdf(byte[] pdf, string[] expected)
    {
        using var document = PdfDocument.Open(pdf);
        var startPages = new List<(int Page, string Name)>();
        foreach (var page in document.GetPages())
        {
            var body = page.GetWords().Where(word => !ReportText.IsFurniture(word)).Select(word => word.Text).ToList();
            if (body is ["NAME:", var given, var family, ..])
            {
                startPages.Add((page.Number, $"{given} {family}"));
            }
        }

        Assert.Equal(expected, startPages.Select(start => start.Name));
        Assert.All(startPages, start => Assert.True(start.Page >= 2));
        Assert.Equal(startPages.Select(start => start.Page).Order(), startPages.Select(start => start.Page));
        Assert.True(document.NumberOfPages > expected.Length + 1, "Some notes should take a second page.");

        var firstWords = document.GetPage(1).GetWords()
            .Where(word => !ReportText.IsFurniture(word))
            .Select(word => word.Text);
        var firstPage = ReportText.Normalised(ReportText.WithoutLeaders(string.Join(' ', firstWords)));
        var indexed = IndexEntry().Matches(firstPage.Split("Participants")[1])
            .Select(entry => (Page: int.Parse(entry.Groups[2].Value), Name: entry.Groups[1].Value))
            .ToList();
        Assert.Equal(startPages, indexed);

        var furniture = ReportText.PdfHeadersAndFooters(pdf);
        Assert.Equal(string.Empty, furniture[0].Header);
        Assert.All(furniture.Skip(1), page => Assert.Equal(ReportText.Normalised(Title), page.Header));
        for (var page = 1; page <= furniture.Count; page++)
        {
            Assert.EndsWith($" Page {page} of {furniture.Count}", furniture[page - 1].Footer);
        }

        Assert.DoesNotContain("OFZED", string.Join(' ', ReportText.PdfWords(pdf)), StringComparison.Ordinal);
    }

    // A short note, or one of 70 lines, which is longer than a page.
    private static string NarrativeOf(string given) => LongNotes.Contains(given)
        ? string.Join('\n', Enumerable.Range(1, 70).Select(line => $"{given} line {line}."))
        : $"{given} had a quiet day.";

    // "ABEL OFABEL 2": two words in capitals and the page, after the dot leader is taken out.
    [GeneratedRegex(@"([A-Z]+ OF[A-Z]+) (\d+)")]
    private static partial Regex IndexEntry();
}
