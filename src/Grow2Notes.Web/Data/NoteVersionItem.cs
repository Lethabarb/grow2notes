namespace Grow2Notes.Web.Data;

/// <summary>
/// A goal or a common item in a <see cref="NoteVersion"/> (design.md §5.3), ticked or not, with its wording as the
/// writer saw it, so editing, archiving or reordering the lists afterwards never changes a saved note. A version holds
/// every goal and every common item of its snapshot. A row is only ever added (§5.8).
/// </summary>
internal sealed class NoteVersionItem : ITenantOwned, IAppendOnly
{
    public required Guid NoteVersionId { get; set; }

    public required NoteSection Section { get; set; }

    /// <summary>The 0-based place in the section; common items go by group, then by their order in the group.</summary>
    public required int Position { get; set; }

    /// <summary>
    /// Given by the code that saves the version, the tenant's, and not left to the save to stamp: it is in every
    /// foreign key.
    /// </summary>
    public required Guid OrganisationId { get; set; }

    /// <summary>Set for a goal alone.</summary>
    public Guid? GoalId { get; set; }

    /// <summary>Set for a common item alone.</summary>
    public Guid? CommonItemId { get; set; }

    /// <summary>The place of the common item's group in the version's groups; set for a common item alone.</summary>
    public int? GroupPosition { get; set; }

    public required string Text { get; set; }

    /// <summary>Ticked means reached, for a goal, or done, for a common item (D7, D10).</summary>
    public bool IsTicked { get; set; }
}
