using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Grow2Notes.Tests.Fixtures;

/// <summary>
/// Ends each save that reaches it, as though the database had written it, and counts them, so a test of a save
/// interceptor needs no database. A stand-in context adds it after the interceptors under test, so they see each save
/// first, and a save they refuse never reaches it.
/// </summary>
internal sealed class InsteadOfTheDatabase : SaveChangesInterceptor
{
    public int Saves { get; private set; }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Saves++;
        return InterceptionResult<int>.SuppressWithResult(0);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(SavingChanges(eventData, result));
    }
}
