using System.Text.Json.Nodes;
using Grow2Notes.Web.Platform.Audit;
using Microsoft.EntityFrameworkCore;

namespace Grow2Notes.Web.Platform.Configuration;

/// <summary>
/// The one save of a change to a configuration row, with its audit event (design.md §5.4, §6.6, A28, D84), one per
/// request or command scope. The handler loads the row through the tenant filter, checks its version with
/// <see cref="IfMatch.Check"/> and changes it on the scope's context; this then saves the change and its event through
/// the audit writer's one <c>SaveChanges</c>, so they commit or fail together without <c>InTransactionAsync</c>, which
/// design.md §5.9 keeps for work of several saves.
/// </summary>
internal sealed class ConfigurationChanges(ITenantContext tenant, IAuditWriter audit)
{
    /// <summary>
    /// Saves the change the scope's context is tracking, with its event in the tenant's organisation and the actor as
    /// who acted, its <c>Details</c> those of <see cref="ChangeDetails.Of"/>; or, when no value changed, saves nothing.
    /// </summary>
    /// <param name="actor">Who made the change.</param>
    /// <param name="eventType">One of the <see cref="AuditEventTypes"/> constants.</param>
    /// <param name="entityType">
    /// One of the <see cref="AuditEntityTypes"/> constants: the row's, or the reordered rows'.
    /// </param>
    /// <param name="entityId">The row's ID, or <see langword="null"/> for a reorder, which is about a list.</param>
    /// <param name="participantId">The participant, for a participant's or a goal's event.</param>
    /// <param name="before">The row's values as it was loaded, or <see langword="null"/> for a new row.</param>
    /// <param name="after">The row's values as it is to be saved.</param>
    /// <param name="extraDetails">Other keys for <c>Details</c>, beside the old and new values.</param>
    /// <param name="refuseIfChanged">
    /// The code to refuse the change with when it would change a value, or <see langword="null"/> (see
    /// <see cref="VersionCheck.RefuseIfChanged"/>). A change that changes nothing is
    /// <see cref="ConfigurationSaveResult.Unchanged"/> whatever it holds.
    /// </param>
    /// <param name="cancellationToken">Cancels the save.</param>
    /// <returns>
    /// <see cref="ConfigurationSaveResult.Saved"/>; <see cref="ConfigurationSaveResult.Unchanged"/> when no value
    /// changed; <see cref="ConfigurationSaveResult.Refused"/> when one would and <paramref name="refuseIfChanged"/>
    /// holds a code, with nothing written; or <see cref="ConfigurationSaveResult.PreconditionFailed"/> when the save
    /// throws <see cref="DbUpdateConcurrencyException"/>, because the row changed after it was loaded or was made up
    /// with another organisation's key (A48), and nothing was written.
    /// </returns>
    public async Task<ConfigurationSaveResult> SaveAsync(
        Actor actor,
        string eventType,
        string entityType,
        Guid? entityId,
        Guid? participantId,
        JsonObject? before,
        JsonObject after,
        JsonObject? extraDetails = null,
        ErrorCode? refuseIfChanged = null,
        CancellationToken cancellationToken = default)
    {
        if (ChangeDetails.Of(before, after, extraDetails) is not { } details)
        {
            return ConfigurationSaveResult.Unchanged;
        }

        if (refuseIfChanged is { } refusal)
        {
            return ConfigurationSaveResult.Refused(refusal);
        }

        try
        {
            await audit.WriteAsync(
                tenant.OrganisationId, actor.UserId, eventType, entityType, entityId, participantId, details,
                cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // The save wrote nothing, and the writer has let go of the event. The change stays tracked with the
            // concurrency tokens it was loaded with, so a later save in this scope fails the same way and writes none.
            return ConfigurationSaveResult.PreconditionFailed;
        }

        return ConfigurationSaveResult.Saved;
    }
}
