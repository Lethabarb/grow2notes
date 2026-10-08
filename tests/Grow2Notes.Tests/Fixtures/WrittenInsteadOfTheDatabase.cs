using Grow2Notes.Web.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Grow2Notes.Tests.Fixtures;

/// <summary>
/// Ends each save as though the database had written it, and keeps the audit events of each save that got that far,
/// so a test of the audit writer needs no database. The app's context adds its own save interceptors after this one,
/// so they run after it too, and a save they refuse never completes, so its events are not kept.
/// </summary>
internal sealed class WrittenInsteadOfTheDatabase : SaveChangesInterceptor
{
    public List<(DbContext Context, AuditEvent Row)> Written { get; } = [];

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(InterceptionResult<int>.SuppressWithResult(0));
    }

    // A save ended here accepts no changes, so its rows are still to be added.
    public override ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        var context = eventData.Context!;
        Written.AddRange(context.ChangeTracker.Entries<AuditEvent>()
            .Where(entry => entry.State == EntityState.Added)
            .Select(entry => (context, entry.Entity)));
        return ValueTask.FromResult(result);
    }
}
