using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Grow2Notes.Web.Platform.Audit;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests.Platform.Audit;

/// <summary>
/// An audit event commits or rolls back with the change it records, and commits at once when no transaction is open
/// (design.md §5.9, §5.3). The change is a user added through <see cref="UserManager{TUser}"/> to seeded organisation
/// A, as an invite adds one: Identity saves through the scope's context, as the writer does, so both saves join the
/// transaction that <c>InTransactionAsync</c> opens. Each test finds its rows by an ID of its own, since every test
/// shares the database.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class AuditWriterTransactionTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    : IClassFixture<Grow2NotesFactory>
{
    private static readonly DateTime InvitedAtUtc = new(2026, 10, 9, 0, 0, 0, DateTimeKind.Utc);

    private readonly SeededOrganisation organisation = sqlServer.Seeded.A;

    [Fact]
    public async Task An_invited_user_and_its_event_commit_together()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        var userId = Guid.NewGuid();

        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(organisation.OrganisationId))
        {
            await db.InTransactionAsync(ct => InviteAsync(scope.ServiceProvider, userId, ct), cancellationToken);
        }

        Assert.Equal(new Stored(Users: 1, Events: 1), await CommittedAsync(userId));
    }

    [Fact]
    public async Task When_the_work_throws_after_both_are_saved_neither_the_user_nor_its_event_is_written()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        var userId = Guid.NewGuid();

        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(organisation.OrganisationId))
        {
            await Assert.ThrowsAsync<WorkFailedException>(() => db.InTransactionAsync(async ct =>
            {
                await InviteAsync(scope.ServiceProvider, userId, ct);

                // Both rows are in the database, inside the transaction, so only its rollback can remove them.
                Assert.Equal(new Stored(Users: 1, Events: 1), await StoredAsync(db, userId, ct));
                throw new WorkFailedException();
            }, cancellationToken));
        }

        Assert.Equal(new Stored(Users: 0, Events: 0), await CommittedAsync(userId));
    }

    // The failure comes before the commit. One after it, whose acknowledgement is lost, can store the event twice
    // (IAuditWriter.WriteAsync).
    [Fact]
    public async Task When_a_transient_failure_runs_the_work_again_the_user_and_its_event_commit_once()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        var userId = Guid.NewGuid();
        var attempts = 0;

        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(organisation.OrganisationId))
        {
            await db.InTransactionAsync(async ct =>
            {
                attempts++;
                await InviteAsync(scope.ServiceProvider, userId, ct);

                // The SQL Server strategy treats a TimeoutException as transient, as TransactionsAndRetriesTests shows.
                if (attempts == 1)
                    throw new TimeoutException();
            }, cancellationToken);
        }

        Assert.Equal(2, attempts);
        Assert.Equal(new Stored(Users: 1, Events: 1), await CommittedAsync(userId));
    }

    [Fact]
    public async Task With_no_transaction_open_the_event_commits_at_once()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        var reportId = Guid.NewGuid();

        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(organisation.OrganisationId))
        {
            Assert.Null(db.Database.CurrentTransaction);

            await scope.ServiceProvider.GetRequiredService<IAuditWriter>().WriteAsync(
                organisation.OrganisationId, organisation.ManagerId, AuditEventTypes.Report.Downloaded,
                AuditEntityTypes.Report, reportId, participantId: null, details: null, cancellationToken);

            // Seen from another connection while this scope is still open, as a download's event must be committed
            // before the file is sent.
            Assert.Equal(1, (await CommittedAsync(reportId)).Events);
        }
    }

    // As an invite will: organisation A's manager adds the user as Invited, then writes its user.invited event. The
    // user's ID is the test's own, so a second attempt adds the same user again.
    private async Task InviteAsync(IServiceProvider services, Guid userId, CancellationToken cancellationToken)
    {
        var email = $"invited-{userId:N}@example.org";
        var result = await services.GetRequiredService<UserManager<ApplicationUser>>().CreateAsync(new ApplicationUser
        {
            Id = userId,
            OrganisationId = organisation.OrganisationId,
            DisplayName = "Avery Brooks",
            Role = UserRole.Worker,
            Status = UserStatus.Invited,
            InvitedAtUtc = InvitedAtUtc,
            InvitedByUserId = organisation.ManagerId,
            UserName = email,
            Email = email,
        });
        Assert.True(result.Succeeded, string.Join(" ", result.Errors.Select(error => error.Code)));

        await services.GetRequiredService<IAuditWriter>().WriteAsync(
            organisation.OrganisationId, organisation.ManagerId, AuditEventTypes.User.Invited, AuditEntityTypes.User,
            userId, participantId: null, details: null, cancellationToken);
    }

    // What has committed, read from a scope of its own.
    private async Task<Stored> CommittedAsync(Guid id)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await StoredAsync(
            scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>(), id, TestContext.Current.CancellationToken);
    }

    // In SQL, so the query filter does not decide what is seen. On a context with a transaction open, the read is in
    // that transaction.
    private static Task<Stored> StoredAsync(Grow2NotesDbContext db, Guid id, CancellationToken cancellationToken) =>
        db.Database.SqlQuery<Stored>($"""
            SELECT (SELECT COUNT(*) FROM [AspNetUsers] WHERE [Id] = {id}) AS [Users],
                   (SELECT COUNT(*) FROM [AuditEvent] WHERE [EntityId] = {id}) AS [Events]
            """).SingleAsync(cancellationToken);

    private sealed record Stored(int Users, int Events);

    // A type only this test throws, so no exception from EF Core or Identity can satisfy the assertion by accident.
    private sealed class WorkFailedException() : Exception("The work failed after saving.");
}
