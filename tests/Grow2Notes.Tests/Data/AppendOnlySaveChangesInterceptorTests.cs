using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests.Data;

/// <summary>
/// The append-only save interceptor (design.md §5.8 <i>In the app</i>) on a stand-in context, through both
/// <c>SaveChanges</c> and <c>SaveChangesAsync</c>, and its place on the app's context.
/// <see cref="InsteadOfTheDatabase"/> ends each save where the database would take over, and the app's context refuses
/// its save before it would open a connection, so these tests need no database. <see cref="AuditEventAppendOnlyTests"/>
/// shows it on SQL Server.
/// </summary>
public sealed class AppendOnlySaveChangesInterceptorTests
{
    public static TheoryData<bool> BothSaves { get; } = [false, true];

    [Theory]
    [MemberData(nameof(BothSaves))]
    public async Task An_append_only_row_is_added(bool saveAsync)
    {
        using var db = new StandInContext();
        db.Readings.Add(new Reading { Id = Guid.NewGuid(), Value = 1 });

        await SaveAsync(db, saveAsync);

        Assert.Equal(1, db.Store.Saves);
    }

    [Theory]
    [MemberData(nameof(BothSaves))]
    public async Task A_changed_append_only_row_is_refused_and_nothing_is_written(bool saveAsync)
    {
        using var db = new StandInContext();
        var reading = Track(db);
        reading.Value = 2;

        // An added row in the same save is refused with it: the whole save is refused.
        db.Readings.Add(new Reading { Id = Guid.NewGuid(), Value = 1 });

        await AssertRefusedAsync(db, saveAsync, reading, "changed");
    }

    [Theory]
    [MemberData(nameof(BothSaves))]
    public async Task A_deleted_append_only_row_is_refused_and_nothing_is_written(bool saveAsync)
    {
        using var db = new StandInContext();
        var reading = Track(db);
        db.Readings.Remove(reading);

        await AssertRefusedAsync(db, saveAsync, reading, "deleted");
    }

    [Theory]
    [MemberData(nameof(BothSaves))]
    public async Task Other_rows_are_changed_and_deleted_beside_append_only_rows_left_as_they_were(bool saveAsync)
    {
        using var db = new StandInContext();
        Track(db);
        var changed = new Visit { Id = Guid.NewGuid(), Purpose = "Loaded" };
        var deleted = new Visit { Id = Guid.NewGuid(), Purpose = "Loaded" };
        db.Visits.AttachRange(changed, deleted);
        changed.Purpose = "Changed";
        db.Visits.Remove(deleted);

        await SaveAsync(db, saveAsync);

        Assert.Equal(1, db.Store.Saves);
    }

    [Fact]
    public async Task The_app_s_context_refuses_a_changed_audit_event_before_it_reads_the_tenant()
    {
        await using var app = new AppWithoutDatabase("Production");
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        var auditEvent = new AuditEvent
        {
            OrganisationId = Guid.NewGuid(),
            OccurredAtUtc = new DateTime(2026, 10, 9, 0, 0, 0, DateTimeKind.Utc),
            EventType = "user.updated",
        };

        // As if loaded: SQL Server sets Id, so a row without one would be tracked as one to add.
        db.Entry(auditEvent).Property(row => row.Id).CurrentValue = 1;
        db.AuditEvents.Attach(auditEvent);
        auditEvent.Details = "{}";

        // Outside a request and outside any Use block, so the tenant interceptor would throw first if it ran first.
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            db.SaveChangesAsync(TestContext.Current.CancellationToken));

        Assert.StartsWith(
            $"Nothing was saved: {nameof(AuditEvent)} row to be changed is append-only.", ex.Message,
            StringComparison.Ordinal);
    }

    // As if loaded from the database: tracked as Unchanged.
    private static Reading Track(StandInContext db)
    {
        var reading = new Reading { Id = Guid.NewGuid(), Value = 1 };
        db.Readings.Attach(reading);
        return reading;
    }

    private static async Task SaveAsync(StandInContext db, bool saveAsync)
    {
        if (saveAsync)
        {
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        else
        {
            db.SaveChanges();
        }
    }

    private static async Task AssertRefusedAsync(StandInContext db, bool saveAsync, Reading refused, string verb)
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => SaveAsync(db, saveAsync));

        Assert.StartsWith(
            $"Nothing was saved: {nameof(Reading)} row to be {verb} is append-only.", ex.Message,
            StringComparison.Ordinal);
        Assert.DoesNotContain(refused.Id.ToString(), ex.Message, StringComparison.OrdinalIgnoreCase);

        // The save stopped before it reached the point where the database would be written.
        Assert.Equal(0, db.Store.Saves);
    }

    private sealed class StandInContext : DbContext
    {
        public DbSet<Reading> Readings => Set<Reading>();

        public DbSet<Visit> Visits => Set<Visit>();

        public InsteadOfTheDatabase Store { get; } = new();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) =>
            optionsBuilder.UseSqlServer().AddInterceptors(new AppendOnlySaveChangesInterceptor(), Store);
    }

    private sealed class Reading : IAppendOnly
    {
        public Guid Id { get; set; }

        public int Value { get; set; }
    }

    private sealed class Visit
    {
        public Guid Id { get; set; }

        public string Purpose { get; set; } = "";
    }
}
