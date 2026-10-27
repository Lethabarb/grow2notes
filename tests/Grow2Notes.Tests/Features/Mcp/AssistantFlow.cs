using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Grow2Notes.Tests.Features.Mcp;

/// <summary>
/// An approved assistant connecting to the app, and using its tokens, for the tests of the token endpoint
/// (mcp-server.md §4.2): the app with the access on, on a database of its own and a fake clock, with the people that
/// connect it signed in by the real session cookie. Each step is what the assistant or its person does: the person
/// presses Allow, the assistant exchanges the code, then refreshes.
/// </summary>
internal sealed class AssistantFlow : IAsyncDisposable
{
    public const string ClientId = "https://assistant.example/client.json";
    public const string RedirectUri = "https://assistant.example/callback";

    private readonly WebApplicationFactory<Program> databaseOfItsOwn;
    private readonly Guid organisationId;

    private AssistantFlow(
        WebApplicationFactory<Program> databaseOfItsOwn,
        WebApplicationFactory<Program> app,
        FakeTimeProvider clock,
        Guid organisationId)
    {
        this.databaseOfItsOwn = databaseOfItsOwn;
        App = app;
        Clock = clock;
        this.organisationId = organisationId;
    }

    public WebApplicationFactory<Program> App { get; }

    public FakeTimeProvider Clock { get; }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string ValidQuery => McpTestApp.AuthorizeQuery(ClientId, RedirectUri);

    public static async Task<AssistantFlow> StartAsync(
        Grow2NotesFactory factory, SqlServerFixture sqlServer, string name)
    {
        var databaseOfItsOwn = await factory.OnDatabaseOfItsOwnAsync(sqlServer, name);
        var clock = new FakeTimeProvider(TestClock.Start);
        var app = databaseOfItsOwn.WithWebHostBuilder(builder => builder.MapCookieSignIn().UseClock(clock));
        await app.RegisterClientAsync(ClientId, applicationType: null, RedirectUri);

        // An organisation is the tenant, not tenant-owned, so this needs no tenant.
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        var organisation = new Organisation
        {
            Name = "Token test",
            GuidePrompts = "",
            CreatedAtUtc = new DateTime(2026, 10, 18, 0, 0, 0, DateTimeKind.Utc),
        };
        db.Organisations.Add(organisation);
        await db.SaveChangesAsync(Ct);
        return new(databaseOfItsOwn, app, clock, organisation.Id);
    }

