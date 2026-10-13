using System.Text.Json.Nodes;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Grow2Notes.Web.Platform.Audit;
using Grow2Notes.Web.Platform.Configuration;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests.Platform.Configuration;

/// <summary>
/// The one save of a configuration change as the app registers it, with no database:
/// <see cref="WrittenInsteadOfTheDatabase"/> ends each save where the database would take over and keeps its audit
/// events. <see cref="ConfigurationChangesOnSqlServerTests"/> shows the change and its event stored together, or
/// neither, end to end.
/// </summary>
public sealed class ConfigurationChangesTests : IAsyncDisposable
{
    private static readonly Actor Manager = new(Guid.Parse("3a9e2c4b-5d6f-4a7b-8c9d-0e1f2a3b4c5d"), Channel.App);
    private static readonly Guid OrganisationId = Guid.Parse("0b4c1f6e-7a52-4c1d-9e3a-2f8d5b6a7c01");
    private static readonly Guid ParticipantId = Guid.Parse("8a3b9c4d-5e6f-4071-8b2c-3d4e5f6a7b8c");
    private static readonly Guid GoalId = Guid.Parse("7f2a8b3c-4d5e-4f60-9a1b-2c3d4e5f6a7b");
    private static readonly Guid GroupId = Guid.Parse("5d0e6f1a-2b3c-4d5e-8f90-a1b2c3d4e5f6");
    private static readonly Guid FirstItemId = Guid.Parse("1c2d3e4f-5a6b-4c7d-8e9f-0a1b2c3d4e5f");
    private static readonly Guid SecondItemId = Guid.Parse("2d3e4f5a-6b7c-4d8e-9f0a-1b2c3d4e5f6a");

    private readonly AppWithoutDatabase host = new("Production");
    private readonly WrittenInsteadOfTheDatabase store = new();
    private readonly WebApplicationFactory<Program> app;

