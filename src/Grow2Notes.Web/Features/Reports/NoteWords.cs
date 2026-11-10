namespace Grow2Notes.Web.Features.Reports;

/// <summary>
/// The words that a note shares with its report files (D104, A159): the labels of the attendance ticks, the two goal
/// terms, the Medication box and the Daily Support Notes box, and the two sentences for a note with no goals and no
/// common items. The SPA's <c>noteWords</c> in <c>strings.ts</c> holds the same words, and a test holds the two to each
/// other, so that a screen and a file never name a section differently. Each field's name is its key in
/// <c>noteWords</c>, with a lower-case first letter.
/// </summary>
internal static class NoteWords
{
    internal const string NotIn = "Not In";

    internal const string LateNotice = "Late Notice";

    internal const string Sick = "Sick";

    internal const string ShortTermGoals = "Short term goals";

    internal const string MediumOrLongTermGoal = "Medium or Long term goal";

    internal const string Medication = "Medication";

    internal const string Details = "Details";

    internal const string DailySupportNotes = "Daily Support Notes";

    internal const string NoGoals = "No goals set";

    internal const string NoCommonItems = "No common items set";
}
