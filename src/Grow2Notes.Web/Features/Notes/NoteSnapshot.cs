using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform.Configuration;

namespace Grow2Notes.Web.Features.Notes;

/// <summary>
/// The lists that a note holds as they were when it was started (design.md §5.3, §5.7, A3): its common item groups,
/// Every note first, and its goals and common items, goals first, with the picks and ticks. It holds every rule about
/// them, whether it is a template or a working copy's snapshot, so that starting a draft, saving it, submitting it and
/// reading it apply the same rules.
/// </summary>
/// <param name="Groups">The common item groups, Every note first.</param>
/// <param name="Items">The goals, then the common items.</param>
/// <param name="HasMedicationLine">
/// Whether the note has a Medication box (A152): the participant's switch when the template was made, and a working
/// copy's or a version's own line after that.
/// </param>
/// <remarks>
/// A snapshot never changes its names, wording, order, membership or Medication line: <see cref="With"/> sets only the
/// picks, the ticks and the details.
/// </remarks>
internal sealed record NoteSnapshot(
    IReadOnlyList<DraftGroup> Groups, IReadOnlyList<DraftItem> Items, bool HasMedicationLine)
{
    // The screen sends only the IDs of what it shows and offers, so these answer only a request made some other way.
    private static readonly IReadOnlyDictionary<string, string[]> TickedOutsideTheGoals =
        new Dictionary<string, string[]>
        {
            [nameof(INoteContent.TickedGoalIds)] = ["Each ticked goal must be one of the note's goals."],
        };

    private static readonly IReadOnlyDictionary<string, string[]> PickedOutsideTheGroups =
        new Dictionary<string, string[]>
        {
            [nameof(INoteContent.PickedGroupIds)] = ["Each picked group must be one of the note's groups with items."],
        };

    private static readonly IReadOnlyDictionary<string, string[]> TickedOutsideThePickedGroups =
        new Dictionary<string, string[]>
        {
            [nameof(INoteContent.TickedCommonItemIds)] =
                ["Each ticked common item must be in Every note or a picked group."],
        };

    private static readonly IReadOnlyDictionary<string, string[]> DetailsForAnotherItem =
        new Dictionary<string, string[]>
        {
            [nameof(INoteContent.CommonItemDetails)] = ["Each detail must be for a common item that asks for them."],
        };

    private static readonly IReadOnlyDictionary<string, string[]> DetailTooLong =
        new Dictionary<string, string[]>
        {
            [nameof(INoteContent.CommonItemDetails)] = [$"Each detail must be {Limits.ItemDetail} characters or less."],
        };

    private static readonly IReadOnlyDictionary<string, string[]> DetailsMissing =
        new Dictionary<string, string[]>
        {
            [nameof(INoteContent.CommonItemDetails)] = ["Enter the details for each ticked item that asks for them."],
        };

    private static readonly IReadOnlyDictionary<string, string[]> MedicationWithoutALine =
        new Dictionary<string, string[]>
        {
            [nameof(INoteContent.Medication)] = ["This note has no Medication box."],
        };

    public static NoteSnapshot Of(NoteDraft draft) => new(draft.Groups, draft.Items, draft.Medication is not null);

    public IEnumerable<DraftItem> Goals => Items.Where(item => item.Section == NoteSection.Goal);

    public IEnumerable<DraftItem> CommonItems => Items.Where(item => item.Section == NoteSection.CommonItem);

    /// <summary>
    /// The groups that a note shows (D47): Every note and the picked groups, each with at least one item, with every
    /// item, ticked or not, in the snapshot's order. A group that is not picked leaves no trace.
    /// </summary>
    public IReadOnlyList<NoteReadGroup> ShownGroups => ReadGroups(group => group.IsEveryNote || group.IsPicked);

    /// <summary>
    /// The groups that the daily report prints (D104, A161): every group with at least one item, Every note first,
    /// picked or not, each with every item in the snapshot's order. The items of a group that is not picked are all
    /// unticked.
    /// </summary>
    public IReadOnlyList<NoteReadGroup> PrintedGroups => ReadGroups(_ => true);

    public IEnumerable<DraftItem> ItemsOf(DraftGroup group) =>
        CommonItems.Where(item => item.GroupId == group.GroupId);

    /// <summary>
    /// Whether the writer may pick <paramref name="group"/> (§5.7): not Every note, which a note always shows (D47),
    /// and with at least one item in this snapshot, as picking a group with none would show nothing.
    /// </summary>
    public bool Offers(DraftGroup group) => !group.IsEveryNote && ItemsOf(group).Any();

    /// <summary>This snapshot with the groups of <paramref name="picked"/> picked, if it offers them.</summary>
    public NoteSnapshot WithStartingPicks(IReadOnlySet<Guid> picked) =>
        this with
        {
            Groups =
            [
                .. Groups.Select(group => group with { IsPicked = Offers(group) && picked.Contains(group.GroupId) }),
            ],
        };

    /// <summary>
    /// The refusal of <paramref name="content"/>, as a single-key <c>422 validation.failed</c> errors object, or
    /// <c>null</c> if it is valid. The checks run in this order, each list a set: a ticked goal that is not one of
    /// the goals, then a picked ID that is not an offered group (Every note, a group with no item, or an ID outside
    /// the snapshot), then a ticked common item that is not in Every note or a picked group, unknown IDs included,
    /// then a detail for an ID that is not a common item of this snapshot that asks for details, or one over
    /// <see cref="Limits.ItemDetail"/> characters once cleaned, then Medication text on a note with no Medication
    /// box. A detail for an item that is not ticked is not refused: <see cref="With"/> drops it.
    /// </summary>
    public IReadOnlyDictionary<string, string[]>? RefusalOf(INoteContent content)
    {
        if (!content.TickedGoalIds.ToHashSet().IsSubsetOf(Goals.Select(goal => goal.SourceId)))
        {
            return TickedOutsideTheGoals;
        }

        var picked = content.PickedGroupIds.ToHashSet();
        if (!picked.IsSubsetOf(Groups.Where(Offers).Select(group => group.GroupId)))
        {
            return PickedOutsideTheGroups;
        }

        var shown = Groups
            .Where(group => group.IsEveryNote || picked.Contains(group.GroupId))
            .Select(group => group.GroupId)
            .ToHashSet();
        if (!content.TickedCommonItemIds.ToHashSet().IsSubsetOf(
            CommonItems.Where(item => shown.Contains(item.GroupId!.Value)).Select(item => item.SourceId)))
        {
            return TickedOutsideThePickedGroups;
        }

        return DetailsRefusalOf(content) ?? MedicationRefusalOf(content);
    }

    /// <summary>
    /// The refusal of a Submit or a Save changes, as a single-key <c>422 validation.failed</c> errors object, or
    /// <c>null</c>: a ticked common item that asks for details has none (A151). A draft's autosave never asks, as a
    /// writer may tick an item before writing its details.
    /// </summary>
    public IReadOnlyDictionary<string, string[]>? SubmitRefusal() =>
        CommonItems.Any(item => item.NeedsDetail is true && item.IsTicked && item.Detail is null)
            ? DetailsMissing
            : null;

    /// <summary>
    /// The Medication text to store for <paramref name="content"/>, which <see cref="RefusalOf"/> has passed: null
    /// when the note has no Medication box, and otherwise the text as written, or empty when it is blank (A152).
    /// </summary>
    public string? MedicationOf(INoteContent content) =>
        !HasMedicationLine ? null : string.IsNullOrWhiteSpace(content.Medication) ? "" : content.Medication;

    /// <summary>
    /// This snapshot with the picks, ticks and details of <paramref name="content"/>, which <see cref="RefusalOf"/>
    /// has passed, so that the items of a group that is not picked stay unticked. An item keeps a detail only while it
    /// is ticked and asks for one, cleaned as a row's text is (<see cref="ItemText.Cleaned"/>), with a blank one stored
    /// as none, so unticking an item or unpicking its group clears it (A151).
    /// </summary>
    public NoteSnapshot With(INoteContent content)
    {
        var tickedGoals = content.TickedGoalIds.ToHashSet();
        var picked = content.PickedGroupIds.ToHashSet();
        var tickedItems = content.TickedCommonItemIds.ToHashSet();
        return this with
        {
            Groups = [.. Groups.Select(group => group with { IsPicked = picked.Contains(group.GroupId) })],
            Items =
            [
                .. Items.Select(item =>
                {
                    var isTicked = (item.Section == NoteSection.Goal ? tickedGoals : tickedItems)
                        .Contains(item.SourceId);
                    return item with { IsTicked = isTicked, Detail = isTicked ? DetailOf(item, content) : null };
                }),
            ],
        };
    }

    private IReadOnlyList<NoteReadGroup> ReadGroups(Func<DraftGroup, bool> shown) =>
    [
        .. Groups
            .Where(shown)
            .Select(group => (group, items: ItemsOf(group).ToList()))
            .Where(listed => listed.items.Count > 0)
            .Select(listed => new NoteReadGroup(
                listed.group.Name,
                listed.group.IsEveryNote,
                [.. listed.items.Select(item => new NoteReadItem(item.Text, item.IsTicked, item.Detail))])),
    ];

    private IReadOnlyDictionary<string, string[]>? DetailsRefusalOf(INoteContent content)
    {
        if (content.CommonItemDetails is not { Count: > 0 } details)
        {
            return null;
        }

        if (!details.Keys.ToHashSet().IsSubsetOf(
            CommonItems.Where(item => item.NeedsDetail is true).Select(item => item.SourceId)))
        {
            return DetailsForAnotherItem;
        }

        return details.Values.Any(detail => Cleaned(detail).Length > Limits.ItemDetail) ? DetailTooLong : null;
    }

    private IReadOnlyDictionary<string, string[]>? MedicationRefusalOf(INoteContent content) =>
        !HasMedicationLine && !string.IsNullOrWhiteSpace(content.Medication) ? MedicationWithoutALine : null;

    // The cleaned detail of a ticked common item that asks for one, or null for none, blank included.
    private static string? DetailOf(DraftItem item, INoteContent content) =>
        item.NeedsDetail is true && Cleaned(content.CommonItemDetails?.GetValueOrDefault(item.SourceId)) is
            { Length: > 0 } cleaned
            ? cleaned
            : null;

    // A JSON null can reach a value of the map, so it counts as blank.
    private static string Cleaned(string? detail) => detail is null ? "" : ItemText.Cleaned(detail);
}
