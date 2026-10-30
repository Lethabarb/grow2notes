using System.Text.Json.Nodes;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Features.Users;
using Grow2Notes.Web.Platform;
using Grow2Notes.Web.Platform.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;

namespace Grow2Notes.Tests.Features.Users;

/// <summary>
/// <see cref="UserDeactivation"/> called as a handler, with the channel an assistant's tool will use (mcp-server.md
/// §3.5, A137 and A138): a user already Deactivated is <see cref="UserChangeResult.Unchanged"/> and the last-manager
/// rule is given, both before the version refusal, which the assistant's channel gives only for a change that would be
/// made; and the reason goes into the event's details. The app's own answers are in
/// <see cref="UserDeactivationEndpointsTests"/>. Each test deactivates only users it adds, in seeded organisation A or,
/// for the only Active manager, an organisation of its own (tests/README.md).
/// </summary>
[Collection<SqlServerCollection>]
public sealed class UserDeactivationTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    : IClassFixture<Grow2NotesFactory>
{
    private readonly SeededOrganisation organisation = sqlServer.Seeded.A;

    [Fact]
    public async Task A_user_already_Deactivated_is_Unchanged_through_MCP_with_no_version_and_nothing_is_written()
    {
        var userId = await factory.Services.AddUserAsync(
            organisation, UserRole.Worker, "Already deactivated", UserStatus.Deactivated);

        var result = await DeactivateAsync(Mcp(), userId, StringValues.Empty, reason: "Asked by phone");

        Assert.Same(UserChangeResult.Unchanged, result);
        Assert.False(result.Changed);
        Assert.Empty(await StoredEventsAboutAsync(userId));
    }

    [Fact]
    public async Task The_only_Active_manager_is_refused_as_the_last_manager_before_the_version_through_MCP()
    {
        var own = await factory.Services.AddOrganisationAsync();

        var result = await DeactivateAsync(
            new(own.ManagerId, Channel.Mcp), own.OrganisationId, own.ManagerId, StringValues.Empty, reason: null);

        Assert.Same(ErrorCode.LastManager, result.Error);
        Assert.Equal(UserStatus.Active, (await StoredAsync(own.ManagerId)).Status);
        Assert.Empty(await StoredEventsAboutAsync(own.ManagerId));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_missing_or_stale_version_is_refused_through_MCP_only_after_the_other_checks(bool stale)
    {
        var userId = await factory.Services.AddUserAsync(organisation, UserRole.Worker, "Version checked");
        var before = await StoredAsync(userId);

        var result = await DeactivateAsync(
            Mcp(), userId, stale ? IfMatch.ETag("stale") : StringValues.Empty, reason: null);

        Assert.Same(stale ? ErrorCode.PreconditionFailed : ErrorCode.PreconditionRequired, result.Error);
        Assert.False(result.Changed);
        var after = await StoredAsync(userId);
        Assert.Equal((before.Status, before.ConcurrencyStamp), (after.Status, after.ConcurrencyStamp));
        Assert.Empty(await StoredEventsAboutAsync(userId));
    }

    [Theory]
    [InlineData("Asked by phone", "{\"reason\":\"Asked by phone\"}")]
    [InlineData(null, null)]
    public async Task The_event_has_the_reason_in_its_details_when_one_is_given_and_none_otherwise(
        string? reason, string? expectedDetails)
    {
        var userId = await factory.Services.AddUserAsync(organisation, UserRole.Worker, "Deactivated through MCP");
        var stamp = (await StoredAsync(userId)).ConcurrencyStamp;

        var result = await DeactivateAsync(Mcp(), userId, IfMatch.ETag(stamp), reason);

        Assert.Same(UserChangeResult.Done, result);
        Assert.True(result.Changed);
        Assert.Equal(UserStatus.Deactivated, (await StoredAsync(userId)).Status);
        var written = Assert.Single(await StoredEventsAboutAsync(userId));
        Assert.Equal(AuditEventTypes.User.Deactivated, written.EventType);
        Assert.Equal(
            expectedDetails is null ? null : JsonNode.Parse(expectedDetails)?.ToJsonString(),
            written.Details is null ? null : JsonNode.Parse(written.Details)?.ToJsonString());
    }

    [Fact]
    public async Task The_app_still_writes_no_details_and_refuses_a_missing_version_for_a_Deactivated_user_first()
    {
        var deactivated = await factory.Services.AddUserAsync(
            organisation, UserRole.Worker, "Already deactivated", UserStatus.Deactivated);
        var active = await factory.Services.AddUserAsync(organisation, UserRole.Worker, "Deactivated in the app");
        var stamp = (await StoredAsync(active)).ConcurrencyStamp;
        var app = new Actor(organisation.ManagerId, Channel.App);

        var refused = await DeactivateAsync(app, deactivated, StringValues.Empty, reason: null);
        var done = await DeactivateAsync(app, active, IfMatch.ETag(stamp), reason: null);

        Assert.Same(ErrorCode.PreconditionRequired, refused.Error);
        Assert.Same(UserChangeResult.Done, done);
        Assert.Null(Assert.Single(await StoredEventsAboutAsync(active)).Details);
    }

    private Actor Mcp() => new(organisation.ManagerId, Channel.Mcp);

    private Task<UserChangeResult> DeactivateAsync(Actor actor, Guid userId, StringValues ifMatch, string? reason) =>
        DeactivateAsync(actor, organisation.OrganisationId, userId, ifMatch, reason);

    private async Task<UserChangeResult> DeactivateAsync(
        Actor actor, Guid organisationId, Guid userId, StringValues ifMatch, string? reason)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(organisationId))
        {
            return await scope.ServiceProvider.GetRequiredService<UserDeactivation>()
                .DeactivateAsync(actor, userId, ifMatch, reason, TestContext.Current.CancellationToken);
        }
    }

    private async Task<StoredUser> StoredAsync(Guid userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var user = await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Users
            .SingleAsync(row => row.Id == userId, TestContext.Current.CancellationToken);
        return new(user.Status, user.ConcurrencyStamp!);
    }

    // By raw SQL, so that no tenant filter decides which events are seen.
    private async Task<List<StoredEvent>> StoredEventsAboutAsync(Guid userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Database
            .SqlQuery<StoredEvent>($"""
                SELECT [EventType], [Details]
                FROM [AuditEvent]
                WHERE [EntityId] = {userId}
                ORDER BY [Id]
                """)
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    private sealed record StoredUser(UserStatus Status, string ConcurrencyStamp);

    private sealed record StoredEvent(string EventType, string? Details);
}
