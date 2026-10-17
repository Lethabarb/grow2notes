using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Grow2Notes.Web.Data;

namespace Grow2Notes.Web.Features.Participants;

/// <summary>
/// A goal's text as it is stored (design.md §5.3, A6): one line, as a note shows it beside its tick box, cleaned as
/// the screen cleans it before it sends it (list-editor.md *Implementation notes*), so that the two agree on what is
/// counted, and checked here, as the server is the gate (form-validation.md rule 4).
/// </summary>
internal static partial class GoalText
{
    /// <summary>The message for a text that is missing, or nothing once it is cleaned.</summary>
    public const string EnterTheGoal = "Enter the goal";

    /// <summary>The message for a text longer than <see cref="Limits.ItemText"/> once it is cleaned.</summary>
    public static readonly string TooLong = $"Goal must be {Limits.ItemText} characters or less";

    /// <summary>
    /// Reads <paramref name="request"/>'s text by participants.md's goal rules: each line break, with the spaces around
    /// it, becomes one space and the ends are trimmed, then 1 to <see cref="Limits.ItemText"/> characters must remain,
    /// counted in UTF-16 units, as the column's size and the screen's count count them. A refusal gives the first
    /// failing check's message, worded as the screen words it, under <c>Text</c>, as T02's server words a participant's
    /// fields.
    /// </summary>
    /// <param name="request">The text as sent.</param>
    /// <param name="text">The text to store, when it is not refused.</param>
    /// <param name="errors">The refusal's message under <c>Text</c>, when it is refused.</param>
    public static bool TryRead(
        GoalTextRequest request,
        [NotNullWhen(true)] out string? text,
        [NotNullWhen(false)] out IReadOnlyDictionary<string, string[]>? errors)
    {
        var cleaned = request.Text is null ? "" : LineBreaks().Replace(request.Text, " ").Trim();
        var refusal = cleaned.Length switch
        {
            0 => EnterTheGoal,
            > Limits.ItemText => TooLong,
            _ => null,
        };

        text = refusal is null ? cleaned : null;
        errors = refusal is null ? null : new Dictionary<string, string[]> { [nameof(request.Text)] = [refusal] };
        return refusal is null;
    }

    // A run of line breaks, CR, LF or both, with the spaces on either side of it, as the screen's own pattern has it.
    [GeneratedRegex(@"\s*[\r\n]+\s*")]
    private static partial Regex LineBreaks();
}
