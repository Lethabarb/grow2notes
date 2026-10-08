using Grow2Notes.Web.Platform;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Grow2Notes.Web.Data;

/// <summary>
/// Keeps every save to the tenant's own rows (design.md §5.9 item 3, D3), as the query filter keeps every query. An
/// added <see cref="ITenantOwned"/> row with no <see cref="ITenantOwned.OrganisationId"/> is stamped with the tenant.
/// A save that would add, change or delete a row of another organisation, or move a row from one organisation to
/// another, throws before anything is written, and leaves the tracked rows as they were.
/// </summary>
/// <remarks>
/// It reads the tenant only when the save adds, changes or deletes a tenant-owned row, so saves of other rows need
/// none: Identity's, such as sign-in's before it has loaded the user, and the organisation's. A context adds it in
/// <c>OnConfiguring</c> with its own <see cref="ITenantContext"/>, so its saves and its queries have the same tenant.
/// </remarks>
internal sealed class TenantSaveChangesInterceptor(ITenantContext tenant) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        StampAndCheck(eventData);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        StampAndCheck(eventData);
        return ValueTask.FromResult(result);
    }

    private void StampAndCheck(DbContextEventData eventData)
    {
        // A save's event data always has its context. Entries runs DetectChanges first, so a value set on a row since
        // it was tracked is seen here, as the save would see it.
        var changed = eventData.Context!.ChangeTracker.Entries<ITenantOwned>()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();
        if (changed.Count == 0)
        {
            return;
        }

        var organisationId = tenant.OrganisationId;

        // Every row is checked before any is stamped, so a refused save changes nothing.
        if (changed.Find(entry => !IsTheTenants(entry, organisationId)) is { } refused)
        {
            throw new InvalidOperationException(Refusal(refused));
        }

        foreach (var entry in changed.Where(entry => entry.State == EntityState.Added))
        {
            var organisation = entry.Property(row => row.OrganisationId);
            if (organisation.CurrentValue == Guid.Empty)
            {
                organisation.CurrentValue = organisationId;
            }
        }
    }

    // A changed or deleted row is checked against the organisation it was loaded with as well, so a row of another
    // organisation cannot be moved into the tenant's either. An added row has no such value.
    private static bool IsTheTenants(EntityEntry<ITenantOwned> entry, Guid organisationId)
    {
        var organisation = entry.Property(row => row.OrganisationId);
        return entry.State == EntityState.Added
            ? organisation.CurrentValue == organisationId || organisation.CurrentValue == Guid.Empty
            : organisation.CurrentValue == organisationId && organisation.OriginalValue == organisationId;
    }

    // Only a row that was loaded can have been moved between organisations.
    private static string Refusal(EntityEntry<ITenantOwned> refused)
    {
        var row = $"Nothing was saved: {refused.Metadata.DisplayName()} row";
        return refused.State == EntityState.Added
            ? $"{row} to be added belongs to an organisation other than the tenant. A save writes only the " +
              "tenant's rows."
            : $"{row} to be {(refused.State == EntityState.Modified ? "changed" : "deleted")} belongs to an " +
              "organisation other than the tenant, or was moved from one organisation to another. A save writes only " +
              "the tenant's rows, and a row stays in the organisation it was added to.";
    }
}
