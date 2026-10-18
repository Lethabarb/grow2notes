using System.Text.Json.Nodes;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Grow2Notes.Web.Platform.Audit;
using Grow2Notes.Web.Platform.Configuration;
using Microsoft.EntityFrameworkCore;

namespace Grow2Notes.Web.Features.CommonItems;

/// <summary>
/// The handler that changes a group's common items (design.md §6.6), as a handler class that takes the
/// <see cref="Actor"/> (mcp-server.md §3.5, convention 1): <c>POST /api/admin/common-item-groups/{groupId}/items</c>
/// calls it with <see cref="Actor.InApp"/>, and Release 2's tools will call it with <see cref="Channel.Mcp"/>. Each
/// change is saved with its audit event through <see cref="ConfigurationChanges"/>, in the tenant's organisation.
/// </summary>
internal sealed class CommonItemChanges(
    Grow2NotesDbContext db,
    ITenantContext tenant,
    ConfigurationChanges changes,
    TimeProvider timeProvider)
{
    /// <summary>
    /// Adds a common item to a group of the tenant's organisation, found through the tenant filter, so that another
    /// organisation's is not found (design.md §9.2); refuses an archived group, which is restored first; then reads
    /// the text that <see cref="ItemText.CommonItem"/> reads from <paramref name="request"/>, puts the item at the end
    /// of the group's items (<see cref="ListOrder.End"/>, A2), and saves it with <c>common_item.created</c>, whose
    /// entity is the new item, which names no participant, and whose <c>Details</c> hold its group and its values as
    /// <c>new</c> alone (T06's Notes). Every refusal adds nothing and writes no event.
    /// </summary>
    /// <remarks>
    /// The group is read just before the save, and carries no token that the save checks (design.md §6.9), so a group
    /// archived in that instant is not caught (T06's Notes).
    /// </remarks>
    /// <returns>
    /// <see cref="CommonItemChangeResult.Saved"/>; <see cref="CommonItemChangeResult.NotFound"/>, for a group not in
    /// the organisation; <see cref="ErrorCode.GroupArchived"/>, for an archived group; or the text's message, checked
    /// in that order, as the goals' add finds the participant before it reads the text.
    /// </returns>
    public async Task<CommonItemChangeResult> AddAsync(
        Actor actor, Guid groupId, CommonItemTextRequest request, CancellationToken cancellationToken)
    {
        var group = await db.CommonItemGroups.AsNoTracking()
            .SingleOrDefaultAsync(g => g.Id == groupId, cancellationToken);
        if (group is null)
        {
            return CommonItemChangeResult.NotFound;
        }

        if (group.ArchivedAtUtc is not null)
        {
            return CommonItemChangeResult.Refused(ErrorCode.GroupArchived);
        }

        if (!ItemText.CommonItem.TryRead(request.Text, out var text, out var errors))
        {
            return CommonItemChangeResult.Invalid(errors);
        }

        // Two adds at once can read the same end and give their items the same SortOrder, which the list's order
        // breaks by CreatedAtUtc, then Id (ListOrder).
        var activeItems = await db.CommonItems.AsNoTracking()
            .Where(item => item.GroupId == groupId && item.ArchivedAtUtc == null)
            .ToListAsync(cancellationToken);
        var item = new CommonItem
        {
            OrganisationId = tenant.OrganisationId,
            GroupId = groupId,
            Text = text,
            SortOrder = ListOrder.End(activeItems),
            CreatedAtUtc = timeProvider.GetUtcNow().UtcDateTime,
        };
        db.CommonItems.Add(item);

        // A new row has no version that could have changed since it was loaded, so the save is done or it throws.
        await changes.SaveAsync(
            actor,
            AuditEventTypes.CommonItem.Created,
            AuditEntityTypes.CommonItem,
            item.Id,
            participantId: null,
            before: null,
            Values(item),
            cancellationToken: cancellationToken);
        return CommonItemChangeResult.Saved;
    }

    /// <summary>
    /// An item's values as an audit event records them, named and written as design.md §6.6's list gives them, never
    /// <c>id</c> or <c>rowVersion</c> (<see cref="ChangeDetails"/>): its <c>groupId</c>, named as S01.03.08's Note
    /// names it, so that the event says which group; and <c>archivedAtUtc</c> as stored, or JSON <c>null</c>.
    /// </summary>
    private static JsonObject Values(CommonItem item) => new()
    {
        ["groupId"] = item.GroupId,
        ["text"] = item.Text,
        ["sortOrder"] = item.SortOrder,
        ["archivedAtUtc"] = item.ArchivedAtUtc,
    };
}
