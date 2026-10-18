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
/// <see cref="Actor"/> (mcp-server.md §3.5, convention 1): <c>POST /api/admin/common-item-groups</c> and
/// <c>PUT /api/admin/common-item-groups/{groupId}</c> call it with <see cref="Actor.InApp"/>, and Release 2's tools
/// will call it with <see cref="Channel.Mcp"/>. Each change is saved with its audit event through
/// <see cref="ConfigurationChanges"/>, in the tenant's organisation.
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
    public async Task<CommonItemChangeResult> RenameAsync(
        Actor actor,
        Guid groupId,
        StringValues ifMatch,
        CommonItemGroupNameRequest request,
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

        if (group.IsEveryNote)
        {
            return CommonItemChangeResult.Refused(ErrorCode.EveryNoteFixed);
        }

        if (!ItemText.GroupName.TryRead(request.Name, out var name, out var errors))
        {
            return CommonItemChangeResult.Invalid(errors);
        }

        var before = NameValues(group);
        group.Name = name;
        var saved = await changes.SaveAsync(
            actor,
            AuditEventTypes.CommonItemGroup.Updated,
            AuditEntityTypes.CommonItemGroup,
            group.Id,
            participantId: null,
            before,
            NameValues(group),
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
}
