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
/// <c>PUT /api/admin/common-items/{id}</c>, <c>PUT /api/admin/common-item-groups/{groupId}/items/order</c> and
/// <c>POST /api/admin/common-items/{id}/archive</c> and <c>/restore</c> call it with <see cref="Actor.InApp"/>, and
/// the item tools of the assistant access call it with <see cref="Channel.Mcp"/>. Each change is saved with its audit
/// event through <see cref="ConfigurationChanges"/>, in the tenant's organisation.
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
    /// <see cref="CommonItemChangeResult.Added"/>, with the new item's ID;
    /// <see cref="CommonItemChangeResult.NotFound"/>, for a group not in the organisation;
    /// <see cref="ErrorCode.GroupArchived"/>, for an archived group; or the text's message, checked in that order, as
    /// the goals' add finds the participant before it reads the text.
    /// </returns>
    public async Task<CommonItemChangeResult> AddAsync(
        Actor actor, Guid groupId, CommonItemTextRequest request, CancellationToken cancellationToken)
    {
        if (await RefusalOfGroupAsync(groupId, cancellationToken) is { } refused)
        {
            return refused;
        }

        if (!ItemText.CommonItem.TryRead(request.Text, out var text, out var errors))
        {
            return CommonItemChangeResult.Invalid(errors);
        }

        // Two adds at once can read the same end and give their items the same SortOrder, which the list's order
        // breaks by CreatedAtUtc, then Id (ListOrder).
        var item = new CommonItem
        {
            OrganisationId = tenant.OrganisationId,
            GroupId = groupId,
            Text = text,
            SortOrder = ListOrder.End(await ActiveItemsAsync(groupId, cancellationToken)),
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
        return CommonItemChangeResult.Added(item.Id);
    }

    /// <summary>
    /// Rewords a common item of the tenant's organisation, and moves it to another group of the organisation when
    /// <paramref name="request"/> names one (S01.03.02, S01.03.08): loads it through the tenant filter, so that another
    /// organisation's is not found (design.md §9.2); checks <paramref name="ifMatch"/> against its version as the
    /// channel orders it (<see cref="IfMatch.CheckFor"/>); refuses an item whose own group is archived, which is
    /// restored first; finds the chosen group through the tenant filter too, as the add finds its group, and refuses
    /// an archived one; reads the text from <paramref name="request"/> by the add's rules
    /// (<see cref="ItemText.CommonItem"/>); then puts a moved item at the end of the chosen group's active items
    /// (<see cref="ListOrder.End"/>, A42), keeping its archived state, and saves it with
    /// <c>common_item.updated</c>, which names no participant, and whose <c>Details</c> hold the old and new values of
    /// those of its <c>groupId</c>, <c>sortOrder</c> and <c>text</c> that changed (S01.03.08's Note); or, when none
    /// changed, saves nothing and writes no event, and answers <see cref="CommonItemChangeResult.Changed"/>
    /// <see langword="false"/> (D84). A missing or <c>null</c> <c>groupId</c>, or the item's own, keeps it in its place
    /// in its group. Any active group can be chosen, Every note included. An archived item of an active group is
    /// reworded and moved too, as design.md §6.6 names no refusal, though the screen offers Edit on active items alone
    /// (T06's and T07's Notes). <see cref="Channel.Mcp"/> may send no text, which keeps the item's own
    /// (<see cref="CommonItemUpdateRequest.OrStored"/>); the app's request without one is refused.
    /// </summary>
    /// <remarks>
    /// <see cref="Channel.App"/> has the version checked first, so that a missing or stale <c>If-Match</c> is refused
    /// whatever follows. <see cref="Channel.Mcp"/> has it checked last, by the save, so that a call that changes
    /// nothing is not refused for its version, while the groups' refusals and the text's rules come first (A134).
    ///
    /// The groups are read just before the save, and carry no token that the save checks (design.md §6.9), so a group
    /// archived in that instant is not caught (T06's Notes); and two items put at the end of one group at once can read
    /// the same end, as two adds can (<see cref="AddAsync"/>).
    /// </remarks>
    /// <param name="actor">Who rewords or moves it.</param>
    /// <param name="itemId">The item.</param>
    /// <param name="ifMatch">
    /// The request's <c>If-Match</c> values, as <c>Headers.IfMatch</c> gives them, or Release 2's
    /// <c>expectedVersion</c> through <see cref="IfMatch.FromExpectedVersion"/>.
    /// </param>
    /// <param name="request">The text as sent, and the group to put the item in.</param>
    /// <param name="cancellationToken">Cancels the change.</param>
    /// <returns>
    /// A result with <see cref="CommonItemChangeResult.Changed"/> true or false and no error, whether a value changed
    /// or not; or, each with nothing written, checked in this order: <see cref="CommonItemChangeResult.NotFound"/>,
    /// for an item not in the organisation; <see cref="ErrorCode.GroupArchived"/>, for an item whose group is archived;
    /// <see cref="CommonItemChangeResult.NotFound"/>, for a chosen group not in the organisation;
    /// <see cref="ErrorCode.GroupArchived"/>, for an archived chosen group; the text's message; and the code that
    /// <see cref="IfMatch.Check"/> gives or <see cref="ErrorCode.PreconditionFailed"/>, when the item changed after it
    /// was loaded. For <see cref="Channel.App"/> the version's code comes right after the first not found.
    /// </returns>
    public Task<CommonItemChangeResult> UpdateAsync(
        Actor actor,
        Guid itemId,
        StringValues ifMatch,
        CommonItemUpdateRequest request,
        CancellationToken cancellationToken) =>
        ChangeAsync(
            actor,
            itemId,
            ifMatch,
            async (item, refuseIfChanged) =>
            {
                if (await db.CommonItemGroups.AnyAsync(
                        group => group.Id == item.GroupId && group.ArchivedAtUtc != null, cancellationToken))
                {
                    return CommonItemChangeResult.Refused(ErrorCode.GroupArchived);
                }

                var sent = actor.Channel == Channel.Mcp ? request.OrStored(item) : request;
                var groupId = sent.GroupId ?? item.GroupId;
                var moved = groupId != item.GroupId;
                if (moved && await RefusalOfGroupAsync(groupId, cancellationToken) is { } refused)
                {
                    return refused;
                }

                if (!ItemText.CommonItem.TryRead(sent.Text, out var text, out var errors))
                {
                    return CommonItemChangeResult.Invalid(errors);
                }

                var before = UpdateValues(item);
                item.Text = text;
                if (moved)
                {
                    item.SortOrder = ListOrder.End(await ActiveItemsAsync(groupId, cancellationToken));
                    item.GroupId = groupId;
                }

                return await SaveAsync(
                    actor,
                    AuditEventTypes.CommonItem.Updated,
                    item,
                    before,
                    UpdateValues(item),
                    refuseIfChanged,
                    cancellationToken);
            },
            cancellationToken);

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
    /// A result with <see cref="CommonItemChangeResult.Changed"/> true or false and no error, whether the order changed
    /// or not; <see cref="CommonItemChangeResult.NotFound"/>, for a group not in the organisation; or
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
            cancellationToken: cancellationToken);
        return saved.Error is null
            ? CommonItemChangeResult.Of(saved)
            : CommonItemChangeResult.Refused(ErrorCode.OrderMismatch);
    }

    /// <summary>
    /// Archives a common item of the tenant's organisation (design.md §3.7, S01.03.04), once <paramref name="ifMatch"/>
    /// is its version, as <see cref="UpdateAsync"/> checks it in both channels' orders: sets <c>ArchivedAtUtc</c>
    /// through <see cref="Archiving.Archive"/>, which leaves its <c>SortOrder</c>, and saves it with
    /// <c>common_item.archived</c>, which names no participant. An item archived already keeps the time it was archived
    /// at, so nothing is saved and no event written, and the answer is <see cref="CommonItemChangeResult.Changed"/>
    /// <see langword="false"/> (D84). An archived group's item is archived as an active group's is, as design.md §6.6
    /// gives the archive no <c>config.group_archived</c> (T06's Notes).
    /// </summary>
    /// <returns>
    /// A result with <see cref="CommonItemChangeResult.Changed"/> true or false and no error, whether the item changed
    /// or not; or, with nothing written, <see cref="CommonItemChangeResult.NotFound"/>, for an item not in the
    /// organisation, the code that <see cref="IfMatch.Check"/> gives, or <see cref="ErrorCode.PreconditionFailed"/>
    /// when the item changed after it was loaded. For <see cref="Channel.Mcp"/> the version's code is given only when
    /// the item would change.
    /// </returns>
    public Task<CommonItemChangeResult> ArchiveAsync(
        Actor actor, Guid itemId, StringValues ifMatch, CancellationToken cancellationToken) =>
        ChangeAsync(
            actor,
            itemId,
            ifMatch,
            (item, refuseIfChanged) =>
            {
                var before = PlaceValues(item);
                Archiving.Archive(item, timeProvider);
                return SaveAsync(
                    actor,
                    AuditEventTypes.CommonItem.Archived,
                    item,
                    before,
                    PlaceValues(item),
                    refuseIfChanged,
                    cancellationToken);
            },
            cancellationToken);

    /// <summary>
    /// Restores a common item of the tenant's organisation (design.md §3.7, S01.03.04), once <paramref name="ifMatch"/>
    /// is its version, as <see cref="UpdateAsync"/> checks it: puts it at the end of its own group's active items
    /// through <see cref="Archiving.Restore"/> (A2), whatever other groups hold, and saves it with
    /// <c>common_item.restored</c>, which names no participant. An active item stays where it is, so nothing is saved
    /// and no event written, and the answer is <see cref="CommonItemChangeResult.Changed"/>
    /// <see langword="false"/> (D84). An archived group's item is restored as an active group's is, as design.md §6.6
    /// gives the restore no <c>config.group_archived</c> (T06's Notes).
    /// </summary>
    /// <returns>As <see cref="ArchiveAsync"/>'s.</returns>
    public Task<CommonItemChangeResult> RestoreAsync(
        Actor actor, Guid itemId, StringValues ifMatch, CancellationToken cancellationToken) =>
        ChangeAsync(
            actor,
            itemId,
            ifMatch,
            async (item, refuseIfChanged) =>
            {
                var before = PlaceValues(item);
                Archiving.Restore(item, await ActiveItemsAsync(item.GroupId, cancellationToken));
                return await SaveAsync(
                    actor,
                    AuditEventTypes.CommonItem.Restored,
                    item,
                    before,
                    PlaceValues(item),
                    refuseIfChanged,
                    cancellationToken);
            },
            cancellationToken);

    /// <summary>
    /// Loads the item through the tenant filter, so that another organisation's is not found (design.md §9.2), and
    /// checks <paramref name="ifMatch"/> against its version (<see cref="IfMatch.CheckFor"/>), which
    /// <see cref="Channel.App"/> has refused at once, before <paramref name="change"/> changes and saves it, with the
    /// refusal to pass on to the save when the channel holds it back.
    /// </summary>
    private async Task<CommonItemChangeResult> ChangeAsync(
        Actor actor,
        Guid itemId,
        StringValues ifMatch,
        Func<CommonItem, ErrorCode?, Task<CommonItemChangeResult>> change,
        CancellationToken cancellationToken)
    {
        var item = await db.CommonItems.SingleOrDefaultAsync(i => i.Id == itemId, cancellationToken);
        if (item is null)
        {
            return CommonItemChangeResult.NotFound;
        }

        var version = IfMatch.CheckFor(actor, ifMatch, RowVersionToken.Of(item));
        return version.RefuseNow is { } refused
            ? CommonItemChangeResult.Refused(refused)
            : await change(item, version.RefuseIfChanged);
    }

    /// <summary>
    /// What refuses a group that an item is added to or moved into, found through the tenant filter, so that another
    /// organisation's is not found (design.md §9.2): <see cref="CommonItemChangeResult.NotFound"/>, for a group not in
    /// the organisation, or <see cref="ErrorCode.GroupArchived"/>, for an archived one, which is restored first; or
    /// <see langword="null"/> for an active group of the organisation.
    /// </summary>
    private async Task<CommonItemChangeResult?> RefusalOfGroupAsync(Guid groupId, CancellationToken cancellationToken)
    {
        var group = await db.CommonItemGroups.AsNoTracking()
            .SingleOrDefaultAsync(g => g.Id == groupId, cancellationToken);
        if (group is null)
        {
            return CommonItemChangeResult.NotFound;
        }

        return group.ArchivedAtUtc is null ? null : CommonItemChangeResult.Refused(ErrorCode.GroupArchived);
    }

    /// <summary>
    /// The active items of the group of ID <paramref name="groupId"/>, untracked and in no order, after which an item
    /// added, moved or restored to the group goes (<see cref="ListOrder.End"/>).
    /// </summary>
    private Task<List<CommonItem>> ActiveItemsAsync(Guid groupId, CancellationToken cancellationToken) =>
        db.CommonItems.AsNoTracking()
            .Where(item => item.GroupId == groupId && item.ArchivedAtUtc == null)
            .ToListAsync(cancellationToken);

    // A change to a loaded item, with its event, whose entity is the item, which names no participant, and whose
    // Details hold the values given before and after, those that changed alone; or nothing when none changed.
    private async Task<CommonItemChangeResult> SaveAsync(
        Actor actor,
        string eventType,
        CommonItem item,
        JsonObject before,
        JsonObject after,
        ErrorCode? refuseIfChanged,
        CancellationToken cancellationToken) =>
        CommonItemChangeResult.Of(await changes.SaveAsync(
            actor,
            eventType,
            AuditEntityTypes.CommonItem,
            item.Id,
            participantId: null,
            before,
            after,
            refuseIfChanged: refuseIfChanged,
            cancellationToken: cancellationToken));

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
    /// The values that an update changes, a move's group and place in it and a reword's text, as
    /// <c>common_item.updated</c> records those of them that changed, named as <see cref="Values"/> names them.
    /// </summary>
    private static JsonObject UpdateValues(CommonItem item) => new()
    {
        ["groupId"] = item.GroupId,
        ["text"] = item.Text,
        ["sortOrder"] = item.SortOrder,
    };

    /// <summary>
    /// The item's place in its group, the values that archiving and restoring change, as their events record them,
    /// named as <see cref="Values"/> names them.
    /// </summary>
    private static JsonObject PlaceValues(CommonItem item) => new()
    {
        ["sortOrder"] = item.SortOrder,
        ["archivedAtUtc"] = item.ArchivedAtUtc,
    };
}
