using System.Text.Json.Nodes;
using Grow2Notes.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace Grow2Notes.Web.Platform.Audit;

/// <summary>
/// The app's <see cref="IAuditWriter"/>, one per request or command scope, so it saves through that scope's context and
/// its transaction. It logs nothing: <c>Details</c> reach the database and nowhere else, because the audit log is a
/// table, not telemetry (design.md §9.5). A failed save reaches telemetry as any database failure does, with the
/// exception type and SQL error number only.
/// </summary>
internal sealed class AuditWriter(
    Grow2NotesDbContext db, TimeProvider timeProvider, IHttpContextAccessor httpContextAccessor) : IAuditWriter
{
    public async Task WriteAsync(
        Guid organisationId,
        Guid? actorUserId,
        string eventType,
        string? entityType,
        Guid? entityId,
        Guid? participantId,
        JsonObject? details,
        CancellationToken cancellationToken = default)
    {
        var entry = db.AuditEvents.Add(new AuditEvent
        {
            OrganisationId = organisationId,
            OccurredAtUtc = timeProvider.GetUtcNow().UtcDateTime,
            ActorUserId = actorUserId,
            EventType = eventType,
            EntityType = entityType,
            EntityId = entityId,
            ParticipantId = participantId,
            Details = details?.ToJsonString(),
            IpAddress = ClientAddress(),
        });

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            // Saved or refused, the event leaves the change tracker: a refused one must not be written by the caller's
            // next save, and nothing reads a saved one back through this context.
            entry.State = EntityState.Detached;
        }
    }

    // A dual-stack socket gives an IPv4 client's address as IPv4-mapped IPv6 (::ffff:203.0.113.7). Stored as IPv4, one
    // client's address has one form, whichever socket it came through.
    private string? ClientAddress()
    {
        var address = httpContextAccessor.HttpContext?.Connection.RemoteIpAddress;
        return (address is { IsIPv4MappedToIPv6: true } ? address.MapToIPv4() : address)?.ToString();
    }
}
