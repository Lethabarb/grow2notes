using Grow2Notes.Web.Features.Reports;

namespace Grow2Notes.Tests.Features.Reports;

public sealed class ReportLayoutTests
{
    private const string Nbsp = " ";

    // 23:14 UTC on 1 October is 9:14 am on 2 October in Melbourne.
    private static readonly DailyReportModel Model = new(
        "Example Care",
        new DateOnly(2026, 10, 1),
        new DateTime(2026, 10, 1, 23, 14, 0, DateTimeKind.Utc),
        "Jo Smith",
        []);

    [Fact]
    public void The_footer_names_the_exporter_and_time_of_the_title_block_and_the_title_names_the_day()
    {
        var document = ReportLayout.DailyReport(Model);

        Assert.Equal(
            $"Confidential: health information. Copy exported from Grow2Notes by Jo Smith, Fri 2{Nbsp}Oct 2026, "
            + $"9:14{Nbsp}am. The record in Grow2Notes is authoritative.",
            document.Footer);
        Assert.Equal($"Daily progress notes: Thu 1{Nbsp}Oct 2026", document.Title);
        Assert.Equal("Example Care", document.OrganisationName);

        var subtitles = document.Blocks.Where(block => block.Kind == ReportBlockKind.Subtitle)
            .Select(block => block.Text)
            .ToList();
        var exportedBy = Assert.Single(subtitles, text => text.StartsWith("Exported by ", StringComparison.Ordinal));
        var asAt = Assert.Single(subtitles, text => text.StartsWith("As at ", StringComparison.Ordinal));
        Assert.Contains(exportedBy["Exported by ".Length..] + ", " + asAt["As at ".Length..] + ".", document.Footer);
    }
}
