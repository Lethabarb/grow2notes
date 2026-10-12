using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Grow2Notes.Tests.Features.Auth;

/// <summary>
/// <c>POST /api/auth/setup/password</c> (design.md §6.2, §8.1 steps 4 and 5), called as the SPA calls it: signed out,
/// over HTTPS, after <c>POST /api/auth/setup/start</c> with a link of the test's own (<see cref="SetupLinks"/>), with
/// the one antiforgery token that the SPA fetches before the start, and with a password and a code from the key that
/// the invite stored, computed on the real clock (<see cref="AuthenticatorCodes"/>). The app is hosted on a fake clock,
/// and gives each request a client address, which the test server does not. The endpoint matrix covers callers with no
/// enrolment cookie, and <see cref="SetupPasskeyOptionsEndpointTests"/> the enrolment check that every setup step after
/// the start makes.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class SetupPasswordEndpointTests : IClassFixture<Grow2NotesFactory>, IAsyncDisposable
{
    private const string Path = "/api/auth/setup/password";

    // A documentation address (RFC 5737), outside App Service's front-end networks, so the app keeps it as the
    // client's.
    private const string ClientAddress = "203.0.113.7";

    // "crèmebrûlées": exactly 12 lowercase letters, accented ones among them, with no digit, capital or symbol, which
    // Identity's default options would refuse, so it is accepted only with design.md §8.4's.
    private const string Password = "cr\u00E8mebr\u00FBl\u00E9es";

    private readonly FakeTimeProvider clock = new(TestClock.Start);
    private readonly Grow2NotesFactory factory;
    private readonly SeededOrganisations seeded;
    private readonly WebApplicationFactory<Program> app;

    public SetupPasswordEndpointTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    {
        this.factory = factory;
        seeded = sqlServer.Seeded;
        app = factory.WithWebHostBuilder(builder => builder
            .UseClock(clock)
            .ConfigureServices(services => services.AddSingleton<IStartupFilter>(new FromClientAddress())));
    }

    // Identity's composition rules count only ASCII letters and digits as letters and digits, so an accented letter is
    // a symbol to them. So with the password above, which has no digit or capital, these pin the other two rules off:
    // twelve ASCII lowercase letters, which the symbol rule would refuse, and twelve accented ones, "éèêëàâîïôûùü",
    // which the lowercase rule would.
    public static TheoryData<string> OtherAcceptedPasswords { get; } =
        new("applecrumble", "\u00E9\u00E8\u00EA\u00EB\u00E0\u00E2\u00EE\u00EF\u00F4\u00FB\u00F9\u00FC");

    // Its first 11 letters, which pin RequiredLength at 12, and twelve spaces, a password of only whitespace, which the
    // built-in validation refuses as [Required] before Identity would.
    public static TheoryData<string> RefusedPasswords { get; } = new(Password[..11], new string(' ', 12));

    // The clock starts at 13:30 UTC on 9 October, which is half past midnight on 10 October in Melbourne.
    [Fact]
    public async Task Completion_answers_the_user_s_Me_and_signs_them_in_with_the_session_cookie()
    {
        var link = await InviteAsync();
        using var client = app.CreateHttpsClient();
        var (token, _) = await client.StartSetupAsync(link);

        using var response = await CompleteAsync(client, token, AuthenticatorCodes.Now(await KeyAsync(link)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        var me = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(
            ["userId", "displayName", "role", "organisationName", "today"],
            me.EnumerateObject().Select(property => property.Name));
        Assert.Equal(link.UserId, me.GetProperty("userId").GetString());
        Assert.Equal(SetupLinks.DisplayName, me.GetProperty("displayName").GetString());
        Assert.Equal("Worker", me.GetProperty("role").GetString());
        Assert.Equal(
            await OrganisationNameAsync(seeded.A.OrganisationId), me.GetProperty("organisationName").GetString());
        Assert.Equal("2026-10-10", me.GetProperty("today").GetString());
        Assert.Contains(response.SetCookies(), cookie => cookie.Name.Value == CookieSignIn.SessionCookie);

        // A second later, so that the session check compares the cookie's security stamp with the row's, which
        // completion rotated (tests/README.md).
        clock.Advance(TimeSpan.FromSeconds(1));
        using var meAgain = await client.GetAsync("/api/auth/me", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, meAgain.StatusCode);
        var session = await meAgain.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(link.UserId, session.GetProperty("userId").GetString());
    }

    // The person takes two minutes with their authenticator app, so the activation is completion's time, not the
    // start's. The key is the user's one row in AspNetUserTokens, where Identity would keep recovery codes too.
    [Fact]
    public async Task Completion_adds_the_password_and_two_factor_sign_in_activates_the_user_and_writes_the_event()
    {
        var link = await InviteAsync();
        var before = await ReadAsync(link);
        using var client = app.CreateHttpsClient();
        var (token, _) = await client.StartSetupAsync(link);
        clock.Advance(TimeSpan.FromMinutes(2));

        using (var response = await CompleteAsync(client, token, AuthenticatorCodes.Now(before.Key)))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        var after = await ReadAsync(link);
        var completedAtUtc = TestClock.Start.AddMinutes(2).UtcDateTime;
        Assert.True(after.HasPassword);
        Assert.True(after.User.TwoFactorEnabled);
        Assert.Equal(before.Key, after.Key);
        Assert.Equal(0, after.Passkeys);
        Assert.Equal(0, after.RecoveryCodes);
        Assert.Equal([("[AspNetUserStore]", "AuthenticatorKey")], after.Tokens);
        Assert.Equal(UserStatus.Active, after.User.Status);
        Assert.Equal(completedAtUtc, after.User.ActivatedAtUtc);
        Assert.True(after.User.EmailConfirmed);
        Assert.NotEqual(before.User.SecurityStamp, after.User.SecurityStamp);
        Assert.Equal(
            new StoredEvent(
                seeded.A.OrganisationId, completedAtUtc, after.User.Id, AuditEventTypes.Auth.SetupCompleted,
                AuditEntityTypes.User, after.User.Id, ParticipantId: null, Details: null, ClientAddress),
            Assert.Single(after.Events));
    }

    // The rotated stamp ends the link, started afresh, as from another tab. A second completion is as after a double
    // tap: the SPA fetches a token for the session that completion started, and the browser still holds the enrolment
    // cookie, whose user is now Active. Nothing is written twice.
    [Fact]
    public async Task After_completion_the_same_link_gets_410_and_a_second_completion_401()
    {
        var link = await InviteAsync();
        var key = await KeyAsync(link);
        using var client = app.CreateHttpsClient();
        var (token, _) = await client.StartSetupAsync(link);
        using (var first = await CompleteAsync(client, token, AuthenticatorCodes.Now(key)))
        {
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        }

        using var second = await CompleteAsync(client, await client.FetchTokenAsync(), AuthenticatorCodes.Now(key));

        await second.ReadProblemAsync(HttpStatusCode.Unauthorized);
        Assert.Equal("no-store", second.Headers.CacheControl?.ToString());
        Assert.Single((await ReadAsync(link)).Events);
        using var again = app.CreateHttpsClient();
        using var start = await again.PostWithTokenAsync(
            SetupLinks.StartPath, await again.FetchTokenAsync(), new { userId = link.UserId, token = link.Token });
        using var problem = JsonDocument.Parse(await start.ReadProblemAsync(HttpStatusCode.Gone));
        Assert.Equal("auth.setup_link_invalid", problem.RootElement.GetProperty("code").GetString());
    }

    // design.md §8.1's invariant: a password alone signs no one in. The password step (S00.04.05), with the password,
    // asks for the code, so no session starts.
    [Fact]
    public async Task After_completion_a_password_sign_in_requires_the_code_and_starts_no_session()
    {
        var link = await InviteAsync();
        using (var client = app.CreateHttpsClient())
        {
            var (token, _) = await client.StartSetupAsync(link);
            using var completion = await CompleteAsync(client, token, AuthenticatorCodes.Now(await KeyAsync(link)));
            Assert.Equal(HttpStatusCode.OK, completion.StatusCode);
        }

        using var signIn = app.CreateHttpsClient();
        using var response = await signIn.SendPasswordAsync(link.Email, Password);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            """{"next":"totp"}""",
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.DoesNotContain(response.SetCookies(), cookie => cookie.Name.Value == CookieSignIn.SessionCookie);
    }

    [Theory]
    [MemberData(nameof(OtherAcceptedPasswords))]
    public async Task A_password_of_any_12_letters_is_accepted(string password)
    {
        var link = await InviteAsync();
        using var client = app.CreateHttpsClient();
        var (token, _) = await client.StartSetupAsync(link);

        using var response = await CompleteAsync(client, token, AuthenticatorCodes.Now(await KeyAsync(link)), password);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // As people group the digits as the app shows them, and as a phone keyboard set to another script types them.
    [Theory]
    [InlineData("spaced")]
    [InlineData("hyphenated")]
    [InlineData("full-width")]
    public async Task A_code_typed_with_a_space_a_hyphen_or_full_width_digits_is_accepted(string form)
    {
        var link = await InviteAsync();
        using var client = app.CreateHttpsClient();
        var (token, _) = await client.StartSetupAsync(link);
        var code = AuthenticatorCodes.Now(await KeyAsync(link));
        var typed = form switch
        {
            "spaced" => $"{code[..3]} {code[3..]}",
            "hyphenated" => $"{code[..3]}-{code[3..]}",
            "full-width" => string.Concat(code.Select(digit => (char)(digit - '0' + '\uFF10'))),
            _ => throw new ArgumentOutOfRangeException(nameof(form), form, null),
        };

        using var response = await CompleteAsync(client, token, typed);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // The right key's code for a step ten minutes away, well outside the steps either side of now that Identity
    // accepts.
    [Fact]
    public Task A_wrong_code_gets_422_for_the_code_and_changes_nothing() =>
        AssertCodeRefusedAsync(key => new
        {
            password = Password,
            totpCode = AuthenticatorCodes.At(key, DateTimeOffset.UtcNow.AddMinutes(10)),
        });

    [Theory]
    [InlineData("12345")]
    [InlineData("abcdef")]
    public Task A_badly_formed_code_gets_422_for_the_code_and_changes_nothing(string totpCode) =>
        AssertCodeRefusedAsync(_ => new { password = Password, totpCode });

    // The built-in validation answers before the endpoint runs.
    [Fact]
    public Task A_body_without_the_code_gets_422_for_the_code_and_changes_nothing() =>
        AssertCodeRefusedAsync(_ => new { password = Password });

    [Theory]
    [MemberData(nameof(RefusedPasswords))]
    public async Task A_password_that_is_refused_gets_422_for_the_password_and_changes_nothing(string password)
    {
        var link = await InviteAsync();
        var before = await ReadAsync(link);
        using var client = app.CreateHttpsClient();
        var (token, _) = await client.StartSetupAsync(link);

        using var response = await CompleteAsync(client, token, AuthenticatorCodes.Now(before.Key), password);

        await AssertRefusedAsync(response, "password");
        await AssertNothingChangedAsync(link, before);
    }

    // The event is written, inside the transaction, before the writer throws, so only the rollback removes it, with
    // the password and the user's changes saved before it. The sign-in comes after the commit, so none starts.
    [Fact]
    public async Task With_an_audit_writer_that_throws_nothing_commits_and_no_session_starts()
    {
        await using var failing = app.WithWebHostBuilder(builder => builder.ConfigureTestServices(
            services => services.AddScoped<IAuditWriter>(
                provider => new FailsAfterWriting(ActivatorUtilities.CreateInstance<AuditWriter>(provider)))));
        var link = await InviteAsync();
        var before = await ReadAsync(link);
        using var client = failing.CreateHttpsClient();
        var (token, _) = await client.StartSetupAsync(link);

        using var response = await CompleteAsync(client, token, AuthenticatorCodes.Now(before.Key));

        await response.ReadProblemAsync(HttpStatusCode.InternalServerError);
        Assert.DoesNotContain(response.SetCookies(), cookie => cookie.Name.Value == CookieSignIn.SessionCookie);
        await AssertNothingChangedAsync(link, before);
    }

    // Two devices finish setup from one link at once, as two tabs or a phone and a laptop can: the laptop's transaction
    // loads the user, and the phone's completion commits before the laptop's first save, whose concurrency check then
    // matches no row. That is no refused password, so it throws, as the passkey path's refusal does, rolling the
    // laptop's transaction back, rather than send the laptop to view F with a password's message. The phone's stands.
    [Fact]
    public async Task A_completion_that_another_device_commits_first_gets_500_not_422_for_the_password()
    {
        var link = await InviteAsync();
        var gate = new HoldsTheFirstSaveInATransaction(Guid.Parse(link.UserId));
        await using var gated = app.WithWebHostBuilder(builder => builder.ConfigureTestServices(
            services => services.ConfigureDbContext<Grow2NotesDbContext>(options => options.AddInterceptors(gate))));
        var code = AuthenticatorCodes.Now(await KeyAsync(link));
        using var laptop = gated.CreateHttpsClient();
        using var phone = gated.CreateHttpsClient();
        var (laptopToken, _) = await laptop.StartSetupAsync(link);
        var (phoneToken, _) = await phone.StartSetupAsync(link);

        var laptopCompletion = CompleteAsync(laptop, laptopToken, code);
        try
        {
            await gate.Reached.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            using var phoneCompletion = await CompleteAsync(phone, phoneToken, code);
            Assert.Equal(HttpStatusCode.OK, phoneCompletion.StatusCode);
        }
        finally
        {
            gate.Release();
        }

        using var response = await laptopCompletion;

        await response.ReadProblemAsync(HttpStatusCode.InternalServerError);
        Assert.DoesNotContain(response.SetCookies(), cookie => cookie.Name.Value == CookieSignIn.SessionCookie);
        var after = await ReadAsync(link);
        Assert.Equal(UserStatus.Active, after.User.Status);
        Assert.True(after.HasPassword);
        Assert.True(after.User.TwoFactorEnabled);
        Assert.Single(after.Events);
    }

    // The request's own tenant is B's, from the session that the test-only sign-in gives it, so only a tenant taken
    // from the user that the enrolment cookie names puts the event in A.
    [Fact]
    public async Task Signed_in_as_B_s_manager_completing_A_s_user_s_setup_writes_the_event_in_A()
    {
        var link = await InviteAsync();
        using var client = app.CreateHttpsClient().SignInAs(seeded.B.ManagerId);
        var (token, _) = await client.StartSetupAsync(link);

        using (var response = await CompleteAsync(client, token, AuthenticatorCodes.Now(await KeyAsync(link))))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        Assert.Equal(seeded.A.OrganisationId, Assert.Single((await ReadAsync(link)).Events).OrganisationId);
    }

    // As view G does: the key fetched, and its code sent with the token that the antiforgery endpoint issued to nobody
    // before the start, which still matches the request's user, as the enrolment cookie never becomes it (S00.04.02's
    // Notes, from S00.01.04).
    [Fact]
    public async Task An_antiforgery_token_fetched_before_the_start_is_accepted_at_completion()
    {
        var link = await InviteAsync();
        using var client = app.CreateHttpsClient();
        var (tokenBeforeTheStart, _) = await client.StartSetupAsync(link);
        var authenticator = await client.GetFromJsonAsync<JsonElement>(
            "/api/auth/setup/authenticator", TestContext.Current.CancellationToken);
        var sharedKey = authenticator.GetProperty("sharedKey").GetString();
        Assert.NotNull(sharedKey);

        using var response = await CompleteAsync(client, tokenBeforeTheStart, AuthenticatorCodes.Now(sharedKey));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    public ValueTask DisposeAsync() => app.DisposeAsync();

    // As the SPA sends it: the password as typed, and the code.
    private static Task<HttpResponseMessage> CompleteAsync(
        HttpClient client, string token, string totpCode, string password = Password) =>
        client.PostWithTokenAsync(Path, token, new { password, totpCode });

    // A refusal keeps the person on the step, with no session, and names the one field.
    private static async Task AssertRefusedAsync(HttpResponseMessage response, string field)
    {
        using var problem = JsonDocument.Parse(await response.ReadProblemAsync(HttpStatusCode.UnprocessableEntity));
        Assert.Equal("validation.failed", problem.RootElement.GetProperty("code").GetString());
        Assert.Equal([field], problem.RootElement.GetProperty("errors").EnumerateObject().Select(error => error.Name));
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.DoesNotContain(response.SetCookies(), cookie => cookie.Name.Value == CookieSignIn.SessionCookie);
    }

    // Completes setup with a body made from the stored key, which gets 422 for totpCode and changes nothing.
    private async Task AssertCodeRefusedAsync(Func<string, object> body)
    {
        var link = await InviteAsync();
        var before = await ReadAsync(link);
        using var client = app.CreateHttpsClient();
        var (token, _) = await client.StartSetupAsync(link);

        using var response = await client.PostWithTokenAsync(Path, token, body(before.Key));

        await AssertRefusedAsync(response, "totpCode");
        await AssertNothingChangedAsync(link, before);
    }

    // As the invite left the user: Invited, with no password, two-factor sign-in off, the stamp that the link holds and
    // no event; so the link still starts setup.
    private async Task AssertNothingChangedAsync(SetupLink link, Account before)
    {
        var after = await ReadAsync(link);
        Assert.Equal(UserStatus.Invited, after.User.Status);
        Assert.Null(after.User.ActivatedAtUtc);
        Assert.False(after.User.EmailConfirmed);
        Assert.False(after.HasPassword);
        Assert.False(after.User.TwoFactorEnabled);
        Assert.Equal(before.User.SecurityStamp, after.User.SecurityStamp);
        Assert.Empty(after.Events);
        using var client = app.CreateHttpsClient();
        await client.StartSetupAsync(link);
    }

    private Task<SetupLink> InviteAsync() => factory.InviteAsync(seeded.A);

    private async Task<string> KeyAsync(SetupLink link) => (await ReadAsync(link)).Key;

    // Read afresh, in a scope of its own, and the events in SQL, so that neither the tenant filter nor a tracked row
    // decides what is seen. The invite core writes no event, so every event about the user is completion's.
    private async Task<Account> ReadAsync(SetupLink link)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        var user = await users.FindByIdAsync(link.UserId);
        Assert.NotNull(user);
        var key = await users.GetAuthenticatorKeyAsync(user);
        Assert.NotNull(key);
        var tokens = await db.UserTokens.Where(token => token.UserId == user.Id).ToListAsync(cancellationToken);
        var events = await db.Database.SqlQuery<StoredEvent>($"""
            SELECT [OrganisationId], [OccurredAtUtc], [ActorUserId], [EventType], [EntityType], [EntityId],
                   [ParticipantId], [Details], [IpAddress]
            FROM [AuditEvent]
            WHERE [EntityId] = {user.Id}
            """).ToListAsync(cancellationToken);
        return new(
            user,
            await users.HasPasswordAsync(user),
            key,
            (await users.GetPasskeysAsync(user)).Count,
            await users.CountRecoveryCodesAsync(user),
            [.. tokens.Select(token => (token.LoginProvider, token.Name))],
            events);
    }

    // An organisation is the tenant, not tenant-owned, so reading one needs no tenant.
    private async Task<string> OrganisationNameAsync(Guid organisationId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Organisations
            .Where(organisation => organisation.Id == organisationId)
            .Select(organisation => organisation.Name)
            .SingleAsync(TestContext.Current.CancellationToken);
    }

    // The user's row, whether they have a password, their authenticator key, how many passkeys and recovery codes they
    // have, their rows in AspNetUserTokens, and the events about them.
    private sealed record Account(
        ApplicationUser User,
        bool HasPassword,
        string Key,
        int Passkeys,
        int RecoveryCodes,
        List<(string LoginProvider, string Name)> Tokens,
        List<StoredEvent> Events);

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

    // Writes the event, then throws, so that the event and everything saved before it are in the transaction.
    private sealed class FailsAfterWriting(IAuditWriter writer) : IAuditWriter
    {
        public async Task WriteAsync(
            Guid organisationId,
            Guid? actorUserId,
            string eventType,
            string? entityType,
            Guid? entityId,
            Guid? participantId,
            JsonObject? details,
            CancellationToken cancellationToken = default)
        {
            await writer.WriteAsync(
                organisationId, actorUserId, eventType, entityType, entityId, participantId, details,
                cancellationToken);
            throw new InvalidOperationException("The audit writer failed after writing the event.");
        }
    }

    /// <summary>
    /// Holds back the first save, made inside a transaction, that changes the user whose ID it is given, until the test
    /// lets it go: in completion, the password's, made once the transaction has loaded the user. Every other save
    /// passes.
    /// </summary>
    private sealed class HoldsTheFirstSaveInATransaction(Guid userId) : SaveChangesInterceptor
    {
        private readonly TaskCompletionSource reached = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource released = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int held;

        public Task Reached => reached.Task;

        public void Release() => released.TrySetResult();

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context is { Database.CurrentTransaction: not null } context
                && context.ChangeTracker.Entries<ApplicationUser>().Any(
                    entry => entry.State == EntityState.Modified && entry.Entity.Id == userId)
                && Interlocked.Exchange(ref held, 1) == 0)
            {
                reached.SetResult();
                await released.Task.WaitAsync(cancellationToken);
            }

            return result;
        }
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
