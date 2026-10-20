using System.Security.Cryptography;
using System.Text.Json;

namespace Grow2Notes.Web.Features.Notes;

/// <summary>
/// The version of the lists that a new note's template shows (design.md §5.7): a SHA-256, as base64, of the active
/// goals' IDs and wording in the template's order, and of every common item group's ID and name, Every note and empty
/// groups included, with its items' IDs and wording, in order. So a goal or an item added, reworded, archived,
/// restored, moved or reordered, or a group added, renamed, archived, restored or reordered, gives another. Picks and
/// ticks are not in it, so the same lists give the same version whatever the starting picks. The first save sends it
/// back, and one that no longer matches is refused, as the form showed other lists.
/// </summary>
internal static class ListsVersion
{
    // As JSON, arrays of [ID, wording] pairs and a group's ID, name and such pairs, which no two different lists write
    // alike.
    public static string Of(NoteSnapshot template) =>
        Convert.ToBase64String(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            goals = template.Goals.Select(goal => Pair(goal.SourceId, goal.Text)),
            groups = template.Groups.Select(group => new
            {
                id = group.GroupId,
                name = group.Name,
                items = template.ItemsOf(group).Select(item => Pair(item.SourceId, item.Text)),
            }),
        })));

    private static string[] Pair(Guid id, string text) => [id.ToString(), text];
}
