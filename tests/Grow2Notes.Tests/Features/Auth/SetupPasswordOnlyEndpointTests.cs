using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Features.Users;
using Grow2Notes.Web.Platform.Audit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

namespace Grow2Notes.Tests.Features.Auth;

/// <summary>
/// <c>POST /api/auth/setup/password-only</c> (design.md §6.2, §8.1 steps 4 and 5, D97), called as the SPA calls it:
/// signed out, over HTTPS, after <c>POST /api/auth/setup/start</c> with a link of the test's own
/// (<see cref="SetupLinks"/>), with the one antiforgery token that the SPA fetches before the start, and with the
/// password as typed. The app is hosted on a fake clock, and its log entries are recorded. The endpoint matrix covers
/// callers with no enrolment cookie, and <see cref="SetupPasswordEndpointTests"/> the password path with the app,
/// which this one leaves as it was.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class SetupPasswordOnlyEndpointTests : IClassFixture<Grow2NotesFactory>, IAsyncDisposable
{
    private const string Path = "/api/auth/setup/password-only";

    // "crèmebrûlées": exactly 12 lowercase letters, accented ones among them, which only design.md §8.4's rule accepts.
    private const string Password = "crèmebrûlées";

    private readonly FakeTimeProvider clock = new(TestClock.Start);
    private readonly RecordingLoggerProvider logs = new();
    private readonly Grow2NotesFactory factory;
    private readonly SeededOrganisations seeded;
    private readonly WebApplicationFactory<Program> app;

    public SetupPasswordOnlyEndpointTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    {
        this.factory = factory;
        seeded = sqlServer.Seeded;
        app = factory.WithWebHostBuilder(
            builder => builder.UseClock(clock).ConfigureLogging(logging => logging.AddProvider(logs)));
    }

    // The password is the user's one way to sign in, with two-factor off and no passkey: the (c) combination. The
    // person takes two minutes to type it, so the activation is completion's time. The rotated stamp ends the link.
    [Fact]
    public async Task Completion_adds_only_the_password_activates_the_user_ends_the_link_and_writes_one_event()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var link = await InviteAsync();
        var stamp = await factory.SecurityStampAsync(link.UserId);
        using var client = app.CreateHttpsClient();
        var (token, _) = await client.StartSetupAsync(link);
        clock.Advance(TimeSpan.FromMinutes(2));

        using (var response = await CompleteAsync(client, token))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
            Assert.Contains(response.SetCookies(), cookie => cookie.Name.Value == CookieSignIn.SessionCookie);
            var me = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
            Assert.Equal(link.UserId, me.GetProperty("userId").GetString());
        }

        var after = await ReadAsync(link);
        Assert.True(after.HasPassword);
        Assert.False(after.User.TwoFactorEnabled);
        Assert.Equal(0, after.Passkeys);
        Assert.Equal(UserStatus.Active, after.User.Status);
        Assert.Equal(TestClock.Start.AddMinutes(2).UtcDateTime, after.User.ActivatedAtUtc);
        Assert.True(after.User.EmailConfirmed);
        Assert.NotEqual(stamp, after.User.SecurityStamp);
        Assert.Equal([AuditEventTypes.Auth.SetupCompleted], after.EventTypes);
        using var again = app.CreateHttpsClient();
        await again.AssertSetupLinkEndedAsync(link);
    }

    // §8.1's second-sign-in test for the password only: the password alone signs the person in.
    [Fact]
    public async Task After_completion_the_password_alone_signs_the_person_in()
    {
        var link = await InviteAsync();
        using (var client = app.CreateHttpsClient())
        {
            var (token, _) = await client.StartSetupAsync(link);
            using var completion = await CompleteAsync(client, token);
            Assert.Equal(HttpStatusCode.OK, completion.StatusCode);
        }

        using var signIn = app.CreateHttpsClient();
        using var response = await signIn.SendPasswordAsync(link.Email, Password);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var me = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(link.UserId, me.GetProperty("userId").GetString());
        Assert.Contains(response.SetCookies(), cookie => cookie.Name.Value == CookieSignIn.SessionCookie);
        Assert.Equal(
            [AuditEventTypes.Auth.SetupCompleted, AuditEventTypes.Auth.SignInSucceeded],
            (await ReadAsync(link)).EventTypes);
    }

    // With no body, which the status code pages make problem details with no design.md §6.9 code.
    [Fact]
    public async Task Completion_without_the_enrolment_cookie_gets_401_and_changes_nothing()
    {
        var link = await InviteAsync();
        var stamp = await factory.SecurityStampAsync(link.UserId);
        using var client = app.CreateHttpsClient();

        using var response = await CompleteAsync(client, await client.FetchTokenAsync());

        await response.ReadProblemAsync(HttpStatusCode.Unauthorized);
        await AssertNothingChangedAsync(link, stamp);
    }

    // Twelve characters is the minimum for every method; twelve spaces is a password of only whitespace, which the
    // built-in validation refuses as [Required] before Identity would; and a body without the password is refused too.
    [Theory]
    [InlineData("elevenchars")]
    [InlineData("            ")]
    [InlineData(null)]
    public async Task A_short_whitespace_or_missing_password_gets_422_for_the_password_and_changes_nothing(
        string? password)
    {
        var link = await InviteAsync();
        var stamp = await factory.SecurityStampAsync(link.UserId);
        using var client = app.CreateHttpsClient();
        var (token, _) = await client.StartSetupAsync(link);

        object body = password is null ? new { } : new { password };

        using var response = await client.PostWithTokenAsync(Path, token, body);

        using var problem = JsonDocument.Parse(await response.ReadProblemAsync(HttpStatusCode.UnprocessableEntity));
        Assert.Equal("validation.failed", problem.RootElement.GetProperty("code").GetString());
        Assert.Equal(
            ["password"], problem.RootElement.GetProperty("errors").EnumerateObject().Select(error => error.Name));
        Assert.DoesNotContain(response.SetCookies(), cookie => cookie.Name.Value == CookieSignIn.SessionCookie);
        await AssertNothingChangedAsync(link, stamp);
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
        var stamp = await factory.SecurityStampAsync(link.UserId);
        using var client = failing.CreateHttpsClient();
        var (token, _) = await client.StartSetupAsync(link);

        using var response = await CompleteAsync(client, token);

        await response.ReadProblemAsync(HttpStatusCode.InternalServerError);
        Assert.DoesNotContain(response.SetCookies(), cookie => cookie.Name.Value == CookieSignIn.SessionCookie);
        await AssertNothingChangedAsync(link, stamp);
    }

    // Two devices finish setup from one link at once: the laptop's transaction loads the user, and the phone's
    // completion commits before the laptop's first save, whose concurrency check then matches no row. Exactly one
    // completion stands, with one event, and the other throws (SetupPasswordEndpointTests).
    [Fact]
    public async Task Two_completions_at_once_commit_exactly_one_and_write_one_event()
    {
        var link = await InviteAsync();
        var gate = new HoldsTheFirstSaveOfTheUser(Guid.Parse(link.UserId));
        await using var gated = gate.On(app);
        using var laptop = gated.CreateHttpsClient();
        using var phone = gated.CreateHttpsClient();
        var (laptopToken, _) = await laptop.StartSetupAsync(link);
        var (phoneToken, _) = await phone.StartSetupAsync(link);

        var laptopCompletion = CompleteAsync(laptop, laptopToken);
        try
        {
            await gate.Reached.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            using var phoneCompletion = await CompleteAsync(phone, phoneToken);
            Assert.Equal(HttpStatusCode.OK, phoneCompletion.StatusCode);
        }
        finally
        {
            gate.Release();
        }

        using var response = await laptopCompletion;

        await response.ReadProblemAsync(HttpStatusCode.InternalServerError);
        Assert.DoesNotContain(response.SetCookies(), cookie => cookie.Name.Value == CookieSignIn.SessionCookie);
        Assert.Equal([AuditEventTypes.Auth.SetupCompleted], (await ReadAsync(link)).EventTypes);
    }

    // A manager's reset takes away whatever the person chose (§8.6), the password only included, and the new link
    // starts setup again with any of the choices.
    [Fact]
    public async Task After_a_manager_resets_a_person_sign_in_a_password_only_can_be_set_up_again()
    {
        var link = await InviteAsync();
        using (var first = app.CreateHttpsClient())
        {
            var (token, _) = await first.StartSetupAsync(link);
            using var completion = await CompleteAsync(first, token);
            Assert.Equal(HttpStatusCode.OK, completion.StatusCode);
        }

        var newLink = await ResetAsync(link);
        Assert.False((await ReadAsync(link)).HasPassword);
        using var client = app.CreateHttpsClient();
        var (newToken, _) = await client.StartSetupAsync(newLink);

        using var response = await CompleteAsync(client, newToken, "another crème");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var after = await ReadAsync(link);
        Assert.True(after.HasPassword);
        Assert.False(after.User.TwoFactorEnabled);
        Assert.Equal(UserStatus.Active, after.User.Status);
    }

    // design.md §9.5: no log entry holds a secret, a refused password included. Setup logs nothing at the app's levels,
    // so a log that stays empty passes.
    [Fact]
    public async Task No_log_entry_holds_the_password()
    {
        var link = await InviteAsync();
        const string refused = "too short";
        using var client = app.CreateHttpsClient();
        var (token, _) = await client.StartSetupAsync(link);
        using (var refusal = await CompleteAsync(client, token, refused))
        {
            Assert.Equal(HttpStatusCode.UnprocessableEntity, refusal.StatusCode);
        }

        using (var completion = await CompleteAsync(client, token))
        {
            Assert.Equal(HttpStatusCode.OK, completion.StatusCode);
        }

        Assert.All(logs.Entries, entry =>
        {
            string?[] logged =
            [
                entry.Message,
                entry.Exception?.ToString(),
                .. entry.Values.Select(value => Convert.ToString(value.Value, CultureInfo.InvariantCulture)),
            ];
            Assert.DoesNotContain(
                logged,
                text => text is not null
                    && (text.Contains(Password, StringComparison.Ordinal)
                        || text.Contains(refused, StringComparison.Ordinal)));
        });
    }

    public ValueTask DisposeAsync() => app.DisposeAsync();

    // As the SPA sends it: the password as typed.
    private static Task<HttpResponseMessage> CompleteAsync(
        HttpClient client, string token, string password = Password) =>
        client.PostWithTokenAsync(Path, token, new { password });

    // As the invite left the user: Invited, with no password, two-factor sign-in off, the stamp that the link holds and
    // no event; so the link still starts setup.
    private async Task AssertNothingChangedAsync(SetupLink link, string? stamp)
    {
        var after = await ReadAsync(link);
        Assert.Equal(UserStatus.Invited, after.User.Status);
        Assert.Null(after.User.ActivatedAtUtc);
        Assert.False(after.User.EmailConfirmed);
        Assert.False(after.HasPassword);
        Assert.False(after.User.TwoFactorEnabled);
        Assert.Equal(stamp, after.User.SecurityStamp);
        Assert.Empty(after.EventTypes);
        using var client = app.CreateHttpsClient();
        await client.StartSetupAsync(link);
    }

    private Task<SetupLink> InviteAsync() => factory.InviteAsync(seeded.A);

    // As a manager's Reset sign-in does, through the reset step, which the endpoint tests of Reset sign-in cover.
    private async Task<SetupLink> ResetAsync(SetupLink link)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByIdAsync(link.UserId);
        Assert.NotNull(user);
        var setupLink = await scope.ServiceProvider.GetRequiredService<SignInReset>().ResetAsync(user);
        Assert.NotNull(setupLink);
        return SetupLinks.Read(setupLink, link.Email);
    }

    // Read afresh, in a scope of its own, and the events in SQL, so that neither the tenant filter nor a tracked row
    // decides what is seen. The invite core writes no event, so every event about the user is completion's or a
    // sign-in's.
    private async Task<Account> ReadAsync(SetupLink link)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByIdAsync(link.UserId);
        Assert.NotNull(user);
        var eventTypes = await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Database
            .SqlQuery<string>($"""
                SELECT [EventType] AS [Value] FROM [AuditEvent] WHERE [EntityId] = {user.Id} ORDER BY [Id]
                """)
            .ToListAsync(TestContext.Current.CancellationToken);
        return new(user, await users.HasPasswordAsync(user), (await users.GetPasskeysAsync(user)).Count, eventTypes);
    }

    // The user's row, whether they have a password, how many passkeys they have, and the types of the events about
    // them.
    private sealed record Account(ApplicationUser User, bool HasPassword, int Passkeys, List<string> EventTypes);

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
}