    public ConfigurationChangesTests() =>
        app = host.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.ConfigureDbContext<Grow2NotesDbContext>(options => options.AddInterceptors(store))));

    [Fact]
    public async Task The_app_provides_one_per_scope()
    {
        await using var first = app.Services.CreateAsyncScope();
        await using var second = app.Services.CreateAsyncScope();

        var changes = first.ServiceProvider.GetService<ConfigurationChanges>();
        Assert.NotNull(changes);
        Assert.Same(changes, first.ServiceProvider.GetService<ConfigurationChanges>());
        Assert.NotSame(changes, second.ServiceProvider.GetService<ConfigurationChanges>());
    }

    [Fact]
    public async Task A_change_is_saved_with_its_event_in_the_tenant_s_organisation_with_the_actor_s_ID()
    {
        var result = await RewordGoalAsync(app, "Made lunch with help");

        Assert.Same(ConfigurationSaveResult.Saved, result);
        Assert.True(result.Changed);
        Assert.Null(result.Error);
        var written = Assert.Single(store.Written).Row;
        Assert.Equal(OrganisationId, written.OrganisationId);
        Assert.Equal(Manager.UserId, written.ActorUserId);
        Assert.Equal(AuditEventTypes.Goal.Updated, written.EventType);
        Assert.Equal(AuditEntityTypes.Goal, written.EntityType);
        Assert.Equal(GoalId, written.EntityId);
        Assert.Equal(ParticipantId, written.ParticipantId);
        Assert.Equal("""{"old":{"text":"Made lunch"},"new":{"text":"Made lunch with help"}}""", written.Details);
    }

    // A reorder is about a list, so its event names no row, and a group's items are named by the group in Details.
    [Fact]
    public async Task Extra_keys_go_into_the_event_s_details_beside_the_old_and_new_values()
    {
        ConfigurationSaveResult result;
        await using (var scope = app.Services.CreateAsyncScope())
        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(OrganisationId))
        {
            result = await scope.ServiceProvider.GetRequiredService<ConfigurationChanges>().SaveAsync(
                Manager, AuditEventTypes.CommonItem.Reordered, AuditEntityTypes.CommonItem, entityId: null,
                participantId: null, new JsonObject { ["order"] = new JsonArray(FirstItemId, SecondItemId) },
                new JsonObject { ["order"] = new JsonArray(SecondItemId, FirstItemId) },
                new JsonObject { ["groupId"] = GroupId }, TestContext.Current.CancellationToken);
        }

        Assert.Same(ConfigurationSaveResult.Saved, result);
        var written = Assert.Single(store.Written).Row;
        Assert.Null(written.EntityId);
        var details = new JsonObject
        {
            ["old"] = new JsonObject { ["order"] = new JsonArray(FirstItemId, SecondItemId) },
            ["new"] = new JsonObject { ["order"] = new JsonArray(SecondItemId, FirstItemId) },
            ["groupId"] = GroupId,
        };
        Assert.Equal(details.ToJsonString(), written.Details);
    }

    // An extra key is not a value of the row, so it does not count as a change.
    [Fact]
    public async Task When_no_value_changed_nothing_is_saved_and_it_says_so()
    {
        var result = await RewordGoalAsync(app, "Made lunch", new JsonObject { ["via"] = "mcp" });

        Assert.Same(ConfigurationSaveResult.Unchanged, result);
        Assert.False(result.Changed);
        Assert.Null(result.Error);
        Assert.Empty(store.Written);
    }

    // Only the first save fails, so the store would keep the event if the service saved it again, or left it tracked
    // for the handler's next save in the scope. ConfigurationChangesOnSqlServerTests shows the change unwritten too.
    [Fact]
    public async Task A_row_changed_since_it_was_loaded_gives_PreconditionFailed_and_no_later_save_writes_the_event()
    {
        var conflicting = app.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.ConfigureDbContext<Grow2NotesDbContext>(options =>
                options.AddInterceptors(new FirstSaveFindsTheRowChanged()))));
        await using var scope = conflicting.Services.CreateAsyncScope();

        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(OrganisationId))
        {
            var result = await RewordGoalInScopeAsync(scope.ServiceProvider, "Made lunch with help");

            Assert.Same(ConfigurationSaveResult.PreconditionFailed, result);
            Assert.False(result.Changed);
            Assert.Same(ErrorCode.PreconditionFailed, result.Error);
            await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>()
                .SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        Assert.Empty(store.Written);
    }

    // Disposing the factory the app was derived from disposes the app too.
    public ValueTask DisposeAsync() => host.DisposeAsync();

    // A goal reworded from "Made lunch" by a manager, in a scope whose tenant is the manager's organisation.
    private static async Task<ConfigurationSaveResult> RewordGoalAsync(
        WebApplicationFactory<Program> app, string text, JsonObject? extraDetails = null)
    {
        await using var scope = app.Services.CreateAsyncScope();
        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(OrganisationId))
        {
            return await RewordGoalInScopeAsync(scope.ServiceProvider, text, extraDetails);
        }
    }

    private static Task<ConfigurationSaveResult> RewordGoalInScopeAsync(
        IServiceProvider scope, string text, JsonObject? extraDetails = null) =>
        scope.GetRequiredService<ConfigurationChanges>().SaveAsync(
            Manager, AuditEventTypes.Goal.Updated, AuditEntityTypes.Goal, GoalId, ParticipantId,
            new JsonObject { ["text"] = "Made lunch" }, new JsonObject { ["text"] = text }, extraDetails,
            TestContext.Current.CancellationToken);

    // As EF Core does when an update's concurrency token matches no row, but for the first save only, so that a later
    // save reaches the store.
    private sealed class FirstSaveFindsTheRowChanged : SaveChangesInterceptor
    {
        private bool failed;

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (failed)
            {
                return ValueTask.FromResult(result);
            }

            failed = true;
            throw new DbUpdateConcurrencyException("The row changed since it was loaded.");
        }
    }
}
