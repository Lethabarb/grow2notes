using Grow2Notes.Web.Data;

namespace Grow2Notes.Web.Features.Notes;

/// <summary>
/// The lists that a note holds as they were when it was started (design.md §5.3, §5.7, A3): its common item groups,
/// Every note first, and its goals and common items, goals first, with the picks and ticks. It holds every rule about
/// them, whether it is a template or a working copy's snapshot, so that starting a draft, saving it, submitting it and
/// reading it apply the same rules.
/// </summary>
/// <remarks>
/// A snapshot never changes its names, wording, order or membership: <see cref="With"/> sets only the picks and the
/// ticks.
/// </remarks>
internal sealed record NoteSnapshot(IReadOnlyList<DraftGroup> Groups, IReadOnlyList<DraftItem> Items)
{
    // The screen sends only the IDs of what it shows and offers, so these answer only a request made some other way.
    private static readonly IReadOnlyDictionary<string, string[]> TickedOutsideTheGoals =
        new Dictionary<string, string[]>
        {
            [nameof(INoteTicks.TickedGoalIds)] = ["Each ticked goal must be one of the note's goals."],
        };

    private static readonly IReadOnlyDictionary<string, string[]> PickedOutsideTheGroups =
        new Dictionary<string, string[]>
        {
            [nameof(INoteTicks.PickedGroupIds)] = ["Each picked group must be one of the note's groups with items."],
        };

    private static readonly IReadOnlyDictionary<string, string[]> TickedOutsideThePickedGroups =
        new Dictionary<string, string[]>
        {
            [nameof(INoteTicks.TickedCommonItemIds)] =
                ["Each ticked common item must be in Every note or a picked group."],
        };

    public static NoteSnapshot Of(NoteDraft draft) => new(draft.Groups, draft.Items);

    public IEnumerable<DraftItem> Goals => Items.Where(item => item.Section == NoteSection.Goal);

    public IEnumerable<DraftItem> CommonItems => Items.Where(item => item.Section == NoteSection.CommonItem);

    /// <summary>
    /// The groups that a note shows (D47): Every note and the picked groups, each with at least one item, with every
    /// item, ticked or not, in the snapshot's order. A group that is not picked leaves no trace.
    /// </summary>
    public IReadOnlyList<NoteReadGroup> ShownGroups =>
    [
        .. Groups
            .Where(group => group.IsEveryNote || group.IsPicked)
            .Select(group => (group, items: ItemsOf(group).ToList()))
            .Where(shown => shown.items.Count > 0)
            .Select(shown => new NoteReadGroup(
                shown.group.Name,
                shown.group.IsEveryNote,
                [.. shown.items.Select(item => new NoteReadItem(item.Text, item.IsTicked))])),
    ];

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
    /// The refusal of <paramref name="ticks"/>, as a single-key <c>422 validation.failed</c> errors object, or
    /// <c>null</c> if they are valid. The checks run in this order, each list a set: a ticked goal that is not one of
    /// the goals, then a picked ID that is not an offered group (Every note, a group with no item, or an ID outside
    /// the snapshot), then a ticked common item that is not in Every note or a picked group, unknown IDs included.
    /// </summary>
    public IReadOnlyDictionary<string, string[]>? RefusalOf(INoteTicks ticks)
    {
        if (!ticks.TickedGoalIds.ToHashSet().IsSubsetOf(Goals.Select(goal => goal.SourceId)))
        {
            return TickedOutsideTheGoals;
        }

        var picked = ticks.PickedGroupIds.ToHashSet();
        if (!picked.IsSubsetOf(Groups.Where(Offers).Select(group => group.GroupId)))
        {
            return PickedOutsideTheGroups;
        }

        var shown = Groups
            .Where(group => group.IsEveryNote || picked.Contains(group.GroupId))
            .Select(group => group.GroupId)
            .ToHashSet();
        return ticks.TickedCommonItemIds.ToHashSet().IsSubsetOf(
            CommonItems.Where(item => shown.Contains(item.GroupId!.Value)).Select(item => item.SourceId))
            ? null
            : TickedOutsideThePickedGroups;
    }

    /// <summary>
    /// This snapshot with the picks and ticks of <paramref name="ticks"/>, which <see cref="RefusalOf"/> has passed,
    /// so that the items of a group that is not picked stay unticked.
    /// </summary>
    public NoteSnapshot With(INoteTicks ticks)
    {
        var tickedGoals = ticks.TickedGoalIds.ToHashSet();
        var picked = ticks.PickedGroupIds.ToHashSet();
        var tickedItems = ticks.TickedCommonItemIds.ToHashSet();
        return new(
            [.. Groups.Select(group => group with { IsPicked = picked.Contains(group.GroupId) })],
            [
                .. Items.Select(item => item with
                {
                    IsTicked = (item.Section == NoteSection.Goal ? tickedGoals : tickedItems).Contains(item.SourceId),
                }),
            ]);
    }
}