    public static async Task<AssistantTokenSet> ReadTokensAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        return new(
            body.GetProperty("access_token").GetString()!,
            body.GetProperty("refresh_token").GetString()!,
            body.GetProperty("expires_in").GetInt32());
    }

    /// <summary>The OpenIddict error code that <paramref name="response"/> carries, once it is a 400.</summary>
    public static async Task<string?> ErrorAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        return body.GetProperty("error").GetString();
    }

    public async ValueTask DisposeAsync()
    {
        await App.DisposeAsync();
        await databaseOfItsOwn.DisposeAsync();
    }

    // AspNetUsers is not filtered by tenant, so this needs none.
    public async Task<Guid> AddUserAsync(UserRole role)
    {
        var addedAtUtc = new DateTime(2026, 10, 18, 0, 0, 0, DateTimeKind.Utc);
        var email = $"{Guid.NewGuid():N}@example.org";
        var user = new ApplicationUser
        {
            OrganisationId = organisationId,
            DisplayName = "Sam Lee",
            Role = role,
            Status = UserStatus.Active,
            InvitedAtUtc = addedAtUtc,
            ActivatedAtUtc = addedAtUtc,
            UserName = email,
            Email = email,
            EmailConfirmed = true,
        };
        await using var scope = App.Services.CreateAsyncScope();
        var created = await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().CreateAsync(user);
        Assert.True(created.Succeeded, string.Join(" ", created.Errors.Select(error => error.Code)));
        return user.Id;
    }

    /// <summary>A browser signed in as <paramref name="userId"/> at the clock's time now.</summary>
    public async Task<HttpClient> SignInAsync(Guid userId)
    {
        var client = App.CreateAssistantClient();
        await client.SignInWithCookieAsync(userId);
        return client;
    }

    /// <summary>
    /// The person at <paramref name="browser"/> presses Allow on the request that <paramref name="query"/> makes (the
    /// valid one by default), and the code they are sent back with.
    /// </summary>
    public async Task<string> AllowAsync(HttpClient browser, string? query = null)
    {
        const string prefix = "/connect?request=";
        using var handOff = await browser.GetAsync("/oauth/authorize?" + (query ?? ValidQuery), Ct);
        Assert.Equal(HttpStatusCode.Redirect, handOff.StatusCode);
        var location = Assert.IsType<Uri>(handOff.Headers.Location).OriginalString;
        Assert.StartsWith(prefix, location, StringComparison.Ordinal);

        using var answer = await browser.PostWithTokenAsync(
            "/api/connect",
            await browser.FetchTokenAsync(),
            new { request = location[prefix.Length..], allow = true });
        Assert.Equal(HttpStatusCode.OK, answer.StatusCode);
        var next = (await answer.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("next").GetString()!;

        using var sentBack = await browser.GetAsync(next, Ct);
        Assert.Equal(HttpStatusCode.Redirect, sentBack.StatusCode);
        var returned = QueryHelpers.ParseQuery(Assert.IsType<Uri>(sentBack.Headers.Location).Query);
        return returned["code"].ToString();
    }

    /// <summary>The assistant exchanges <paramref name="code"/>, with the person's PKCE verifier.</summary>
    public Task<HttpResponseMessage> ExchangeAsync(
        string code, bool withResource = true, string? clientSecret = null) =>
        PostFormAsync(
            "/oauth/token",
            ("grant_type", GrantTypes.AuthorizationCode),
            ("code", code),
            ("redirect_uri", RedirectUri),
            ("client_id", ClientId),
            ("code_verifier", McpTestApp.CodeVerifier),
            ("resource", withResource ? McpTestApp.Resource : null),
            ("client_secret", clientSecret));

    /// <summary>The assistant refreshes with <paramref name="refreshToken"/>, naming no resource unless asked.</summary>
    public Task<HttpResponseMessage> RefreshAsync(string refreshToken, bool withResource = false) =>
        PostFormAsync(
            "/oauth/token",
            ("grant_type", GrantTypes.RefreshToken),
            ("refresh_token", refreshToken),
            ("client_id", ClientId),
            ("resource", withResource ? McpTestApp.Resource : null));

    /// <summary>The assistant revokes <paramref name="token"/>, as a public client, with no secret.</summary>
    public Task<HttpResponseMessage> RevokeAsync(string token, string hint) =>
        PostFormAsync("/oauth/revoke", ("token", token), ("token_type_hint", hint), ("client_id", ClientId));

    /// <summary>The person allows, and the assistant exchanges the code: the tokens of a connection.</summary>
    public async Task<AssistantTokenSet> ConnectAsync(HttpClient browser)
    {
        using var response = await ExchangeAsync(await AllowAsync(browser));
        return await ReadTokensAsync(response);
    }

    /// <summary>The status of each authorisation that OpenIddict holds for <paramref name="userId"/>.</summary>
    public async Task<IReadOnlyList<string?>> AuthorisationStatusesAsync(Guid userId)
    {
        await using var scope = App.Services.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictAuthorizationManager>();
        var statuses = new List<string?>();
        await foreach (var authorisation in manager.FindBySubjectAsync(userId.ToString("D"), Ct))
        {
            statuses.Add(await manager.GetStatusAsync(authorisation, Ct));
        }

        return statuses;
    }

    public Task DeactivateAsync(Guid userId) => ChangeAsync(userId, async (users, user) =>
    {
        user.Status = UserStatus.Deactivated;
        await users.UpdateAsync(user);
        await users.UpdateSecurityStampAsync(user);
    });

    public Task ResetSignInAsync(Guid userId) =>
        ChangeAsync(userId, (users, user) => users.UpdateSecurityStampAsync(user));

    public Task ChangeRoleAsync(Guid userId, UserRole role) => ChangeAsync(userId, (users, user) =>
    {
        user.Role = role;
        return users.UpdateAsync(user);
    });

    /// <summary>The person signs out of the app, which changes their security stamp (design.md §8.5).</summary>
    public async Task SignOutOfTheAppAsync(HttpClient browser)
    {
        using var response = await browser.PostWithTokenAsync("/api/auth/logout", await browser.FetchTokenAsync());
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private async Task ChangeAsync(Guid userId, Func<UserManager<ApplicationUser>, ApplicationUser, Task> change)
    {
        await using var scope = App.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByIdAsync(userId.ToString("D"));
        Assert.NotNull(user);
        await change(users, user);
    }

    private async Task<HttpResponseMessage> PostFormAsync(string path, params (string Name, string? Value)[] fields)
    {
        using var client = App.CreateAssistantClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new FormUrlEncodedContent(fields
                .Where(field => field.Value is not null)
                .Select(field => new KeyValuePair<string, string>(field.Name, field.Value!))),
        };
        return await client.SendAsync(request, Ct);
    }
}

/// <summary>What the token endpoint gave an assistant.</summary>
internal sealed record AssistantTokenSet(string AccessToken, string RefreshToken, int ExpiresIn);
