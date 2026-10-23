namespace Grow2Notes.Web.Features.Reports;

/// <summary>
/// What a report file says and in what order (design.md §11.3), as <see cref="ReportBlock"/>s that both renderers read.
/// The file's own words are constants here, outside the SPA's copy, as the file is not a screen.
/// </summary>
internal static class ReportLayout
{
    private const string DailyTitle = "Daily progress notes";

    private const string AsAt = "As at ";

    private const string ExportedBy = "Exported by ";

    private const string WrittenBy = "Written by ";

    private const string Submitted = " · Submitted ";

    private const string Goals = "Goals";

    private const string NoGoals = "No goals set";

    private const string GuidedNotes = "Guided notes";

    /// <summary>
    /// The day's report: the organisation's name, the title block, then each note under its participant's name, in the
    /// model's order.
    /// </summary>
    public static IReadOnlyList<ReportBlock> DailyReport(DailyReportModel model)
    {
        List<ReportBlock> blocks =
        [
            new(ReportBlockKind.Title, model.OrganisationName),
            new(ReportBlockKind.Subtitle, DailyTitle),
            new(ReportBlockKind.Subtitle, ReportDates.DateLong(model.NoteDate)),
            new(ReportBlockKind.Subtitle, AsAt + ReportDates.DateTimeWithYear(model.AsAtUtc)),
            new(ReportBlockKind.Subtitle, ExportedBy + model.ExportedBy),
        ];
        foreach (var note in model.Notes)
        {
            blocks.Add(new(ReportBlockKind.Heading1, note.ParticipantName));
            blocks.AddRange(NoteBody(note));
        }

        return blocks;
    }

    /// <summary>
    /// A note under its participant's name: who wrote it and when it was first submitted, its goals in the order of
    /// its version, and its Guided notes exactly as stored. The time carries its day (A79), so a note first submitted
    /// on a later day already shows its own date.
    /// </summary>
    public static IEnumerable<ReportBlock> NoteBody(DailyReportNote note)
    {
        yield return new(
            ReportBlockKind.Line,
            $"{WrittenBy}{note.AuthorDisplayName}{Submitted}{ReportDates.DateTimeShort(note.FirstSubmittedAtUtc)}");
        yield return new(ReportBlockKind.Heading2, Goals);
        if (note.Current.Goals.Count == 0)
        {
            yield return new(ReportBlockKind.Line, NoGoals);
        }

        foreach (var goal in note.Current.Goals)
        {
            yield return new(ReportBlockKind.Tick, goal.Text, goal.IsTicked);
        }

        yield return new(ReportBlockKind.Heading2, GuidedNotes);
        yield return new(ReportBlockKind.Line, note.Current.Narrative);
    }
}
