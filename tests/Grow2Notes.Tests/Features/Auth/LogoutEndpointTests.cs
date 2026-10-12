using System.Net;
using System.Text.Json.Nodes;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform.Audit;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Grow2Notes.Tests.Features.Auth;

/// <summary>
/// <c>POST /api/auth/logout</c> (design.md §6.2, §8.5), called as the SPA calls it: over HTTPS, signed in with the
/// cookie sign-in (<see cref="CookieSignIn"/>) as a user that the test adds to seeded organisation A, so that it finds
/// its own <c>auth.signed_out</c> by its actor, with the antiforgery token fetched after signing in. The app is hosted
/// on a fake clock (<see cref="TestClock"/>) that moves a second before each request, so the session check runs on
/// each, and gives each request a client address, which the test server does not. The endpoint matrix covers who may
/// call it, and <see cref="SignedOutTests"/> what a signed-out caller gets.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class LogoutEndpointTests : IClassFixture<Grow2NotesFactory>, IAsyncDisposable
{
    private const string Path = "/api/auth/logout";

    private const string MePath = "/api/auth/me";

    // A documentation address (RFC 5737), outside App Service's front-end networks, so the app keeps it as the
    // client's.
    private const string ClientAddress = "203.0.113.7";

    private readonly FakeTimeProvider clock = new(TestClock.Start);
    private readonly Grow2NotesFactory factory;
    private readonly SeededOrganisation a;
    private readonly WebApplicationFactory<Program> app;

    public LogoutEndpointTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    {
        this.factory = factory;
        a = sqlServer.Seeded.A;
        app = factory.WithWebHostBuilder(builder => builder
            .MapCookieSignIn()
            .UseClock(clock)
            .ConfigureServices(services => services.AddSingleton<IStartupFilter>(new FromClientAddress())));
    }

    [Fact]
    public async Task Answers_204_with_Clear_Site_Data_and_no_store_and_deletes_the_session_cookie()
    {
        using var client = app.CreateHttpsClient();
        var token = await SignInAsync(client, await AddUserToAAsync());

        using var response = await SignOutAsync(client, token);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(
            "\"cache\", \"cookies\", \"storage\"", Assert.Single(response.Headers.NonValidated["Clear-Site-Data"]));
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        response.AssertClearsTheSessionCookie();
    }

    // The client drops the cookie that the answer deleted, as a browser does, so its next request is signed out.
    [Fact]
    public async Task The_same_client_s_next_request_gets_401()
    {
        using var client = app.CreateHttpsClient();
        var token = await SignInAsync(client, await AddUserToAAsync());
        using (var response = await SignOutAsync(client, token))
        {
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        using var me = await MeAsync(client);

        await me.ReadProblemAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Writes_one_auth_signed_out_in_A_with_the_person_as_actor_and_entity_and_the_client_s_address()
    {
        var userId = await AddUserToAAsync();
        using var client = app.CreateHttpsClient();
        var token = await SignInAsync(client, userId);

        using (var response = await SignOutAsync(client, token))
        {
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        Assert.Equal(
            new StoredEvent(
                a.OrganisationId, clock.GetUtcNow().UtcDateTime, userId, AuditEventTypes.Auth.SignedOut,
                AuditEntityTypes.User, userId, ParticipantId: null, Details: null, ClientAddress),
            Assert.Single(await EventsByAsync(userId)));
    }

    // As on a phone and a laptop. The session check compares the second session's security stamp with the row's on its
    // request, so its 200 shows that signing out left the stamp alone.
    [Fact]
    public async Task A_second_session_of_the_same_person_goes_on()
    {
        var userId = await AddUserToAAsync();
        using var phone = app.CreateHttpsClient();
        var token = await SignInAsync(phone, userId);
        using var laptop = app.CreateHttpsClient();
        await SignInAsync(laptop, userId);
        using (var response = await SignOutAsync(phone, token))
        {
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        using var me = await MeAsync(laptop);

        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }

    [Fact]
    public async Task Without_an_antiforgery_token_it_gets_400_writes_no_event_and_the_session_goes_on()
    {
        var userId = await AddUserToAAsync();
        using var client = app.CreateHttpsClient();
        await SignInAsync(client, userId);

        using (var response = await SignOutAsync(client, token: null))
        {
            await response.ReadProblemAsync(HttpStatusCode.BadRequest);
            Assert.False(response.Headers.Contains("Clear-Site-Data"));
        }

        Assert.Empty(await EventsByAsync(userId));
        using var me = await MeAsync(client);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }

    // A failed write leaves the person signed in, so no sign-out goes unrecorded. The session check renews the cookie
    // on the 500, but nothing deletes it.
    [Fact]
    public async Task When_the_event_cannot_be_written_it_gets_500_and_the_session_goes_on()
    {
        await using var failing = app.WithWebHostBuilder(builder => builder.ConfigureTestServices(
            services => services.AddScoped<IAuditWriter, FailingAuditWriter>()));
        using var client = failing.CreateHttpsClient();
        var token = await SignInAsync(client, await AddUserToAAsync());

        using (var response = await SignOutAsync(client, token))
        {
            await response.ReadProblemAsync(HttpStatusCode.InternalServerError);
            Assert.DoesNotContain(response.SetCookies(), cookie => cookie.Expires == DateTimeOffset.UnixEpoch);
            Assert.False(response.Headers.Contains("Clear-Site-Data"));
        }

        using var me = await MeAsync(client);

        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }

    public ValueTask DisposeAsync() => app.DisposeAsync();

    // Returns the antiforgery token, which is issued to the user a request is signed in as, so is fetched after
    // signing in (tests/README.md).
    private static async Task<string> SignInAsync(HttpClient client, Guid userId)
    {
        await client.SignInWithCookieAsync(userId);
        return await client.FetchTokenAsync();
    }

    // A second after the last request, so the session check runs on this one (tests/README.md), with the token in its
    // header as the SPA sends it, unless there is none.
    private async Task<HttpResponseMessage> SignOutAsync(HttpClient client, string? token)
    {
        clock.Advance(TimeSpan.FromSeconds(1));
        using var request = new HttpRequestMessage(HttpMethod.Post, Path);
        if (token is not null)
        {
            request.Headers.Add(AntiforgeryTokens.Header, token);
        }

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private async Task<HttpResponseMessage> MeAsync(HttpClient client)
    {
        clock.Advance(TimeSpan.FromSeconds(1));
        return await client.GetAsync(MePath, TestContext.Current.CancellationToken);
    }

    // An Active worker of A, with an address of its own, which no other test or run uses, as setup leaves one
    // (design.md §8.1) apart from the passkey or password, which the cookie sign-in does not need.
    private async Task<Guid> AddUserToAAsync()
    {
        var email = $"{Guid.NewGuid():N}@example.org";
        var now = clock.GetUtcNow().UtcDateTime;
        var user = new ApplicationUser
        {
            OrganisationId = a.OrganisationId,
            DisplayName = "Sam Taylor",
            Role = UserRole.Worker,
            Status = UserStatus.Active,
            InvitedAtUtc = now,
            InvitedByUserId = a.ManagerId,
            ActivatedAtUtc = now,
            UserName = email,
            Email = email,
            EmailConfirmed = true,
        };
        await using var scope = app.Services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().CreateAsync(user);
        Assert.True(result.Succeeded, string.Join(" ", result.Errors.Select(error => error.Code)));
        return user.Id;
    }

    // Read in SQL from a scope of its own, so that neither the query filter nor a context's tracked rows decide what is
    // seen. Adding the user and signing in write no event, so every event the user acted in is the sign-out's.
    private async Task<List<StoredEvent>> EventsByAsync(Guid userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Database
            .SqlQuery<StoredEvent>($"""
                SELECT [OrganisationId], [OccurredAtUtc], [ActorUserId], [EventType], [EntityType], [EntityId],
                       [ParticipantId], [Details], [IpAddress]
                FROM [AuditEvent]
                WHERE [ActorUserId] = {userId}
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
        string? IpAddress);

    // Throws before anything is written, as the app's writer does when its save fails.
    private sealed class FailingAuditWriter : IAuditWriter
    {
        public Task WriteAsync(
            Guid organisationId,
            Guid? actorUserId,
            string eventType,
            string? entityType,
            Guid? entityId,
            Guid? participantId,
            JsonObject? details,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The audit event could not be written.");
    }

    /// <summary>
    /// Gives every request <see cref="ClientAddress"/> as its client address, before the app's middleware, as the
    /// server's socket gives one: the test server gives none, and the audit event records it.
    /// </summary>
    private sealed class FromClientAddress : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, nextMiddleware) =>
            {
                context.Connection.RemoteIpAddress = IPAddress.Parse(ClientAddress);
                return nextMiddleware(context);
            });
            next(app);
        };
    }
}
