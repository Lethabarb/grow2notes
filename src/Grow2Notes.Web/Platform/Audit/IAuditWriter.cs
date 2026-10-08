using System.Text.Json.Nodes;

namespace Grow2Notes.Web.Platform.Audit;

/// <summary>
/// Writes the audit event of a state change or a download (design.md §5.3, §5.4, A28). It is write-only, with no read
/// or query method and no <c>AuditEvent</c> in its signature, so code that may write the audit trail but must not read
/// it, such as Release 2's MCP tools, can depend on it (mcp-server.md §6.3 rule 1, §9).
/// </summary>
internal interface IAuditWriter
{
    /// <summary>
    /// Saves one event through the scope's <c>Grow2NotesDbContext</c>, at the time <see cref="TimeProvider"/> gives and
    /// from the client address of the current request, or none outside a request. Inside <c>InTransactionAsync</c> the
    /// save joins the open transaction, so the event commits or rolls back with the change it records (design.md §5.9);
    /// with no transaction open it commits at once, as a download's event must before the file is sent (§5.3). The save
    /// is the context's, so it also writes any other change the context is tracking. Saved or not, the event then
    /// leaves the change tracker, so with no transaction open a failed write is tried again by calling this method
    /// again, not by another <c>SaveChanges</c>, which would leave the event out. Inside <c>InTransactionAsync</c> a
    /// transient failure runs the whole work again, this call included. The event is written at least once: if the
    /// connection drops after the server has committed, the acknowledgement is lost and the retry can store the event
    /// again: with no transaction open always, and inside <c>InTransactionAsync</c> unless the change it records has
    /// its own idempotency guard, such as a version save's <c>Idempotency-Key</c> (design.md §5.9).
    /// </summary>
    /// <param name="organisationId">
    /// The organisation the event belongs to, which must be the tenant: the tenant save interceptor refuses the event
    /// of any other, and nothing is written. Code without a session, such as an operator command, opens
    /// <see cref="ITenantContext.Use"/> for the organisation first.
    /// </param>
    /// <param name="actorUserId">The user who acted, or null for an operator command.</param>
    /// <param name="eventType">One of the <see cref="AuditEventTypes"/> constants.</param>
    /// <param name="entityType">One of the <see cref="AuditEntityTypes"/> constants, or null.</param>
    /// <param name="entityId">The ID of the row the event is about, or null.</param>
    /// <param name="participantId">The participant, set on every participant-related event.</param>
    /// <param name="details">
    /// IDs, and the old and new values of a change, never note text. It is stored as given, so a caller can add any
    /// key, such as Release 2's <c>"via": "mcp"</c> (mcp-server.md §3.5 convention 3).
    /// </param>
    /// <param name="cancellationToken">Cancels the save.</param>
    Task WriteAsync(
        Guid organisationId,
        Guid? actorUserId,
        string eventType,
        string? entityType,
        Guid? entityId,
        Guid? participantId,
        JsonObject? details,
        CancellationToken cancellationToken = default);
}
