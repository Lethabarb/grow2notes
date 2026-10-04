using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests.Data;

[Collection<SqlServerCollection>]
public sealed class TransactionsAndRetriesTests(Grow2NotesFactory factory) : IClassFixture<Grow2NotesFactory>
{
    [Fact]
    public void Both_contexts_use_the_retrying_SQL_Server_execution_strategy()
    {
        using var scope = factory.Services.CreateScope();
        var app = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        var keys = scope.ServiceProvider.GetRequiredService<KeysDbContext>();

        Assert.IsType<SqlServerRetryingExecutionStrategy>(app.Database.CreateExecutionStrategy());
        Assert.IsType<SqlServerRetryingExecutionStrategy>(keys.Database.CreateExecutionStrategy());
    }

    [Fact]
    public async Task InTransactionAsync_commits_the_work()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        var organisation = NewOrganisation();

        await db.InTransactionAsync(async ct =>
        {
            db.Organisations.Add(organisation);
            await db.SaveChangesAsync(ct);
        }, cancellationToken);

        Assert.True(await db.Organisations.AnyAsync(o => o.Id == organisation.Id, cancellationToken));
    }

    [Fact]
    public async Task InTransactionAsync_rolls_back_the_work_and_rethrows_a_failure_that_is_not_transient()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        var organisation = NewOrganisation();
        var attempts = 0;

        await Assert.ThrowsAsync<WorkFailedException>(() => db.InTransactionAsync(async ct =>
        {
            attempts++;
            db.Organisations.Add(organisation);
            await db.SaveChangesAsync(ct);
            throw new WorkFailedException();
        }, cancellationToken));

        Assert.Equal(1, attempts);
        Assert.False(await db.Organisations.AnyAsync(o => o.Id == organisation.Id, cancellationToken));
    }

    [Fact]
    public async Task InTransactionAsync_runs_the_whole_work_again_in_a_new_transaction_after_a_transient_failure()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        List<int> trackedAtStart = [];
        List<Guid> saved = [];

        await db.InTransactionAsync(async ct =>
        {
            trackedAtStart.Add(db.ChangeTracker.Entries().Count());
            var organisation = NewOrganisation();
            db.Organisations.Add(organisation);
            await db.SaveChangesAsync(ct);
            saved.Add(organisation.Id);

            // The SQL Server strategy treats a TimeoutException as transient, as it does the SqlException numbers of a
            // failover, which a test cannot make the server send on demand.
            if (saved.Count == 1)
                throw new TimeoutException();
        }, cancellationToken);

        Assert.Equal([0, 0], trackedAtStart);
        Assert.Equal(2, saved.Count);
        Assert.False(await db.Organisations.AnyAsync(o => o.Id == saved[0], cancellationToken));
        Assert.True(await db.Organisations.AnyAsync(o => o.Id == saved[1], cancellationToken));
    }

    private static Organisation NewOrganisation() =>
        new() { Name = "Transactions test", CreatedAtUtc = new DateTime(2026, 10, 4, 0, 0, 0, DateTimeKind.Utc) };

    // A type only this test throws, so EF Core's own InvalidOperationException (thrown when SaveChanges runs in a user
    // transaction without the execution strategy) can never satisfy the assertion by accident.
    private sealed class WorkFailedException() : Exception("The work failed after saving.");
}
