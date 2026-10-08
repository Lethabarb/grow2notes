using System.Net;
using System.Text.Json.Nodes;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Grow2Notes.Web.Platform.Audit;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Grow2Notes.Tests.Platform.Audit;

/// <summary>
/// The audit writer as the app registers it, with no database: <see cref="WrittenInsteadOfTheDatabase"/> ends each
/// save where the database would take over, once the app's own save interceptors have passed it.
/// <see cref="AuditWriterOnSqlServerTests"/> shows what SQL Server stores.
/// </summary>
public sealed class AuditWriterTests : IAsyncDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 9, 1, 2, 3, 456, TimeSpan.Zero);

    private readonly AppWithoutDatabase host = new("Production");
    private readonly WrittenInsteadOfTheDatabase store = new();
    private readonly WebApplicationFactory<Program> app;

    public AuditWriterTests() =>
        app = host.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<TimeProvider>(new FakeTimeProvider(Now));
            services.ConfigureDbContext<Grow2NotesDbContext>(options => options.AddInterceptors(store));
        }));

    [Fact]
    public async Task The_app_provides_one_audit_writer_per_scope()
    {
        await using var first = app.Services.CreateAsyncScope();
        await using var second = app.Services.CreateAsyncScope();

        var writer = Assert.IsType<AuditWriter>(first.ServiceProvider.GetService<IAuditWriter>());
        Assert.Same(writer, first.ServiceProvider.GetService<IAuditWriter>());
        Assert.NotSame(writer, second.ServiceProvider.GetService<IAuditWriter>());
    }

    [Fact]
    public async Task An_event_is_saved_through_the_scope_s_context_as_given_at_the_time_TimeProvider_gives()
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        var organisationId = Guid.NewGuid();
        var actorUserId = Guid.NewGuid();
        var goalId = Guid.NewGuid();
        var participantId = Guid.NewGuid();

        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(organisationId))
        {
            await scope.ServiceProvider.GetRequiredService<IAuditWriter>().WriteAsync(
                organisationId, actorUserId, AuditEventTypes.Goal.Reordered, AuditEntityTypes.Goal, goalId,
                participantId, new JsonObject { ["position"] = new JsonObject { ["old"] = 3, ["new"] = 1 } },
                TestContext.Current.CancellationToken);
        }

        var (context, written) = Assert.Single(store.Written);
        Assert.Same(db, context);
        Assert.Equal(organisationId, written.OrganisationId);
        Assert.Equal(Now.UtcDateTime, written.OccurredAtUtc);
        Assert.Equal(DateTimeKind.Utc, written.OccurredAtUtc.Kind);
        Assert.Equal(actorUserId, written.ActorUserId);
        Assert.Equal(AuditEventTypes.Goal.Reordered, written.EventType);
        Assert.Equal(AuditEntityTypes.Goal, written.EntityType);
        Assert.Equal(goalId, written.EntityId);
        Assert.Equal(participantId, written.ParticipantId);
        Assert.Equal("""{"position":{"old":3,"new":1}}""", written.Details);

        // Outside a request, there is no client.
        Assert.Null(written.IpAddress);

        Assert.Empty(db.ChangeTracker.Entries());
    }

    [Fact]
    public async Task Details_keep_a_key_the_caller_adds()
    {
        await using var scope = app.Services.CreateAsyncScope();
        var organisationId = Guid.NewGuid();
        var details = new JsonObject
        {
            ["role"] = new JsonObject { ["old"] = "Worker", ["new"] = "Manager" },
            ["via"] = "mcp",
        };

        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(organisationId))
        {
            await WriteAsync(scope.ServiceProvider, organisationId, details);
        }

        Assert.Equal(
            """{"role":{"old":"Worker","new":"Manager"},"via":"mcp"}""", Assert.Single(store.Written).Row.Details);
    }

    [Fact]
    public async Task An_operator_command_s_event_can_have_no_actor_entity_participant_or_details()
    {
        await using var scope = app.Services.CreateAsyncScope();
        var organisationId = Guid.NewGuid();

        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(organisationId))
        {
            await scope.ServiceProvider.GetRequiredService<IAuditWriter>().WriteAsync(
                organisationId, actorUserId: null, AuditEventTypes.Admin.SignOutAll, entityType: null,
                entityId: null, participantId: null, details: null, TestContext.Current.CancellationToken);
        }

        var written = Assert.Single(store.Written).Row;
        Assert.Equal(AuditEventTypes.Admin.SignOutAll, written.EventType);
        Assert.Null(written.ActorUserId);
        Assert.Null(written.EntityType);
        Assert.Null(written.EntityId);
        Assert.Null(written.ParticipantId);
        Assert.Null(written.Details);
    }

    [Theory]
    [InlineData("::ffff:203.0.113.7", "203.0.113.7")]
    [InlineData("203.0.113.7", "203.0.113.7")]
    [InlineData("2001:db8::7", "2001:db8::7")]
    public async Task In_a_request_the_IP_address_is_the_client_s_with_an_IPv4_mapped_one_stored_as_IPv4(
        string clientAddress, string stored)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var organisationId = Guid.NewGuid();
        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext =
            new DefaultHttpContext { Connection = { RemoteIpAddress = IPAddress.Parse(clientAddress) } };

        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(organisationId))
        {
            await WriteAsync(scope.ServiceProvider, organisationId);
        }

        Assert.Equal(stored, Assert.Single(store.Written).Row.IpAddress);
    }

    [Fact]
    public async Task An_event_of_an_organisation_other_than_the_tenant_is_refused_and_no_later_save_writes_it()
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        var tenant = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        var other = Guid.NewGuid();

        using (tenant.Use(Guid.NewGuid()))
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                WriteAsync(scope.ServiceProvider, other));

            Assert.StartsWith(
                $"Nothing was saved: {nameof(AuditEvent)} row to be added belongs to an organisation other than the " +
                "tenant.", ex.Message, StringComparison.Ordinal);
        }

        // Under the other organisation's tenant, a save would write the refused event if it were still tracked.
        using (tenant.Use(other))
        {
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        Assert.Empty(store.Written);
    }

    // Disposing the factory the app was derived from disposes the app too.
    public ValueTask DisposeAsync() => host.DisposeAsync();

    private static Task WriteAsync(IServiceProvider services, Guid organisationId, JsonObject? details = null) =>
        services.GetRequiredService<IAuditWriter>().WriteAsync(
            organisationId, Guid.NewGuid(), AuditEventTypes.User.Updated, AuditEntityTypes.User, Guid.NewGuid(),
            participantId: null, details, TestContext.Current.CancellationToken);

    /// <summary>
    /// Ends each save as though the database had written it, and keeps the audit events of each save that got that
    /// far. The app's context adds its own save interceptors after this one, so they run after it too, and a save they
    /// refuse never completes, so its events are not kept.
    /// </summary>
    private sealed class WrittenInsteadOfTheDatabase : SaveChangesInterceptor
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
}
