using System.Security.Cryptography;
using System.Text.Json;
using Grow2Notes.Web.Data;

namespace Grow2Notes.Web.Features.Notes;

/// <summary>
/// The version of the lists that a new note's template shows (design.md §5.7): a SHA-256, as base64, of the active
/// goals' IDs and wording in the template's order, and of every common item group's ID and name, Every note and empty
/// groups included, with its items' IDs and wording, in order. So a goal or an item added, reworded, archived,
/// restored, moved or reordered, or a group added, renamed, archived, restored or reordered, gives another. Picks and
/// ticks are not in it, so the same lists give the same version whatever the starting picks. The first save sends it
/// back, and one that no longer matches is refused, as the form showed other lists.
/// </summary>
/// <remarks>
/// A goal's term, an item's need for details and the Medication box count too, so a change to one between the form's
/// opening and the first save is refused, but only when they are set: lists that use none of them hash as they did
/// before T44, so a deploy refuses no open form (A154).
/// </remarks>
internal static class ListsVersion
{
    // As JSON, arrays of [ID, wording] pairs and a group's ID, name and such pairs, which no two different lists write
    // alike. A pair has a third element, "medium" or "detail", when the goal is Medium or long term or the item asks
    // for details, and the whole has medicationLine when the note has a Medication box.
    public static string Of(NoteSnapshot template)
    {
        var goals = template.Goals.Select(Pair);
        var groups = template.Groups.Select(group => new
        {
            id = group.GroupId,
            name = group.Name,
            items = template.ItemsOf(group).Select(Pair),
        });
        object lists = template.HasMedicationLine
            ? new { goals, groups, medicationLine = true }
            : new { goals, groups };
        return Convert.ToBase64String(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(lists)));
    }

    private static string[] Pair(DraftItem item) => item switch
    {
        { IsMediumOrLongTerm: true } => [item.SourceId.ToString(), item.Text, "medium"],
        { NeedsDetail: true } => [item.SourceId.ToString(), item.Text, "detail"],
        _ => [item.SourceId.ToString(), item.Text],
    };
}
