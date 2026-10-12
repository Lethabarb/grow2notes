using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Features.Auth;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Microsoft.Net.Http.Headers;

namespace Grow2Notes.Tests.Features.Auth;

/// <summary>
/// <c>POST /api/auth/setup/start</c> (design.md §6.2, §8.1 step 3), called as the SPA calls it: signed out, over HTTPS,
/// with an antiforgery token fetched first. Each test invites a user of its own into seeded organisation A through the
/// invite core, as tests/README.md's seeded-rows rule asks, and reads the user's ID and token from the link's fragment
/// as the SPA does. The endpoint matrix covers who may call it.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class SetupStartEndpointTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    : IClassFixture<Grow2NotesFactory>
{
    [Fact]
    public async Task A_link_gets_200_with_the_user_s_email_and_display_name_and_the_enrolment_cookie()
    {
        var link = await InviteAsync();
        using var client = factory.CreateHttpsClient();

        using var response = await StartAsync(client, link.UserId, link.Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(["email", "displayName"], body.EnumerateObject().Select(property => property.Name));
        Assert.Equal(link.Email, body.GetProperty("email").GetString());
        Assert.Equal(SetupLinks.DisplayName, body.GetProperty("displayName").GetString());

        // A session cookie, sent to the setup endpoints only (design.md §8). A browser takes a __Secure- cookie only
        // when it is Secure.
        var cookie = Assert.Single(response.SetCookies());
        Assert.Equal("__Secure-grow2notes-setup", cookie.Name.Value);
        Assert.False(string.IsNullOrEmpty(cookie.Value.Value));
        Assert.Equal("/api/auth/setup", cookie.Path.Value);
        Assert.True(cookie.Secure);
        Assert.True(cookie.HttpOnly);
        Assert.Equal(SameSiteMode.Strict, cookie.SameSite);
        Assert.Null(cookie.Expires);
        Assert.Null(cookie.MaxAge);
        Assert.False(cookie.Domain.HasValue);
    }

    // Read with the scheme's own ticket format, as the setup steps after the start read it. The ticket is issued on
    // the fake clock, which the cookie's expiry follows. The stamp is there so that a new link ends it (A53).
    [Fact]
    public async Task The_enrolment_cookie_holds_the_user_s_ID_and_stamp_for_30_minutes_with_no_renewal()
    {
        var clock = new FakeTimeProvider(TestClock.Start);
        await using var app = factory.WithWebHostBuilder(builder => builder.UseClock(clock));
        var link = await InviteAsync();
        var stamp = await factory.SecurityStampAsync(link.UserId);
        using var client = app.CreateHttpsClient();

        using var response = await StartAsync(client, link.UserId, link.Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var options = app.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(EnrolmentCookie.Scheme);
        Assert.False(options.SlidingExpiration);
        var ticket = options.TicketDataFormat.Unprotect(Assert.Single(response.SetCookies()).Value.Value);
        Assert.NotNull(ticket);
        Assert.Equal(link.UserId, ticket.Principal.FindFirstValue(ClaimTypes.NameIdentifier));
        var stampClaim = app.Services.GetRequiredService<IOptions<IdentityOptions>>().Value.ClaimsIdentity
            .SecurityStampClaimType;
        Assert.Equal(stamp, ticket.Principal.FindFirstValue(stampClaim));
        Assert.Equal(TestClock.Start, ticket.Properties.IssuedUtc);
        Assert.Equal(TestClock.Start.AddMinutes(30), ticket.Properties.ExpiresUtc);
        Assert.False(ticket.Properties.IsPersistent);
    }

    // So the link still works after the enrolment cookie has ended, until setup completes (S00.03.05's third
    // criterion, design.md §8.1 step 3).
    [Fact]
    public async Task The_same_link_starts_setup_again_and_neither_start_changes_the_security_stamp()
    {
        var link = await InviteAsync();
        var stamp = await factory.SecurityStampAsync(link.UserId);
        using var client = factory.CreateHttpsClient();

        using (var first = await StartAsync(client, link.UserId, link.Token))
        {
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        }

        using (var second = await StartAsync(client, link.UserId, link.Token))
        {
            Assert.Equal(HttpStatusCode.OK, second.StatusCode);
            Assert.Equal("__Secure-grow2notes-setup", Assert.Single(second.SetCookies()).Name.Value);
        }

        Assert.Equal(stamp, await factory.SecurityStampAsync(link.UserId));
    }

    // The base64 form of a token, but not one that the app made.
    [Fact]
    public async Task A_wrong_token_gets_410()
    {
        var link = await InviteAsync();

        await AssertLinkInvalidAsync(factory, link.UserId, Convert.ToBase64String(new byte[96]));
    }

    [Fact]
    public async Task Another_user_s_token_gets_410()
    {
        var link = await InviteAsync();
        var other = await InviteAsync();

        await AssertLinkInvalidAsync(factory, link.UserId, other.Token);
    }

    [Fact]
    public async Task An_ID_that_names_no_user_gets_410()
    {
        var link = await InviteAsync();

        await AssertLinkInvalidAsync(factory, Guid.NewGuid().ToString(), link.Token);
    }

    [Fact]
    public async Task An_ID_that_is_not_a_GUID_gets_410()
    {
        var link = await InviteAsync();

        await AssertLinkInvalidAsync(factory, "not-a-guid", link.Token);
    }

    // Made Active without a change to the security stamp, so the token still verifies, and only the status refuses it.
    [Fact]
    public async Task An_Active_user_s_token_gets_410()
    {
        var link = await InviteAsync();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await FindUserAsync(users, link.UserId);
            user.Status = UserStatus.Active;
            AssertSucceeded(await users.UpdateAsync(user));
            Assert.True(await users.VerifyUserTokenAsync(
                user, SetupTokenProvider.ProviderName, SetupTokenProvider.Purpose, link.Token));
        }

        await AssertLinkInvalidAsync(factory, link.UserId, link.Token);
    }

    // Identity checks a token's age on the real clock, not TimeProvider (S00.03.05's Notes), so the app is hosted with
    // a lifespan of none, which every token has outlived, as a link older than 7 days has outlived the app's.
    // SetupTokenProviderTests checks the 7 days themselves.
    [Fact]
    public async Task A_token_older_than_the_lifespan_gets_410()
    {
        var link = await InviteAsync();
        await using var app = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.Configure<SetupTokenProviderOptions>(options => options.TokenLifespan = TimeSpan.Zero)));

        await AssertLinkInvalidAsync(app, link.UserId, link.Token);

        // With the app's own lifespan, the same link still starts setup, so its age was what refused it.
        using var client = factory.CreateHttpsClient();
        using var response = await StartAsync(client, link.UserId, link.Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // Validation's 422, which the SPA's api() wrapper does not send again, as it would a 400.
    [Fact]
    public async Task A_body_without_the_ID_and_the_token_gets_422_validation_failed_naming_both()
    {
        using var client = factory.CreateHttpsClient();

        using var response = await PostAsync(client, new { });

        using var problem = JsonDocument.Parse(await response.ReadProblemAsync(HttpStatusCode.UnprocessableEntity));
        Assert.Equal("validation.failed", problem.RootElement.GetProperty("code").GetString());
        Assert.Equal(
            ["token", "userId"],
            problem.RootElement.GetProperty("errors").EnumerateObject().Select(field => field.Name)
                .Order(StringComparer.Ordinal));
        Assert.Empty(response.SetCookies());
    }

    private static async Task AssertLinkInvalidAsync(WebApplicationFactory<Program> app, string userId, string token)
    {
        using var client = app.CreateHttpsClient();

        using var response = await StartAsync(client, userId, token);

        using var problem = JsonDocument.Parse(await response.ReadProblemAsync(HttpStatusCode.Gone));
        Assert.Equal("auth.setup_link_invalid", problem.RootElement.GetProperty("code").GetString());
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Empty(response.SetCookies());
    }

    private static Task<HttpResponseMessage> StartAsync(HttpClient client, string userId, string token) =>
        PostAsync(client, new { userId, token });

    // As the SPA sends it: as JSON, with an antiforgery token that the client fetched before, signed out.
    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, object body) =>
        await client.PostWithTokenAsync(SetupLinks.StartPath, await client.FetchTokenAsync(), body);

    private Task<SetupLink> InviteAsync() => factory.InviteAsync(sqlServer.Seeded.A);

    private static async Task<ApplicationUser> FindUserAsync(UserManager<ApplicationUser> users, string userId)
    {
        var user = await users.FindByIdAsync(userId);
        Assert.NotNull(user);
        return user;
    }

    private static void AssertSucceeded(IdentityResult result) =>
        Assert.True(result.Succeeded, string.Join(" ", result.Errors.Select(error => error.Code)));
}
