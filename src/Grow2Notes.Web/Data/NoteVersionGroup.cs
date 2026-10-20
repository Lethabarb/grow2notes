namespace Grow2Notes.Web.Data;

/// <summary>
/// A common item group in a <see cref="NoteVersion"/> (design.md §5.3), with its name as the writer saw it, picked or
/// not. A version holds every group of its snapshot, Every note first, so the common items in
/// <see cref="NoteVersionItem"/> name their group by its <see cref="Position"/>. A row is only ever added (§5.8).
/// </summary>
internal sealed class NoteVersionGroup : ITenantOwned, IAppendOnly
{
    public required Guid NoteVersionId { get; set; }

    /// <summary>The 0-based place in the version's groups; Every note is first.</summary>
    public required int Position { get; set; }

    /// <summary>
    /// Given by the code that saves the version, the tenant's, and not left to the save to stamp: it is in every
    /// foreign key.
    /// </summary>
    public required Guid OrganisationId { get; set; }

    public required Guid CommonItemGroupId { get; set; }

    /// <summary>The group's name when the draft's snapshot was made.</summary>
    public required string Name { get; set; }

    public bool IsEveryNote { get; set; }

    /// <summary>Never set for Every note, which a note always shows (D47).</summary>
    public bool IsPicked { get; set; }
}
