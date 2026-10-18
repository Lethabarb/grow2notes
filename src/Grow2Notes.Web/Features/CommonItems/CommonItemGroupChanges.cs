using System.Text.Json.Nodes;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Grow2Notes.Web.Platform.Audit;
using Grow2Notes.Web.Platform.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;

namespace Grow2Notes.Web.Features.CommonItems;

/// <summary>
/// The handler that changes an organisation's common item groups (design.md §6.6), as a handler class that takes the
/// <see cref="Actor"/> (mcp-server.md §3.5, convention 1): <c>POST /api/admin/common-item-groups</c>,
/// <c>PUT /api/admin/common-item-groups/{groupId}</c>, <c>PUT /api/admin/common-item-groups/order</c> and
/// <c>POST /api/admin/common-item-groups/{groupId}/archive</c> and <c>/restore</c> call it with
/// <see cref="Actor.InApp"/>, and Release 2's tools will call it with <see cref="Channel.Mcp"/>. Each change is saved
/// with its audit event through <see cref="ConfigurationChanges"/>, in the tenant's organisation.
/// </summary>
internal sealed class CommonItemGroupChanges(
    Grow2NotesDbContext db,
    ITenantContext tenant,
    ConfigurationChanges changes,
    TimeProvider timeProvider)
{
    /// <summary>
    /// Adds a group, with no items, to the tenant's organisation, named as <see cref="ItemText.GroupName"/> reads
    /// <paramref name="request"/>; puts it at the end of the active groups other than Every note, which always comes
    /// first (<see cref="ListOrder.End"/>, A2); and saves it with <c>common_item_group.created</c>, whose entity is the
    /// new group, which names no participant, and whose <c>Details</c> hold its values as <c>new</c> alone (T07's
    /// Notes); or, when the name is refused, adds nothing and writes no event. The group is never Every note, which
    /// only <see cref="CommonItemGroup.EveryNote"/> makes, so the organisation keeps its one (S01.03.01).
    /// </summary>
    public async Task<AddCommonItemGroupResult> AddAsync(
        Actor actor, CommonItemGroupNameRequest request, CancellationToken cancellationToken)
    {
        if (!ItemText.GroupName.TryRead(request.Name, out var name, out var errors))
        {
            return new(Guid.Empty, errors);
        }

        // Two adds at once can read the same end and give their groups the same SortOrder, which the list's order
        // breaks by CreatedAtUtc, then Id (ListOrder).
        var activeGroups = await db.CommonItemGroups.AsNoTracking()
            .Where(group => !group.IsEveryNote && group.ArchivedAtUtc == null)
            .ToListAsync(cancellationToken);
        var group = new CommonItemGroup
        {
            OrganisationId = tenant.OrganisationId,
            Name = name,
            SortOrder = ListOrder.End(activeGroups),
            CreatedAtUtc = timeProvider.GetUtcNow().UtcDateTime,
        };
        db.CommonItemGroups.Add(group);

        // A new row has no version that could have changed since it was loaded, so the save is done or it throws.
        await changes.SaveAsync(
            actor,
            AuditEventTypes.CommonItemGroup.Created,
            AuditEntityTypes.CommonItemGroup,
            group.Id,
            participantId: null,
            before: null,
            Values(group),
            cancellationToken: cancellationToken);
        return new(group.Id, Errors: null);
    }

    /// <summary>
    /// Renames a group of the tenant's organisation (S01.03.05): loads it through the tenant filter, so that another
    /// organisation's is not found (design.md §9.2); checks <paramref name="ifMatch"/> against its version; refuses
    /// Every note, which is never renamed (A43); reads the name that <see cref="ItemText.GroupName"/> reads from
    /// <paramref name="request"/>, by the add's rules; then saves it with <c>common_item_group.updated</c>, whose
    /// <c>Details</c> hold the old and new <c>name</c>, and which names no participant; or, when the name is already
    /// stored, saves nothing and writes no event, and answers as a save does (D84). Notes already started keep the name
    /// they were started with (A3). An archived group is renamed too, as design.md §6.6 names no refusal, though the
    /// screen offers Rename on active groups alone (T07's Notes).
    /// </summary>
    /// <param name="actor">Who renames it.</param>
    /// <param name="groupId">The group.</param>
    /// <param name="ifMatch">
    /// The request's <c>If-Match</c> values, as <c>Headers.IfMatch</c> gives them, or Release 2's
    /// <c>expectedVersion</c> through <see cref="IfMatch.ETag"/>.
    /// </param>
    /// <param name="request">The name as sent.</param>
    /// <param name="cancellationToken">Cancels the rename.</param>
    /// <returns>
    /// <see cref="CommonItemChangeResult.Saved"/>, whether the name changed or not; or, each with nothing written,
    /// checked in this order: <see cref="CommonItemChangeResult.NotFound"/>, for a group not in the organisation; the
    /// code that <see cref="IfMatch.Check"/> gives; <see cref="ErrorCode.EveryNoteFixed"/>, for Every note; the name's
    /// message; and <see cref="ErrorCode.PreconditionFailed"/>, when the group changed after it was loaded.
    /// </returns>
    public Task<CommonItemChangeResult> RenameAsync(
        Actor actor,
        Guid groupId,
        StringValues ifMatch,
        CommonItemGroupNameRequest request,
        CancellationToken cancellationToken) =>
        ChangeAsync(
            groupId,
            ifMatch,
            group =>
            {
                if (!ItemText.GroupName.TryRead(request.Name, out var name, out var errors))
                {
                    return Task.FromResult(CommonItemChangeResult.Invalid(errors));
                }

                var before = NameValues(group);
                group.Name = name;
                return SaveAsync(
                    actor,
                    AuditEventTypes.CommonItemGroup.Updated,
                    group,
                    before,
                    NameValues(group),
                    cancellationToken);
            },
            cancellationToken);

    /// <summary>
    /// Puts the active groups of the tenant's organisation other than Every note, which always comes first and is never
    /// moved (A43), in the order of <paramref name="groupIds"/> (S01.03.06): reads them in the list's order
    /// (<see cref="CommonItemGroupList.InListOrder(IQueryable{CommonItemGroup})"/>), Every note and the archived ones
    /// left out, so that <see cref="ListOrder.Reorder"/> refuses the ID of either as one it was not given; then saves
    /// the new order with <c>common_item_group.reordered</c>, which names no group as its entity and no participant,
    /// and whose <c>Details</c> hold the old and new order (S01.03.06's Notes); or, for the order already stored, saves
    /// nothing and writes no event (D84).
    /// </summary>
    /// <param name="actor">Who reorders them.</param>
    /// <param name="groupIds">The IDs of the active groups other than Every note, in their new order.</param>
    /// <param name="cancellationToken">Cancels the reorder.</param>
    /// <returns>
    /// <see cref="CommonItemChangeResult.Saved"/>, whether the order changed or not; or
    /// <see cref="ErrorCode.OrderMismatch"/>, with nothing written, when the IDs are not the active groups other than
    /// Every note, each once, or when a group changed after the list was read, which the save finds as
    /// <see cref="ErrorCode.PreconditionFailed"/>: a reorder sends no <c>If-Match</c>, and the screen reads the list
    /// again for either (list-editor.md <i>Request errors</i>), so both are the one code.
    /// </returns>
    public async Task<CommonItemChangeResult> ReorderAsync(
        Actor actor, IReadOnlyList<Guid> groupIds, CancellationToken cancellationToken)
    {
        // Tracked, so that the save writes the SortOrder that each moved group is given, checking the version it was
        // read at.
        var activeGroups = await CommonItemGroupList.InListOrder(
                db.CommonItemGroups.Where(group => !group.IsEveryNote && group.ArchivedAtUtc == null))
            .ToListAsync(cancellationToken);
        var reordered = ListOrder.Reorder(activeGroups, groupIds);
        if (reordered.Error is { } refused)
        {
            return CommonItemChangeResult.Refused(refused);
        }

        var saved = await changes.SaveAsync(
            actor,
            AuditEventTypes.CommonItemGroup.Reordered,
            AuditEntityTypes.CommonItemGroup,
            entityId: null,
            participantId: null,
            reordered.Before,
            reordered.After,
            cancellationToken: cancellationToken);
        return saved.Error is null
            ? CommonItemChangeResult.Saved
            : CommonItemChangeResult.Refused(ErrorCode.OrderMismatch);
    }

    /// <summary>
    /// Archives a group of the tenant's organisation (design.md §3.7, S01.03.07), checked as <see cref="RenameAsync"/>
    /// checks a rename, Every note refused as never archived (A43): sets <c>ArchivedAtUtc</c> through
    /// <see cref="Archiving.Archive"/>, which leaves its <c>SortOrder</c>, and saves it with
    /// <c>common_item_group.archived</c>, which names no participant. Its items are left as they are, each with its own
    /// archived state and version (design.md §5.3), so that a restore brings back its active ones, and an item add or
    /// reword in the group is refused for the group's own state (<see cref="ErrorCode.GroupArchived"/>), which no
    /// <c>If-Match</c> could catch. A group archived already keeps the time it was archived at, so nothing is saved and
    /// no event written, and the answer is as a save's (D84).
    /// </summary>
    /// <returns>
    /// <see cref="CommonItemChangeResult.Saved"/>, whether the group changed or not; or, each with nothing written,
    /// checked in this order: <see cref="CommonItemChangeResult.NotFound"/>, for a group not in the organisation; the
    /// code that <see cref="IfMatch.Check"/> gives; <see cref="ErrorCode.EveryNoteFixed"/>, for Every note; and
    /// <see cref="ErrorCode.PreconditionFailed"/>, when the group changed after it was loaded.
    /// </returns>
    public Task<CommonItemChangeResult> ArchiveAsync(
        Actor actor, Guid groupId, StringValues ifMatch, CancellationToken cancellationToken) =>
        ChangeAsync(
            groupId,
            ifMatch,
            group =>
            {
                var before = PlaceValues(group);
                Archiving.Archive(group, timeProvider);
                return SaveAsync(
                    actor,
                    AuditEventTypes.CommonItemGroup.Archived,
                    group,
                    before,
                    PlaceValues(group),
                    cancellationToken);
            },
            cancellationToken);

    /// <summary>
    /// Restores a group of the tenant's organisation (design.md §3.7, S01.03.07), checked as an archive is, Every note
    /// refused too, though it is never archived, as S01.03.07 names both (T07's Notes): puts it at the end of the
    /// active groups other than Every note, which always comes first, through <see cref="Archiving.Restore"/> (A2), its
    /// items as they were, so that its active items show with it again; and saves it with
    /// <c>common_item_group.restored</c>, which names no participant, and whose <c>Details</c> hold its
    /// <c>sortOrder</c> only when the end was not already its place. An active group stays where it is, so nothing is
    /// saved and no event written, and the answer is as a save's (D84).
    /// </summary>
    /// <returns>As <see cref="ArchiveAsync"/>'s.</returns>
    public Task<CommonItemChangeResult> RestoreAsync(
        Actor actor, Guid groupId, StringValues ifMatch, CancellationToken cancellationToken) =>
        ChangeAsync(
            groupId,
            ifMatch,
            async group =>
            {
                var before = PlaceValues(group);
                var activeGroups = await db.CommonItemGroups.AsNoTracking()
                    .Where(g => !g.IsEveryNote && g.ArchivedAtUtc == null)
                    .ToListAsync(cancellationToken);
                Archiving.Restore(group, activeGroups);
                return await SaveAsync(
                    actor,
                    AuditEventTypes.CommonItemGroup.Restored,
                    group,
                    before,
                    PlaceValues(group),
                    cancellationToken);
            },
            cancellationToken);

    /// <summary>
    /// Loads the group through the tenant filter, so that another organisation's is not found (design.md §9.2); checks
    /// <paramref name="ifMatch"/> against its version; and refuses Every note, which is never renamed, archived or
    /// restored (A43), before <paramref name="change"/> changes and saves it.
    /// </summary>
    private async Task<CommonItemChangeResult> ChangeAsync(
        Guid groupId,
        StringValues ifMatch,
        Func<CommonItemGroup, Task<CommonItemChangeResult>> change,
        CancellationToken cancellationToken)
    {
        var group = await db.CommonItemGroups.SingleOrDefaultAsync(g => g.Id == groupId, cancellationToken);
        if (group is null)
        {
            return CommonItemChangeResult.NotFound;
        }

        if (IfMatch.Check(ifMatch, RowVersionToken.Of(group)) is { } refused)
        {
            return CommonItemChangeResult.Refused(refused);
        }

        return group.IsEveryNote ? CommonItemChangeResult.Refused(ErrorCode.EveryNoteFixed) : await change(group);
    }

    // A change to a loaded group, with its event, whose entity is the group, which names no participant, and whose
    // Details hold the values given before and after, those that changed alone; or nothing when none changed.
    private async Task<CommonItemChangeResult> SaveAsync(
        Actor actor,
        string eventType,
        CommonItemGroup group,
        JsonObject before,
        JsonObject after,
        CancellationToken cancellationToken)
    {
        var saved = await changes.SaveAsync(
            actor,
            eventType,
            AuditEntityTypes.CommonItemGroup,
            group.Id,
            participantId: null,
            before,
            after,
            cancellationToken: cancellationToken);
        return saved.Error is { } error ? CommonItemChangeResult.Refused(error) : CommonItemChangeResult.Saved;
    }

    /// <summary>
    /// A group's values as an audit event records them, named and written as design.md §6.6's list gives them, never
    /// <c>id</c>, <c>isEveryNote</c> or <c>rowVersion</c> (<see cref="ChangeDetails"/>): <c>archivedAtUtc</c> as
    /// stored, or JSON <c>null</c>.
    /// </summary>
    private static JsonObject Values(CommonItemGroup group) => new()
    {
        ["name"] = group.Name,
        ["sortOrder"] = group.SortOrder,
        ["archivedAtUtc"] = group.ArchivedAtUtc,
    };

    /// <summary>
    /// The one value that a rename changes, as <c>common_item_group.updated</c> records it, named as
    /// <see cref="Values"/> names it.
    /// </summary>
    private static JsonObject NameValues(CommonItemGroup group) => new() { ["name"] = group.Name };

    /// <summary>
    /// The group's place among the groups, the values that archiving and restoring change, as their events record
    /// them, named as <see cref="Values"/> names them.
    /// </summary>
    private static JsonObject PlaceValues(CommonItemGroup group) => new()
    {
        ["sortOrder"] = group.SortOrder,
        ["archivedAtUtc"] = group.ArchivedAtUtc,
    };
}
