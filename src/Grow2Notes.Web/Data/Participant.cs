using Grow2Notes.Web.Platform.Configuration;

namespace Grow2Notes.Web.Data;

/// <summary>
/// A person the organisation supports, whom notes are written for (design.md §5.3). Managers add, correct, archive and
/// restore them; nothing deletes one (A29). No NDIS number or other government identifier is stored (A5, §12).
/// </summary>
internal sealed class Participant : ITenantOwned, IVersionedRow, IArchivableRow
{
    public Guid Id { get; set; }

    /// <summary>
    /// Given by the code that adds the participant, the tenant's, and not left to the save to stamp: it is in the
    /// alternate key and the foreign key to <see cref="Organisation"/>, so EF Core gives an empty one a new GUID as the
    /// participant is tracked, which the save then refuses as another organisation's.
    /// </summary>
    public required Guid OrganisationId { get; set; }

    /// <summary>As typed, with only its ends trimmed, never re-cased (A5).</summary>
    public required string GivenName { get; set; }

    /// <summary>As typed, with only its ends trimmed, never re-cased (A5).</summary>
    public required string FamilyName { get; set; }

    /// <summary>Needed for the under-25 retention rule (A29), and shown to managers only.</summary>
    public required DateOnly DateOfBirth { get; set; }

    /// <summary>
    /// An archived participant is not on Today and gets no new notes; their notes stay readable (§3.7).
    /// </summary>
    public ParticipantStatus Status { get; set; } = ParticipantStatus.Active;

    /// <summary>Set on archive, cleared on restore.</summary>
    public DateTime? ArchivedAtUtc { get; set; }

    public required DateTime CreatedAtUtc { get; set; }

    /// <summary>
    /// The row's version (<see cref="RowVersionToken"/>), which the list gives as <c>rowVersion</c> for
    /// <c>If-Match</c> (D78). SQL Server sets it on every insert and update, so EF Core never writes it.
    /// </summary>
    public byte[] RowVersion { get; private set; } = [];
}
