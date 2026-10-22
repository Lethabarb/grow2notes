using System.Text.Json.Serialization;

namespace Grow2Notes.Web.Features.Notes;

/// <summary>
/// The note form with the caller's working copy, as <c>GET {base}/draft</c> answers it (design.md §6.3): for a note
/// with no live note yet, the unsaved template, with <see cref="BaseVersion"/> <c>0</c>, <see cref="IsSaved"/> false,
/// the participant's active goals in the manager's order, unticked, the common item groups as a new draft's snapshot
/// will hold them, Every note first, with the groups that the participant's latest earlier note picked picked, no
/// <see cref="Narrative"/>, unflagged, and <see cref="ListsVersion"/>, which the first save sends back (§5.7); or the
/// caller's own draft, with its snapshot, picks, ticks, text and flag, and <see cref="SavedAtUtc"/>, the time of its
/// last save. <see cref="GuidePrompts"/> are the organisation's as they are now, which a draft's empty box shows
/// (§4.10). <see cref="FlagReason"/> is <c>null</c> unless the draft is flagged with a reason, and is written so.
/// </summary>
internal sealed record DraftForm(
    int BaseVersion,
    bool IsSaved,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ListsVersion,
    string GuidePrompts,
    IReadOnlyList<DraftFormItem> Goals,
    IReadOnlyList<DraftFormGroup> CommonItemGroups,
    string Narrative,
    bool IsFlagged,
    string? FlagReason,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DateTime? SavedAtUtc);

/// <summary>
/// A common item group on the note form, with its name as the form shows it, whether it is Every note, and whether it
/// is picked. Empty groups are included, as the lists' version covers them (§5.7).
/// </summary>
internal sealed record DraftFormGroup(
    Guid Id, string Name, bool IsEveryNote, bool IsPicked, IReadOnlyList<DraftFormItem> Items);

/// <summary>
/// A goal or a common item on the note form: its wording as the form shows it, and whether it is ticked, as reached
/// for a goal and as done for a common item.
/// </summary>
internal sealed record DraftFormItem(Guid Id, string Text, bool IsTicked);
