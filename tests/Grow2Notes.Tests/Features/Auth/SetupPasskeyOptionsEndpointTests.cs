using System.Buffers.Text;
using System.Net;
using System.Text;
using System.Text.Json;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Microsoft.Net.Http.Headers;

namespace Grow2Notes.Tests.Features.Auth;

/// <summary>
/// <c>POST /api/auth/setup/passkey/options</c> (design.md §6.2, §8.1 step 4, §8.4), and with it the enrolment check
/// that every setup step after the start makes, called as the SPA calls it: signed out, over HTTPS, after
/// <c>POST /api/auth/setup/start</c> with a link of the test's own (<see cref="SetupLinks"/>), with the one antiforgery
/// token that the SPA fetches before the start. The endpoint matrix covers callers with no enrolment cookie.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class SetupPasskeyOptionsEndpointTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    : IClassFixture<Grow2NotesFactory>
{
    private const string Path = "/api/auth/setup/passkey/options";

    // The factory's App:Origin is https://grow2notes.example and the client calls https://localhost, so the domain
    // shows that it comes from the setting, not from the request. The prompt waits 5 minutes, never less
    // (passkey-flows.md).
    [Fact]
    public async Task After_the_start_the_user_gets_options_for_a_verified_discoverable_passkey_on_the_app_s_domain()
    {
        var link = await InviteAsync();
        var stamp = await factory.SecurityStampAsync(link.UserId);
        using var client = factory.CreateHttpsClient();
        var (token, _) = await client.StartSetupAsync(link);

        using var response = await PostAsync(client, token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        using var options = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var root = options.RootElement;
        Assert.Equal("grow2notes.example", root.GetProperty("rp").GetProperty("id").GetString());
        var user = root.GetProperty("user");
        var userHandle = Base64Url.DecodeFromChars(user.GetProperty("id").GetString());
        Assert.Equal(link.UserId, Encoding.UTF8.GetString(userHandle));
        Assert.Equal(link.Email, user.GetProperty("name").GetString());
        Assert.Equal(SetupLinks.DisplayName, user.GetProperty("displayName").GetString());
        var authenticatorSelection = root.GetProperty("authenticatorSelection");
        Assert.Equal("required", authenticatorSelection.GetProperty("residentKey").GetString());
        Assert.Equal("required", authenticatorSelection.GetProperty("userVerification").GetString());
        Assert.Equal(300_000, root.GetProperty("timeout").GetInt32());

        // Nothing before completion changes the stamp, so the link still works (S00.03.05's third criterion).
        Assert.Equal(stamp, await factory.SecurityStampAsync(link.UserId));
    }

    [Fact]
    public async Task Without_the_enrolment_cookie_the_options_get_401()
    {
        using var client = factory.CreateHttpsClient();
        var token = await client.FetchTokenAsync();

        using var response = await PostAsync(client, token);

        await AssertUnauthorizedAsync(response);
    }

    // Made Active as completion leaves a user, so the cookie, still within its 30 minutes, is refused for the status.
    [Fact]
    public async Task With_the_enrolment_cookie_of_a_user_made_Active_since_the_start_the_options_get_401()
    {
        var link = await InviteAsync();
        using var client = factory.CreateHttpsClient();
        var (token, _) = await client.StartSetupAsync(link);
        await ChangeUserAsync(link.UserId, (users, user) =>
        {
            user.Status = UserStatus.Active;
            return users.UpdateAsync(user);
        });

        using var response = await PostAsync(client, token);

        await AssertUnauthorizedAsync(response);
    }

    // A resend or a Reset sign-in leaves the user Invited with a new stamp and a new link (design.md §8.6), so the
    // cookie that the older link started, still within its 30 minutes, is refused for the stamp (A53).
    [Fact]
    public async Task With_the_enrolment_cookie_of_an_older_link_the_options_get_401()
    {
        var link = await InviteAsync();
        using var client = factory.CreateHttpsClient();
        var (token, _) = await client.StartSetupAsync(link);
        await ChangeUserAsync(link.UserId, (users, user) => users.UpdateSecurityStampAsync(user));

        using var response = await PostAsync(client, token);

        await AssertUnauthorizedAsync(response);
    }

    // The cookie keeps its ticket's expiry to the second, and a step at 30 minutes does not renew it.
    [Fact]
    public async Task The_enrolment_cookie_works_until_30_minutes_after_the_start_and_not_a_second_longer()
    {
        var clock = new FakeTimeProvider(TestClock.Start);
        await using var app = factory.WithWebHostBuilder(builder => builder.UseClock(clock));
        var link = await InviteAsync();
        using var client = app.CreateHttpsClient();
        var (token, _) = await client.StartSetupAsync(link);

        clock.Advance(TimeSpan.FromMinutes(30));
        using (var atThirtyMinutes = await PostAsync(client, token))
        {
            Assert.Equal(HttpStatusCode.OK, atThirtyMinutes.StatusCode);
        }

        clock.Advance(TimeSpan.FromSeconds(1));
        using var aSecondLater = await PostAsync(client, token);

        await AssertUnauthorizedAsync(aSecondLater);
    }

    // A browser sends the cookie to the setup endpoints only (its Path), so it is sent by hand, from a client that
    // keeps no cookies. It never becomes the request's user, so it is no session.
    [Fact]
    public async Task The_enrolment_cookie_sent_alone_to_the_session_s_start_up_gets_401()
    {
        var link = await InviteAsync();
        using var client = factory.CreateHttpsClient();
        var (token, enrolmentCookie) = await client.StartSetupAsync(link);
        using var withoutCookies = factory.CreateClient(
            new() { BaseAddress = new("https://localhost"), AllowAutoRedirect = false, HandleCookies = false });
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.Add(HeaderNames.Cookie, new CookieHeaderValue(enrolmentCookie.Name, enrolmentCookie.Value)
            .ToString());

        using var response = await withoutCookies.SendAsync(request, TestContext.Current.CancellationToken);

        await response.ReadProblemAsync(HttpStatusCode.Unauthorized);

        // The cookie itself is good: the setup step gets the options with it.
        using var options = await PostAsync(client, token);
        Assert.Equal(HttpStatusCode.OK, options.StatusCode);
    }

    // The enrolment cookie never becomes the request's user, so the token, which the antiforgery endpoint issued to
    // nobody before the start, still matches the request's user after it (S00.04.02's Notes, from S00.01.04).
    [Fact]
    public async Task An_antiforgery_token_fetched_before_the_start_is_accepted_after_it()
    {
        var link = await InviteAsync();
        using var client = factory.CreateHttpsClient();
        var (tokenBeforeTheStart, _) = await client.StartSetupAsync(link);

        using var response = await PostAsync(client, tokenBeforeTheStart);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // A refusal starts no ceremony, so it sets no cookie.
    private static async Task AssertUnauthorizedAsync(HttpResponseMessage response)
    {
        await response.ReadProblemAsync(HttpStatusCode.Unauthorized);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Empty(response.SetCookies());
    }

    private Task<SetupLink> InviteAsync() => factory.InviteAsync(sqlServer.Seeded.A);

    // In a scope of its own, as a manager's request would change the user.
    private async Task ChangeUserAsync(
        string userId, Func<UserManager<ApplicationUser>, ApplicationUser, Task<IdentityResult>> change)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByIdAsync(userId);
        Assert.NotNull(user);
        var result = await change(users, user);
        Assert.True(result.Succeeded, string.Join(" ", result.Errors.Select(error => error.Code)));
    }

    // With no body, as the SPA sends it.
    private static Task<HttpResponseMessage> PostAsync(HttpClient client, string token) =>
        client.PostWithTokenAsync(Path, token);
}
