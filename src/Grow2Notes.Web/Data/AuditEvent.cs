namespace Grow2Notes.Web.Data;

/// <summary>
/// One row of the audit trail (design.md §5.3, A28): a state change or a download, with who made it, when and from
/// where. The table has no foreign keys, so the trail never blocks, and is never rewritten by, a manual retention task.
/// A row is only ever added (design.md §5.8).
/// </summary>
internal sealed class AuditEvent : ITenantOwned, IAppendOnly
{
    /// <summary>
    /// A <c>bigint</c> identity, design.md §5.1's exception to client-generated GUID keys, so SQL Server sets it as the
    /// row is inserted.
    /// </summary>
    public long Id { get; private set; }

    public required Guid OrganisationId { get; set; }

    public required DateTime OccurredAtUtc { get; set; }

    /// <summary>Null for an operator command.</summary>
    public Guid? ActorUserId { get; set; }

    /// <summary>One of design.md §5.4's event types.</summary>
    public required string EventType { get; set; }

    /// <summary>The kind of row the event is about, such as <c>Note</c> or <c>User</c> (design.md §5.3).</summary>
    public string? EntityType { get; set; }

    public Guid? EntityId { get; set; }

    /// <summary>
    /// Set on every participant-related event, so "what was done to this person's records" is one indexed query.
    /// </summary>
    public Guid? ParticipantId { get; set; }

    /// <summary>
    /// JSON metadata only: old and new values for a configuration change, version numbers for a note event, and never
    /// note text.
    /// </summary>
    public string? Details { get; set; }

    /// <summary>The client's IP address, read through forwarded headers; null outside a request.</summary>
    public string? IpAddress { get; set; }
}
