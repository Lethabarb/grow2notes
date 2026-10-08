using System.Net;
using System.Text.Json.Nodes;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Grow2Notes.Web.Platform.Audit;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Grow2Notes.Tests.Platform.Audit;

/// <summary>
/// What the audit writer stores in SQL Server, under seeded organisation A's tenant. Each test finds its event by an
/// entity ID of its own, since every test shares the database. <see cref="AuditWriterTests"/> covers the writer
/// without a database.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class AuditWriterOnSqlServerTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    : IClassFixture<Grow2NotesFactory>
{
    // An endpoint that only this test maps. No real endpoint writes an audit event yet.
    private const string WritePath = "/api/test-only/audit";

    private readonly SeededOrganisation organisation = sqlServer.Seeded.A;

    [Fact]
    public async Task An_event_is_stored_as_given_with_an_extra_key_in_its_Details_and_no_IP_address_outside_a_request()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var now = new DateTimeOffset(2026, 10, 9, 1, 2, 3, 456, TimeSpan.Zero);
        await using var app = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(
            services => services.AddSingleton<TimeProvider>(new FakeTimeProvider(now))));
        await using var scope = app.Services.CreateAsyncScope();
        var goalId = Guid.NewGuid();
        var participantId = Guid.NewGuid();
        var details = new JsonObject
        {
            ["text"] = new JsonObject { ["old"] = "Make a cup of tea", ["new"] = "Make a cup of tea on their own" },
            ["via"] = "mcp",
        };

        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(organisation.OrganisationId))
        {
            await scope.ServiceProvider.GetRequiredService<IAuditWriter>().WriteAsync(
                organisation.OrganisationId, organisation.ManagerId, AuditEventTypes.Goal.Updated,
                AuditEntityTypes.Goal, goalId, participantId, details, cancellationToken);
        }

        // SQL Server reads the extra key, as the operator's extraction of MCP events does (mcp-server.md §9).
        Assert.Equal(
            new StoredEvent(
                organisation.OrganisationId, now.UtcDateTime, organisation.ManagerId, AuditEventTypes.Goal.Updated,
                AuditEntityTypes.Goal, goalId, participantId, details.ToJsonString(), IpAddress: null, Via: "mcp"),
            Assert.Single(await StoredEventsAsync(goalId)));
    }

    [Fact]
    public async Task In_a_request_the_IP_address_is_the_client_s_with_an_IPv4_mapped_one_stored_as_IPv4()
    {
        await using var app = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddSingleton<IStartupFilter>(new TestOnlyEndpoint(organisation.OrganisationId))));
        var reportId = Guid.NewGuid();

        var context = await app.Server.SendAsync(request =>
        {
            request.Request.Method = HttpMethods.Post;
            request.Request.Path = $"{WritePath}/{reportId}";
            request.Connection.RemoteIpAddress = IPAddress.Parse("::ffff:203.0.113.7");
        }, TestContext.Current.CancellationToken);

        Assert.Equal(StatusCodes.Status204NoContent, context.Response.StatusCode);
        Assert.Equal("203.0.113.7", Assert.Single(await StoredEventsAsync(reportId)).IpAddress);
    }

    [Fact]
    public async Task An_event_of_an_organisation_other_than_the_tenant_is_refused_and_nothing_is_written()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = factory.Services.CreateAsyncScope();
        var userId = Guid.NewGuid();

        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(organisation.OrganisationId))
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                scope.ServiceProvider.GetRequiredService<IAuditWriter>().WriteAsync(
                    sqlServer.Seeded.B.OrganisationId, sqlServer.Seeded.B.ManagerId, AuditEventTypes.User.Invited,
                    AuditEntityTypes.User, userId, participantId: null, details: null, cancellationToken));

            Assert.StartsWith(
                $"Nothing was saved: {nameof(AuditEvent)} row to be added belongs to an organisation other than the " +
                "tenant.", ex.Message, StringComparison.Ordinal);
        }

        Assert.Empty(await StoredEventsAsync(userId));
    }

    // Read in SQL from a scope of its own, so that neither the query filter nor a context's tracked rows decide what is
    // seen.
    private async Task<List<StoredEvent>> StoredEventsAsync(Guid entityId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        return await db.Database.SqlQuery<StoredEvent>($"""
            SELECT [OrganisationId], [OccurredAtUtc], [ActorUserId], [EventType], [EntityType], [EntityId],
                   [ParticipantId], [Details], [IpAddress], JSON_VALUE([Details], '$.via') AS [Via]
            FROM [AuditEvent]
            WHERE [EntityId] = {entityId}
            """).ToListAsync(TestContext.Current.CancellationToken);
    }

    private sealed record StoredEvent(
        Guid OrganisationId,
        DateTime OccurredAtUtc,
        Guid? ActorUserId,
        string EventType,
        string? EntityType,
        Guid? EntityId,
        Guid? ParticipantId,
        string? Details,
        string? IpAddress,
        string? Via);

    /// <summary>
    /// Maps an endpoint that writes a report download's event, for the report ID in its path, under the organisation's
    /// tenant. It is routed ahead of the app's own routing, which then leaves the endpoint already chosen, so it runs
    /// where the app's own endpoints do, after the app's middleware.
    /// </summary>
    private sealed class TestOnlyEndpoint(Guid organisationId) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.UseRouting();
            next(app);
            app.UseEndpoints(endpoints => endpoints.MapPost($"{WritePath}/{{reportId:guid}}", async (
                Guid reportId, IAuditWriter audit, ITenantContext tenant, CancellationToken cancellationToken) =>
            {
                using (tenant.Use(organisationId))
                {
                    await audit.WriteAsync(
                        organisationId, actorUserId: null, AuditEventTypes.Report.Downloaded,
                        AuditEntityTypes.Report, reportId, participantId: null, details: null, cancellationToken);
                }

                return Results.NoContent();
            }).AllowAnonymous());
        };
    }
}
