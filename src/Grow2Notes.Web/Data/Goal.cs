using Grow2Notes.Web.Platform.Configuration;

namespace Grow2Notes.Web.Data;

/// <summary>
/// One of a participant's goals, a tick box on their notes (design.md §5.3, D7, D8), in the flat list that managers
/// put in order. Managers add, reword, reorder, archive and restore goals; nothing deletes one (A2, A29). A note keeps
/// the wording and order its draft started with, so a change here reaches only notes started afterwards (A3).
/// </summary>
internal sealed class Goal : ITenantOwned, IVersionedRow, IOrderedRow
{
    public Guid Id { get; set; }

    /// <summary>
    /// Given by the code that adds the goal, the tenant's, and not left to the save to stamp, as
    /// <see cref="Participant.OrganisationId"/> is: it is in the alternate key and the foreign key to the participant.
    /// </summary>
    public required Guid OrganisationId { get; set; }

    public required Guid ParticipantId { get; set; }

    /// <summary>The current wording, which notes started from now on show.</summary>
    public required string Text { get; set; }

    /// <summary>The goal's place in the participant's list (<see cref="IOrderedRow"/>).</summary>
    public int SortOrder { get; set; }

    /// <summary>Null while the goal is active (<see cref="IArchivableRow"/>).</summary>
    public DateTime? ArchivedAtUtc { get; set; }

    public required DateTime CreatedAtUtc { get; set; }

    /// <summary>
    /// The row's version (<see cref="RowVersionToken"/>), which the goals list gives as <c>rowVersion</c> for
    /// <c>If-Match</c> (D78). SQL Server sets it on every insert and update, so EF Core never writes it.
    /// </summary>
    public byte[] RowVersion { get; private set; } = [];
}
