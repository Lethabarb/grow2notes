using System.Text.Json.Nodes;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Grow2Notes.Web.Platform.Audit;
using Grow2Notes.Web.Platform.Configuration;
using Microsoft.EntityFrameworkCore;

namespace Grow2Notes.Web.Features.CommonItems;

/// <summary>
/// The handler that changes an organisation's common item groups (design.md §6.6), as a handler class that takes the
/// <see cref="Actor"/> (mcp-server.md §3.5, convention 1): <c>POST /api/admin/common-item-groups</c> calls it with
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
}
