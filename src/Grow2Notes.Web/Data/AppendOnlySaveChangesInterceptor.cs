using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Grow2Notes.Web.Data;

/// <summary>
/// The app's half of design.md §5.8 <i>Append-only, enforced twice</i>: a save that would change or delete an
/// <see cref="IAppendOnly"/> row throws before anything is written. Adding one is a save like any other.
/// </summary>
/// <remarks>
/// The database's half is the <c>DENY UPDATE, DELETE</c> that each append-only table's migration gives the role
/// <c>grow2notes_runtime</c>. That half also refuses <c>ExecuteUpdate</c> and <c>ExecuteDelete</c>, which run without
/// <c>SaveChanges</c>, so this interceptor never sees them.
/// </remarks>
internal sealed class AppendOnlySaveChangesInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Check(eventData);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Check(eventData);
        return ValueTask.FromResult(result);
    }

    private static void Check(DbContextEventData eventData)
    {
        // A save's event data always has its context. Entries runs DetectChanges first, so a value set on a loaded row
        // since it was tracked counts as a change here, as it would in the save.
        var refused = eventData.Context!.ChangeTracker.Entries<IAppendOnly>()
            .FirstOrDefault(entry => entry.State is EntityState.Modified or EntityState.Deleted);
        if (refused is not null)
        {
            var verb = refused.State == EntityState.Modified ? "changed" : "deleted";
            throw new InvalidOperationException(
                $"Nothing was saved: {refused.Metadata.DisplayName()} row to be {verb} is append-only. Rows of its " +
                "type are added, and never changed or deleted.");
        }
    }
}
