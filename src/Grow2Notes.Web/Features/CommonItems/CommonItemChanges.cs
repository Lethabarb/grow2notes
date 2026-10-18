using System.Text.Json.Nodes;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Grow2Notes.Web.Platform.Audit;
using Grow2Notes.Web.Platform.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;

namespace Grow2Notes.Web.Features.CommonItems;

/// <summary>
/// The handler that changes a group's common items (design.md §6.6), as a handler class that takes the
/// <see cref="Actor"/> (mcp-server.md §3.5, convention 1): <c>POST /api/admin/common-item-groups/{groupId}/items</c>,
/// <c>PUT /api/admin/common-items/{id}</c> and <c>PUT /api/admin/common-item-groups/{groupId}/items/order</c> call it
/// with <see cref="Actor.InApp"/>, and Release 2's tools will call it with <see cref="Channel.Mcp"/>. Each change is
/// saved with its audit event through <see cref="ConfigurationChanges"/>, in the tenant's organisation.
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
    /// Rewords a common item of the tenant's organisation in its own group (S01.03.02), as T07 adds the move to another
    /// group to the same request (T06's Notes): loads it through the tenant filter, so that another organisation's is
    /// not found (design.md §9.2); checks <paramref name="ifMatch"/> against its version; refuses an item whose group
    /// is archived, which is restored first; reads the text from <paramref name="request"/> by the add's rules
    /// (<see cref="ItemText.CommonItem"/>); then saves it with <c>common_item.updated</c>, whose <c>Details</c> hold
    /// the old and new <c>text</c>, and which names no participant; or, when the text is already stored, saves nothing
    /// and writes no event, and answers as a save does (D84). An archived item of an active group is reworded too, as
    /// design.md §6.6 names no refusal, though the screen offers Edit on active items alone (T06's Notes).
    /// </summary>
    /// <remarks>
    /// The group is read just before the save, and carries no token that the save checks (design.md §6.9), so a group
    /// archived in that instant is not caught (T06's Notes).
    /// </remarks>
    /// <param name="actor">Who rewords it.</param>
    /// <param name="itemId">The item.</param>
    /// <param name="ifMatch">
    /// The request's <c>If-Match</c> values, as <c>Headers.IfMatch</c> gives them, or Release 2's
    /// <c>expectedVersion</c> through <see cref="IfMatch.ETag"/>.
    /// </param>
    /// <param name="request">The text as sent.</param>
    /// <param name="cancellationToken">Cancels the reword.</param>
    /// <returns>
    /// <see cref="CommonItemChangeResult.Saved"/>, whether the text changed or not; or, each with nothing written,
    /// checked in this order: <see cref="CommonItemChangeResult.NotFound"/>, for an item not in the organisation; the
    /// code that <see cref="IfMatch.Check"/> gives; <see cref="ErrorCode.GroupArchived"/>, for an item whose group is
    /// archived; the text's message; and <see cref="ErrorCode.PreconditionFailed"/>, when the item changed after it
    /// was loaded.
    /// </returns>
    public async Task<CommonItemChangeResult> UpdateAsync(
        Actor actor,
        Guid itemId,
        StringValues ifMatch,
        CommonItemTextRequest request,
        CancellationToken cancellationToken)
    {
        var item = await db.CommonItems.SingleOrDefaultAsync(i => i.Id == itemId, cancellationToken);
        if (item is null)
        {
            return CommonItemChangeResult.NotFound;
        }

        if (IfMatch.Check(ifMatch, RowVersionToken.Of(item)) is { } refused)
        {
            return CommonItemChangeResult.Refused(refused);
        }

        if (await db.CommonItemGroups.AnyAsync(
                group => group.Id == item.GroupId && group.ArchivedAtUtc != null, cancellationToken))
        {
            return CommonItemChangeResult.Refused(ErrorCode.GroupArchived);
        }

        if (!ItemText.CommonItem.TryRead(request.Text, out var text, out var errors))
        {
            return CommonItemChangeResult.Invalid(errors);
        }

        var before = TextValues(item);
        item.Text = text;
        var saved = await changes.SaveAsync(
            actor,
            AuditEventTypes.CommonItem.Updated,
            AuditEntityTypes.CommonItem,
            item.Id,
            participantId: null,
            before,
            TextValues(item),
            cancellationToken: cancellationToken);
        return saved.Error is { } error ? CommonItemChangeResult.Refused(error) : CommonItemChangeResult.Saved;
    }

    /// <summary>
    /// Puts the active items of a group of the tenant's organisation, found through the tenant filter, so that another
    /// organisation's is not found (design.md §9.2), in the order of <paramref name="commonItemIds"/> (S01.03.03):
    /// reads them in the list's order (<see cref="CommonItemGroupList.InListOrder(IQueryable{CommonItem})"/>), the
    /// archived ones left out, so that <see cref="ListOrder.Reorder"/> refuses an archived ID as one it was not given;
    /// then saves the new order with <c>common_item.reordered</c>, which names no item as its entity and no
    /// participant, and whose <c>Details</c> hold the old and new order with the <c>groupId</c> beside them
    /// (S01.03.03's Note); or, for the order already stored, saves nothing and writes no event (D84). An archived
    /// group's items are reordered as an active one's are, as design.md §6.6 gives the reorder no
    /// <c>config.group_archived</c> (T06's Notes).
    /// </summary>
    /// <param name="actor">Who reorders them.</param>
    /// <param name="groupId">The group.</param>
    /// <param name="commonItemIds">The IDs of the group's active items, in their new order.</param>
    /// <param name="cancellationToken">Cancels the reorder.</param>
    /// <returns>
    /// <see cref="CommonItemChangeResult.Saved"/>, whether the order changed or not;
    /// <see cref="CommonItemChangeResult.NotFound"/>, for a group not in the organisation; or
    /// <see cref="ErrorCode.OrderMismatch"/>, with nothing written, when the IDs are not the group's active items, each
    /// once, or when an item changed after the list was read, which the save finds as
    /// <see cref="ErrorCode.PreconditionFailed"/>: a reorder sends no <c>If-Match</c>, and the screen reads the list
    /// again for either (list-editor.md <i>Request errors</i>), so both are the one code.
    /// </returns>
    public async Task<CommonItemChangeResult> ReorderAsync(
        Actor actor, Guid groupId, IReadOnlyList<Guid> commonItemIds, CancellationToken cancellationToken)
    {
        if (!await db.CommonItemGroups.AnyAsync(group => group.Id == groupId, cancellationToken))
        {
            return CommonItemChangeResult.NotFound;
        }

        // Tracked, so that the save writes the SortOrder that each moved item is given, checking the version it was
        // read at.
        var activeItems = await CommonItemGroupList.InListOrder(
                db.CommonItems.Where(item => item.GroupId == groupId && item.ArchivedAtUtc == null))
            .ToListAsync(cancellationToken);
        var reordered = ListOrder.Reorder(activeItems, commonItemIds);
        if (reordered.Error is { } refused)
        {
            return CommonItemChangeResult.Refused(refused);
        }

        var saved = await changes.SaveAsync(
            actor,
            AuditEventTypes.CommonItem.Reordered,
            AuditEntityTypes.CommonItem,
            entityId: null,
            participantId: null,
            reordered.Before,
            reordered.After,
            new JsonObject { ["groupId"] = groupId },
            cancellationToken);
        return saved.Error is null
            ? CommonItemChangeResult.Saved
            : CommonItemChangeResult.Refused(ErrorCode.OrderMismatch);
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

    /// <summary>
    /// The one value that a reword changes, as <c>common_item.updated</c> records it, named as <see cref="Values"/>
    /// names it.
    /// </summary>
    private static JsonObject TextValues(CommonItem item) => new() { ["text"] = item.Text };
}
