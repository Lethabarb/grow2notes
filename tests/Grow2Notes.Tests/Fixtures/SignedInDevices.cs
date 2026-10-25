using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Grow2Notes.Tests.Fixtures;

/// <summary>
/// One user signed in on several devices, each a client with the real session cookie, for the tests that a change to
/// the user reaches every session of theirs on its next request (design.md §8.4, §8.5, §8.6): a reset, a deactivation,
/// a sign-out on another device or <c>admin signout-all</c> ends each, with <c>401</c>, and a change to Worker refuses
/// each a manager's request, with
/// <c>403</c>. Each device signs in with the cookie sign-in (<see cref="CookieSignIn"/>) on the app's fake clock
/// (<see cref="TestClock"/>), and is let in once before the test changes the user; then the clock moves on a second
/// before each device's next request, so that the session check, which compares the security stamp and rebuilds the
/// principal from the user's row, runs on it.
/// </summary>
internal sealed class SignedInDevices : IDisposable
{
    /// <summary>The request a device sends when the test names none: <c>GET /api/auth/me</c>.</summary>
    public static readonly NextRequest Me = NextRequest.Get("/api/auth/me");

    /// <summary><c>POST /api/auth/ping</c>, a request that changes something, in the <c>/api</c> group.</summary>
    public static readonly NextRequest Ping = NextRequest.Post("/api/auth/ping");

    private readonly FakeTimeProvider clock;
    private readonly IReadOnlyList<Device> devices;

    private SignedInDevices(FakeTimeProvider clock, IReadOnlyList<Device> devices)
    {
        this.clock = clock;
        this.devices = devices;
    }

    /// <summary>
    /// Signs the user whose ID is <paramref name="userId"/> in on <paramref name="count"/> devices of
    /// <paramref name="app"/>, which is hosted with <see cref="CookieSignIn.MapCookieSignIn"/> and on
    /// <paramref name="clock"/>; fetches each device's antiforgery token, issued to the user as the SPA's is, for a
    /// <c>POST</c> later; and checks that each device's next request, <see cref="Me"/>, is let in.
    /// </summary>
    /// <remarks>
    /// Sign in an Active user, as <see cref="CookieSignIn.SignInWithCookieAsync"/> asks, and one the test added itself,
    /// as the test then changes them (tests/README.md).
    /// </remarks>
    public static async Task<SignedInDevices> SignInAsync(
        WebApplicationFactory<Program> app, FakeTimeProvider clock, Guid userId, int count = 2)
    {
        var devices = new List<Device>();
        var signedIn = new SignedInDevices(clock, devices);
        try
        {
            for (var i = 0; i < count; i++)
            {
                var client = app.CreateHttpsClient();
                await client.SignInWithCookieAsync(userId);
                devices.Add(new(client, await client.FetchTokenAsync()));
            }

            await signedIn.AssertNextRequestsAsync(HttpStatusCode.OK);
            return signedIn;
        }
        catch
        {
            signedIn.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Asserts that each device's next request gets <paramref name="status"/>: <paramref name="requests"/>' request at
    /// the device's index, one for each device, or <see cref="Me"/> on each when it names none. A <c>401</c> must be
    /// problem details that clear the session cookie, as the session check answers once it ends a session, so the
    /// browser stops sending it.
    /// </summary>
    public Task AssertNextRequestsAsync(HttpStatusCode status, params NextRequest[] requests) =>
        AssertNextAsync(status, answer: null, requests);

    /// <summary>
    /// As <see cref="AssertNextRequestsAsync"/>, for requests whose answer has a JSON body, and asserts
    /// <paramref name="answer"/> of each device's body too, such as the role that <see cref="Me"/> gives after a change
    /// of role.
    /// </summary>
    public Task AssertNextAnswersAsync(
        HttpStatusCode status, Action<JsonElement> answer, params NextRequest[] requests) =>
        AssertNextAsync(status, answer, requests);

    public void Dispose()
    {
        foreach (var device in devices)
        {
            device.Client.Dispose();
        }
    }

    private async Task AssertNextAsync(HttpStatusCode status, Action<JsonElement>? answer, NextRequest[] requests)
    {
        if (requests.Length == 0)
        {
            requests = [.. devices.Select(_ => Me)];
        }

        Assert.Equal(devices.Count, requests.Length);
        foreach (var (device, request) in devices.Zip(requests))
        {
            clock.Advance(TimeSpan.FromSeconds(1));
            using var response = await device.SendAsync(request);
            if (status == HttpStatusCode.Unauthorized)
            {
                await response.ReadProblemAsync(status);
                response.AssertClearsTheSessionCookie();
            }
            else
            {
                Assert.Equal(status, response.StatusCode);
            }

            answer?.Invoke(
                await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken));
        }
    }

    private sealed record Device(HttpClient Client, string AntiforgeryToken)
    {
        public async Task<HttpResponseMessage> SendAsync(NextRequest request)
        {
            using var message = new HttpRequestMessage(request.Method, request.Path);
            if (request.Method == HttpMethod.Post)
            {
                message.Headers.Add(AntiforgeryTokens.Header, AntiforgeryToken);
            }

            return await Client.SendAsync(message, TestContext.Current.CancellationToken);
        }
    }
}

/// <summary>
/// A request that a signed-in device sends next: a <c>GET</c>, or a <c>POST</c> with no body and the device's
/// antiforgery token, as the SPA sends one.
/// </summary>
internal sealed record NextRequest(HttpMethod Method, string Path)
{
    public static NextRequest Get(string path) => new(HttpMethod.Get, path);

    public static NextRequest Post(string path) => new(HttpMethod.Post, path);
}
