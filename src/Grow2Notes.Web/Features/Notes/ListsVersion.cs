using System.Security.Cryptography;
using System.Text.Json;

namespace Grow2Notes.Web.Features.Notes;

/// <summary>
/// The version of the lists that a new note's template shows (design.md §5.7): a SHA-256, as base64, of the active
/// goals' IDs and wording in the template's order, so that a goal added, reworded, archived, restored or moved gives
/// another. The first save sends it back, and one that no longer matches is refused, as the form showed other lists.
/// T13 adds the common items and their groups.
/// </summary>
internal static class ListsVersion
{
    // As JSON, an array of [ID, wording] pairs, which no two different lists write alike.
    public static string Of(IReadOnlyList<DraftFormGoal> goals) =>
        Convert.ToBase64String(SHA256.HashData(
            JsonSerializer.SerializeToUtf8Bytes(goals.Select(goal => new[] { goal.Id.ToString(), goal.Text }))));
}
