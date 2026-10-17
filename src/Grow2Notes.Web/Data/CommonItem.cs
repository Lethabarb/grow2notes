using Grow2Notes.Web.Platform.Configuration;

namespace Grow2Notes.Web.Data;

/// <summary>
/// One of the organisation's common items, an everyday activity ticked off on notes that is not a goal (design.md
/// §5.3, D9 to D11), in exactly one group's list (A42), which managers put in order. Managers add, reword, reorder,
/// archive and restore items; nothing deletes one (A2, A29). An item shows on a new note only while it and its group
/// are both active, and a note keeps the wording its draft started with (A3).
/// </summary>
internal sealed class CommonItem : ITenantOwned, IVersionedRow, IOrderedRow
{
    public Guid Id { get; set; }

    /// <summary>
    /// Given by the code that adds the item, the tenant's, and not left to the save to stamp, as
    /// <see cref="Participant.OrganisationId"/> is: it is in the alternate key and the foreign key to the group.
    /// </summary>
    public required Guid OrganisationId { get; set; }

    /// <summary>The <see cref="CommonItemGroup"/> whose list the item is in.</summary>
    public required Guid GroupId { get; set; }

    /// <summary>The current wording, which notes started from now on show.</summary>
    public required string Text { get; set; }

    /// <summary>The item's place in its group's list (<see cref="IOrderedRow"/>).</summary>
    public int SortOrder { get; set; }

    /// <summary>Null while the item is active (<see cref="IArchivableRow"/>).</summary>
    public DateTime? ArchivedAtUtc { get; set; }

    public required DateTime CreatedAtUtc { get; set; }

    /// <summary>
    /// The row's version (<see cref="RowVersionToken"/>), which the groups list gives as <c>rowVersion</c> for
    /// <c>If-Match</c> (D78). SQL Server sets it on every insert and update, so EF Core never writes it.
    /// </summary>
    public byte[] RowVersion { get; private set; } = [];
}
