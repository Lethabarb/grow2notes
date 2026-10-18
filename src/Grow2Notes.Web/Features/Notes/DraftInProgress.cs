using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Grow2Notes.Web.Features.Notes;

/// <summary>
/// Another user's draft of the note that the caller asked for (design.md §3.2): who started it, by their display name,
/// and when, the note's <c>CreatedAtUtc</c>, which <c>409 note.in_progress_by_other</c> includes (§6.9).
/// </summary>
internal sealed record DraftInProgress(string AuthorDisplayName, DateTime StartedAtUtc)
{
    /// <summary>Answers <c>409 note.in_progress_by_other</c> with the author's name and start time.</summary>
    public ProblemHttpResult Problem() =>
        ErrorCode.InProgressByOther.Problem(new Dictionary<string, object?>
        {
            ["authorDisplayName"] = AuthorDisplayName,
            ["startedAtUtc"] = StartedAtUtc,
        });
}
