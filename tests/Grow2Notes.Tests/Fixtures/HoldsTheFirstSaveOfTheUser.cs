using Grow2Notes.Web.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests.Fixtures;

/// <summary>
/// Holds back the first save, made inside a transaction, that changes the user whose ID it is given, until the test
/// lets it go: in a change to a user made through <c>UserChanges</c>, the change's first save, which comes once the
/// transaction has loaded the user, checked their ETag and, under the last-manager rule, taken the organisation's
/// lock. Every other save passes. A test sends a second request meanwhile, to show what two changes at once do.
/// </summary>
internal sealed class HoldsTheFirstSaveOfTheUser(Guid userId) : SaveChangesInterceptor
{
    private readonly TaskCompletionSource reached = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource released = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int held;

    /// <summary>Completes once the save is held.</summary>
    public Task Reached => reached.Task;

    /// <summary>
    /// <paramref name="app"/>, with this interceptor on every context it makes, for the requests that the test sends.
    /// </summary>
    public WebApplicationFactory<Program> On(WebApplicationFactory<Program> app) =>
        app.WithWebHostBuilder(builder => builder.ConfigureTestServices(
            services => services.ConfigureDbContext<Grow2NotesDbContext>(options => options.AddInterceptors(this))));

    /// <summary>Lets the held save go on.</summary>
    public void Release() => released.TrySetResult();

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { Database.CurrentTransaction: not null } context
            && context.ChangeTracker.Entries<ApplicationUser>().Any(
                entry => entry.State == EntityState.Modified && entry.Entity.Id == userId)
            && Interlocked.Exchange(ref held, 1) == 0)
        {
            reached.SetResult();
            await released.Task.WaitAsync(cancellationToken);
        }

        return result;
    }
}
