using System.Text.Json.Serialization;

namespace Grow2Notes.Web.Features.Notes;

/// <summary>
/// The note form with the caller's working copy, as <c>GET {base}/draft</c> answers it (design.md §6.3): for a note
/// with no live note yet, the unsaved template, with <see cref="BaseVersion"/> <c>0</c>, <see cref="IsSaved"/> false,
/// the participant's active goals in the manager's order, unticked, no <see cref="Narrative"/>, unflagged, and
/// <see cref="ListsVersion"/>, which the first save sends back (§5.7); or the caller's own draft, with its snapshot,
/// ticks, text and flag, and <see cref="SavedAtUtc"/>, the time of its last save. <see cref="GuidePrompts"/> are the
/// organisation's as they are now, which a draft's empty box shows (§4.10). <see cref="FlagReason"/> is <c>null</c>
/// unless the draft is flagged with a reason, and is written so. The rest of §6.3's shape comes with what uses it: T13
/// adds the common items.
/// </summary>
internal sealed record DraftForm(
    int BaseVersion,
    bool IsSaved,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ListsVersion,
    string GuidePrompts,
    IReadOnlyList<DraftFormGoal> Goals,
    string Narrative,
    bool IsFlagged,
    string? FlagReason,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DateTime? SavedAtUtc);

/// <summary>A goal on the note form: its wording as the form shows it, and whether it is ticked as reached.</summary>
internal sealed record DraftFormGoal(Guid Id, string Text, bool IsTicked);
