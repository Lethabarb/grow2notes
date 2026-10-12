using System.Buffers.Text;
using System.Net;
using System.Text.Json;
using Grow2Notes.Tests.Fixtures;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace Grow2Notes.Tests.Features.Auth;

/// <summary>
/// <c>POST /api/auth/passkey/options</c> (design.md §6.2, §8.4), called signed out over HTTPS with the antiforgery
/// token, as the SPA calls it. The app is hosted as it is deployed, without the test-only sign-in, and without a
/// database, with its Data Protection keys in memory, so the tests run without Docker. Its <c>App:Origin</c> is the
/// factory's, <c>https://grow2notes.example</c>, and the client calls <c>https://localhost</c>, so the options' domain
/// shows that it comes from the setting, not from the request. The endpoint matrix covers who may call it.
/// </summary>
public sealed class PasskeyOptionsEndpointTests : IAsyncDisposable
{
    private const string Path = "/api/auth/passkey/options";

    private readonly AppWithoutDatabase host = new("Production");
    private readonly WebApplicationFactory<Program> app;

    public PasskeyOptionsEndpointTests() => app = host.WithWebHostBuilder(builder => builder
        .KeepKeysInMemory()
        .ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(
            [new("App:Origin", Grow2NotesFactory.Origin)])));

    // A discoverable-credential request: with no credentials listed, the browser offers any passkey it holds for the
    // domain, so nobody gives an email first. The prompt waits 5 minutes, never less (passkey-flows.md).
    [Fact]
    public async Task Answers_options_as_JSON_for_any_passkey_of_the_app_s_domain_with_user_verification_required()
    {
        using var client = app.CreateHttpsClient();

        using var options = await PostForOptionsAsync(client);

        var root = options.RootElement;
        Assert.Equal("grow2notes.example", root.GetProperty("rpId").GetString());
        Assert.Equal("required", root.GetProperty("userVerification").GetString());
        Assert.Empty(root.GetProperty("allowCredentials").EnumerateArray());
        Assert.Equal(300_000, root.GetProperty("timeout").GetInt32());
    }

    // Setup's creation options, which this endpoint does not make, are the ones that ask for a discoverable credential,
    // so that setting is read from the options themselves.
    [Fact]
    public void Passkeys_are_user_verified_and_discoverable_and_belong_to_App_Origin_s_host()
    {
        var options = app.Services.GetRequiredService<IOptions<IdentityPasskeyOptions>>().Value;

        Assert.Equal("grow2notes.example", options.ServerDomain);
        Assert.Equal("required", options.UserVerificationRequirement);
        Assert.Equal("required", options.ResidentKeyRequirement);
    }

    // WebAuthn asks for a random challenge of at least 16 bytes, new for each ceremony, so an assertion signed for one
    // cannot be replayed in another.
    [Fact]
    public async Task Each_answer_holds_a_challenge_of_its_own()
    {
        using var client = app.CreateHttpsClient();

        using var first = await PostForOptionsAsync(client);
        using var second = await PostForOptionsAsync(client);

        var challenge = first.RootElement.GetProperty("challenge").GetString();
        Assert.NotNull(challenge);
        Assert.True(Base64Url.DecodeFromChars(challenge).Length >= 16);
        Assert.NotEqual(challenge, second.RootElement.GetProperty("challenge").GetString());
    }

    // Identity keeps the ceremony's state, the challenge included, in its two-factor cookie, for the sign-in to check
    // the credential against.
    [Fact]
    public async Task Is_never_stored_and_sets_the_ceremony_s_cookie()
    {
        using var client = app.CreateHttpsClient();
        var token = await client.FetchTokenAsync();

        using var response = await PostAsync(client, token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        var ceremonyCookie = app.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(IdentityConstants.TwoFactorUserIdScheme).Cookie.Name;
        var cookie = SetCookieHeaderValue.Parse(Assert.Single(response.Headers.GetValues(HeaderNames.SetCookie)));
        Assert.Equal(ceremonyCookie, cookie.Name.ToString());
        Assert.False(string.IsNullOrEmpty(cookie.Value.ToString()));
        Assert.True(cookie.HttpOnly);
    }

    // Disposing the factory the app was derived from disposes the app too.
    public ValueTask DisposeAsync() => host.DisposeAsync();

    // The body is the options object itself, as SimpleWebAuthn's startAuthentication takes it, not a JSON string.
    private static async Task<JsonDocument> PostForOptionsAsync(HttpClient client)
    {
        using var response = await PostAsync(client, await client.FetchTokenAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var options = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(JsonValueKind.Object, options.RootElement.ValueKind);
        return options;
    }

    // With no body, as the SPA sends it.
    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Path);
        request.Headers.Add(AntiforgeryTokens.Header, token);
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }
}
