using System.Net;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Grow2Notes.Web.Platform.Audit;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests;

/// <summary>
/// Forwarded headers through the app's pipeline, with no database: a request comes as App Service's front end sends it,
/// with the client's address in <c>X-Forwarded-For</c> and <c>https</c> in <c>X-Forwarded-Proto</c>, from a private
/// or link-local address that the app's dual-stack socket gives as IPv4-mapped IPv6, or as plain IPv4, or from any
/// other address. A test-only endpoint writes an audit event, whose <c>IpAddress</c> is the client address the app
/// took, and answers with the request's scheme as the app saw it.
/// </summary>
public sealed class ForwardedHeadersTests : IAsyncDisposable
{
    // An endpoint that only this test maps. No real endpoint writes an audit event yet.
    private const string WritePath = "/api/test-only/audit";

    private static readonly Guid OrganisationId = Guid.NewGuid();

    private readonly AppWithoutDatabase host = new("Production");
    private readonly WrittenInsteadOfTheDatabase store = new();
    private readonly WebApplicationFactory<Program> app;

    public ForwardedHeadersTests() =>
        app = host.WithWebHostBuilder(builder => builder
            .ConfigureServices(services => services.AddSingleton<IStartupFilter>(new TestOnlyEndpoint()))
            .ConfigureTestServices(services =>
                services.ConfigureDbContext<Grow2NotesDbContext>(options => options.AddInterceptors(store))));

    // Addresses at both ends of each of the front end's private ranges, and link-local ones that Linux App Service's
    // front end calls from, with the client's address in App Service's own form, with a port, and without one.
    [Theory]
    [InlineData("::ffff:10.0.0.1", "203.0.113.7:51234", "203.0.113.7")]
    [InlineData("::ffff:10.255.255.254", "203.0.113.7", "203.0.113.7")]
    [InlineData("::ffff:172.16.0.1", "[2001:db8::7]:51234", "2001:db8::7")]
    [InlineData("::ffff:172.31.255.254", "2001:db8::7", "2001:db8::7")]
    [InlineData("::ffff:192.168.0.1", "203.0.113.7:51234", "203.0.113.7")]
    [InlineData("::ffff:192.168.255.254", "203.0.113.7", "203.0.113.7")]
    [InlineData("10.0.0.1", "203.0.113.7", "203.0.113.7")]
    [InlineData("172.31.255.254", "203.0.113.7", "203.0.113.7")]
    [InlineData("192.168.0.1", "203.0.113.7", "203.0.113.7")]
    [InlineData("::ffff:169.254.130.1", "203.0.113.7:51234", "203.0.113.7")]
    [InlineData("169.254.129.1", "203.0.113.7", "203.0.113.7")]
    public async Task From_a_front_end_address_the_IP_address_and_scheme_are_the_forwarded_ones_with_or_without_a_port(
        string frontEnd, string forwardedFor, string stored)
    {
        var scheme = await SendAsync(frontEnd, forwardedFor);

        Assert.Equal(stored, Assert.Single(store.Written).Row.IpAddress);
        Assert.Equal("https", scheme);
    }

    [Fact]
    public async Task An_address_a_client_put_before_the_one_the_front_end_added_is_ignored()
    {
        await SendAsync("::ffff:10.0.0.1", "198.51.100.66, 203.0.113.7:51234");

        Assert.Equal("203.0.113.7", Assert.Single(store.Written).Row.IpAddress);
    }

    // The entry the front end added is itself a known address here, so only ForwardLimit stops the middleware from
    // taking the one before it.
    [Fact]
    public async Task Only_the_last_entry_is_taken_even_when_it_is_a_known_address()
    {
        await SendAsync("::ffff:10.0.0.1", "198.51.100.66, ::ffff:10.1.2.3");

        Assert.Equal("10.1.2.3", Assert.Single(store.Written).Row.IpAddress);
    }

    // A client calling the app directly, and the addresses just outside each of the front end's ranges.
    [Theory]
    [InlineData("::ffff:203.0.113.9", "203.0.113.9")]
    [InlineData("2001:db8::9", "2001:db8::9")]
    [InlineData("::ffff:9.255.255.254", "9.255.255.254")]
    [InlineData("::ffff:11.0.0.1", "11.0.0.1")]
    [InlineData("::ffff:172.15.255.254", "172.15.255.254")]
    [InlineData("::ffff:172.32.0.1", "172.32.0.1")]
    [InlineData("::ffff:192.167.255.254", "192.167.255.254")]
    [InlineData("::ffff:192.169.0.1", "192.169.0.1")]
    [InlineData("::ffff:169.253.255.254", "169.253.255.254")]
    [InlineData("::ffff:169.255.0.1", "169.255.0.1")]
    public async Task A_request_from_any_other_address_keeps_its_own_IP_address_and_scheme(string remote, string stored)
    {
        var scheme = await SendAsync(remote, "198.51.100.66:51234");

        Assert.Equal(stored, Assert.Single(store.Written).Row.IpAddress);
        Assert.Equal("http", scheme);
    }

    // Disposing the factory the app was derived from disposes the app too.
    public ValueTask DisposeAsync() => host.DisposeAsync();

    // Sent over the test server's plain HTTP, so only X-Forwarded-Proto can make the scheme https.
    private async Task<string> SendAsync(string remoteAddress, string forwardedFor)
    {
        var context = await app.Server.SendAsync(request =>
        {
            request.Request.Method = HttpMethods.Post;
            request.Request.Path = WritePath;
            request.Request.Headers["X-Forwarded-For"] = forwardedFor;
            request.Request.Headers["X-Forwarded-Proto"] = "https";
            request.Connection.RemoteIpAddress = IPAddress.Parse(remoteAddress);
        }, TestContext.Current.CancellationToken);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        using var body = new StreamReader(context.Response.Body);
        return await body.ReadToEndAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Maps an endpoint that writes a report download's event under the organisation's tenant and answers with the
    /// request's scheme. It is routed ahead of the app's own routing, which then leaves the endpoint already chosen, so
    /// it runs where the app's own endpoints do, after the app's middleware, forwarded headers included.
    /// </summary>
    private sealed class TestOnlyEndpoint : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.UseRouting();
            next(app);
            app.UseEndpoints(endpoints => endpoints.MapPost(WritePath, async (
                HttpRequest request, IAuditWriter audit, ITenantContext tenant, CancellationToken cancellationToken) =>
            {
                using (tenant.Use(OrganisationId))
                {
                    await audit.WriteAsync(
                        OrganisationId, actorUserId: null, AuditEventTypes.Report.Downloaded, AuditEntityTypes.Report,
                        Guid.NewGuid(), participantId: null, details: null, cancellationToken);
                }

                return Results.Text(request.Scheme);
            }).AllowAnonymous());
        };
    }
}
