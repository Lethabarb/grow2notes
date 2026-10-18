using Grow2Notes.Web.Platform.Configuration;

namespace Grow2Notes.Web.Data;

/// <summary>
/// One of the organisation's groups of common items, the groups in section 2 of a note (design.md §5.3, D44), such as
/// Community outing or Personal care. Any group can be picked on any participant's note. One built-in group, Every
/// note, is always shown first and cannot be unpicked (D45, A43); <see cref="EveryNote"/> makes it. Managers add,
/// rename, reorder, archive and restore the other groups; nothing deletes one (A29). A note keeps the name its draft
/// started with, so a change here reaches only notes started afterwards (A3).
/// </summary>
internal sealed class CommonItemGroup : ITenantOwned, IVersionedRow, IOrderedRow
{
    public Guid Id { get; set; }

    /// <summary>
    /// Given by the code that adds the group, the tenant's, and not left to the save to stamp, as
    /// <see cref="Participant.OrganisationId"/> is: it is in the alternate key and the foreign key to the organisation.
    /// </summary>
    public required Guid OrganisationId { get; set; }

    /// <summary>The current name, which notes started from now on show. Every note's is always "Every note".</summary>
    public required string Name { get; set; }

    /// <summary>
    /// The group's place among the groups other than Every note, which always comes first (<see cref="IOrderedRow"/>).
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// True only for the organisation's one Every note group. Only <see cref="EveryNote"/> sets it, so no endpoint can
    /// make a second one, and the filtered unique index on <c>OrganisationId</c> refuses one all the same.
    /// </summary>
    public bool IsEveryNote { get; private set; }

    /// <summary>
    /// Null while the group is active (<see cref="IArchivableRow"/>). Every note's is always null, which the check
    /// constraint <c>CK_CommonItemGroup_EveryNoteActive</c> holds to.
    /// </summary>
    public DateTime? ArchivedAtUtc { get; set; }

    public required DateTime CreatedAtUtc { get; set; }

    /// <summary>
    /// The row's version (<see cref="RowVersionToken"/>), which the groups list gives as <c>rowVersion</c> for
    /// <c>If-Match</c> (D78). SQL Server sets it on every insert and update, so EF Core never writes it.
    /// </summary>
    public byte[] RowVersion { get; private set; } = [];

    /// <summary>
    /// The organisation's built-in Every note group (A43), named "Every note", active, with <c>SortOrder</c> 0 and no
    /// items yet, which <c>admin bootstrap</c> adds with the organisation (design.md §7.4).
    /// </summary>
    public static CommonItemGroup EveryNote(Guid organisationId, DateTime createdAtUtc) => new()
    {
        OrganisationId = organisationId,
        Name = "Every note",
        IsEveryNote = true,
        CreatedAtUtc = createdAtUtc,
    };
}
