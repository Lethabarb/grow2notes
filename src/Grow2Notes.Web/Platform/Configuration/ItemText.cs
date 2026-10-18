using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Grow2Notes.Web.Data;

namespace Grow2Notes.Web.Platform.Configuration;

/// <summary>
/// The one rule for the text of a row of a list that managers edit, a goal's or a common item's, and for a common item
/// group's name, as each is stored (design.md §5.3, A6): one line, as a note shows it beside its tick box or over its
/// group's items, cleaned as the screen cleans it before it sends it (list-editor.md *Implementation notes*), so that
/// the two agree on what is counted, and checked here, as the server is the gate (form-validation.md rule 4). Each kind
/// refuses a text in its own words, as the screen words it, under the request's member that holds it, so each has its
/// member, its limit and its two messages here.
/// </summary>
internal sealed partial class ItemText
{
    // The keys of a refusal's message: the C# names of the requests' members, which ValidationProblems answers in
    // camelCase, as `text` or `name`.
    private const string TextField = "Text";
    private const string NameField = "Name";

    /// <summary>A goal's text (participants.md B6).</summary>
    public static readonly ItemText Goal =
        new(TextField, Limits.ItemText, "Enter the goal", $"Goal must be {Limits.ItemText} characters or less");

    /// <summary>A common item's text (common-items.md *States*).</summary>
    public static readonly ItemText CommonItem = new(
        TextField,
        Limits.ItemText,
        "Enter the common item",
        $"Common item must be {Limits.ItemText} characters or less");

    /// <summary>A common item group's name (design.md §4.9 <i>States</i>).</summary>
    public static readonly ItemText GroupName = new(
        NameField,
        Limits.GroupName,
        "Enter the group name",
        $"Group name must be {Limits.GroupName} characters or less");

    // The request's member that holds the text, under which a refusal's message is keyed.
    private readonly string field;

    // The most characters that the text may have once it is cleaned.
    private readonly int limit;

    // The message for a text that is missing, or nothing once it is cleaned.
    private readonly string enter;

    // The message for a text longer than the limit once it is cleaned.
    private readonly string tooLong;

    private ItemText(string field, int limit, string enter, string tooLong)
    {
        this.field = field;
        this.limit = limit;
        this.enter = enter;
        this.tooLong = tooLong;
    }

    /// <summary>
    /// Reads <paramref name="sent"/>: each line break, with the spaces around it, becomes one space and the ends are
    /// trimmed, then 1 to this kind's limit of characters must remain, <see cref="Limits.ItemText"/> for a text and
    /// <see cref="Limits.GroupName"/> for a name, counted in UTF-16 units, as the column's size and the screen's count
    /// count them. A missing text is refused as an empty one. A refusal gives the first failing check's message, in
    /// this kind's words, under its member, <c>Text</c> or <c>Name</c>, as T02's server words a participant's fields.
    /// </summary>
    /// <param name="sent">The text as sent, or <see langword="null"/> when the body had none.</param>
    /// <param name="text">The text to store, when it is not refused.</param>
    /// <param name="errors">The refusal's message under this kind's member, when it is refused.</param>
    public bool TryRead(
        string? sent,
        [NotNullWhen(true)] out string? text,
        [NotNullWhen(false)] out IReadOnlyDictionary<string, string[]>? errors)
    {
        var cleaned = sent is null ? "" : LineBreaks().Replace(sent, " ").Trim();
        var refusal = cleaned.Length switch
        {
            0 => enter,
            var length when length > limit => tooLong,
            _ => null,
        };

        text = refusal is null ? cleaned : null;
        errors = refusal is null ? null : new Dictionary<string, string[]> { [field] = [refusal] };
        return refusal is null;
    }

    // A run of line breaks, CR, LF or both, with the spaces on either side of it, as the screen's own pattern has it.
    [GeneratedRegex(@"\s*[\r\n]+\s*")]
    private static partial Regex LineBreaks();
}
