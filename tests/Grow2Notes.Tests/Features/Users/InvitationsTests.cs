using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Features.Auth;
using Grow2Notes.Web.Features.Users;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using Microsoft.Extensions.Time.Testing;

namespace Grow2Notes.Tests.Features.Users;

/// <summary>
/// The invite core (design.md §8.1 steps 1 and 2), through the app's <see cref="Invitations"/>: the Invited user it
/// creates, their authenticator key, and the setup link. Each step runs in a scope of its own, as each request has one,
/// and reads the user afresh. Each test invites an address of its own into seeded organisation A, as tests/README.md's
/// seeded-rows rule asks.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class InvitationsTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    : IClassFixture<Grow2NotesFactory>
{
    private const string DisplayName = "Sam Taylor";

    private readonly SeededOrganisation organisation = sqlServer.Seeded.A;

    [Fact]
    public Task A_worker_invited_by_a_manager_is_created_Invited_with_no_password_at_the_clock_s_time() =>
        AssertCreatedInvitedAsync(UserRole.Worker, organisation.ManagerId);

    // As admin bootstrap invites the first manager.
    [Fact]
    public Task A_manager_invited_by_no_one_is_created_Invited_with_no_password_at_the_clock_s_time() =>
        AssertCreatedInvitedAsync(UserRole.Manager, invitedByUserId: null);

    [Fact]
    public async Task The_user_s_authenticator_key_is_in_AspNetUserTokens()
    {
        var user = await InviteAsync(factory.Services, NewAddress());

        await using var scope = factory.Services.CreateAsyncScope();
        var stored = Assert.Single(await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().UserTokens
            .Where(token => token.UserId == user.Id)
            .ToListAsync(TestContext.Current.CancellationToken));

        // Where Identity's user store keeps the key: under a login provider of its own, by the token's name.
        Assert.Equal(("[AspNetUserStore]", "AuthenticatorKey"), (stored.LoginProvider, stored.Name));
        Assert.False(string.IsNullOrEmpty(stored.Value));
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.Equal(stored.Value, await users.GetAuthenticatorKeyAsync(await FindUserAsync(users, user.Id)));
    }

    // The tests' own origin, and Development's, which has a port.
    [Theory]
    [InlineData(Grow2NotesFactory.Origin)]
    [InlineData("https://localhost:7107")]
    public async Task The_link_is_App_Origin_s_setup_path_with_the_user_s_ID_and_setup_token_in_its_fragment(
        string origin)
    {
        await using var app = factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration(
            configuration => configuration.AddInMemoryCollection([new("App:Origin", origin)])));
        var (user, setupLink) = await InviteWithLinkAsync(app.Services, NewAddress());

        var link = new Uri(setupLink);
        var expected = new Uri(origin);
        Assert.Equal((expected.Scheme, expected.Host, expected.Port), (link.Scheme, link.Host, link.Port));
        Assert.Equal("/setup", link.AbsolutePath);
        Assert.Empty(link.Query);

        // The token is escaped, so the fragment holds no + for the SPA's URLSearchParams to read as a space.
        Assert.DoesNotContain("+", link.Fragment, StringComparison.Ordinal);
        var fragment = ReadFragment(setupLink);
        Assert.Equal(["t", "u"], fragment.Keys.Order());
        Assert.Equal(user.Id.ToString("D"), fragment["u"].ToString());
        Assert.True(await VerifySetupTokenAsync(app.Services, user.Id, fragment["t"].ToString()));
    }

    [Fact]
    public async Task Reading_the_key_back_leaves_the_security_stamp_unchanged_and_the_setup_token_valid()
    {
        var (user, setupLink) = await InviteWithLinkAsync(factory.Services, NewAddress());
        var token = ReadFragment(setupLink)["t"].ToString();

        // As S00.04.03's setup steps will, to show the key as a QR code (design.md §8.1 step 4).
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var key = await users.GetAuthenticatorKeyAsync(await FindUserAsync(users, user.Id));
            Assert.False(string.IsNullOrEmpty(key));
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            Assert.Equal(user.SecurityStamp, (await FindUserAsync(users, user.Id)).SecurityStamp);
        }

        Assert.True(await VerifySetupTokenAsync(factory.Services, user.Id, token));
    }

    [Fact]
    public async Task An_address_already_in_use_is_refused_with_Identity_s_errors_and_nothing_is_created()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var email = NewAddress();
        var first = await InviteAsync(factory.Services, email);

        await using var scope = factory.Services.CreateAsyncScope();
        // In another letter case, because the address is unique in any.
        var result = await scope.ServiceProvider.GetRequiredService<Invitations>().InviteAsync(
            organisation.OrganisationId, "Alex Morgan", email.ToUpperInvariant(), UserRole.Worker,
            organisation.ManagerId);

        Assert.False(result.Succeeded);
        Assert.Null(result.User);
        Assert.Null(result.SetupLink);
        // The user name holds the address too, so Identity refuses it as a duplicate user name as well.
        Assert.Equal(
            [nameof(IdentityErrorDescriber.DuplicateUserName), nameof(IdentityErrorDescriber.DuplicateEmail)],
            result.Errors.Select(error => error.Code));

        // Nothing is saved, and nothing is left in the scope's context for a later save, such as the caller's audit
        // event, to write.
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        Assert.False(db.ChangeTracker.HasChanges());
        var normalizedEmail = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>()
            .NormalizeEmail(email);
        Assert.Equal(
            [first.Id],
            await db.Users.Where(user => user.NormalizedEmail == normalizedEmail)
                .Select(user => user.Id)
                .ToListAsync(cancellationToken));
    }

    [Fact]
    public async Task With_no_App_Origin_the_invite_stops_before_it_creates_anything()
    {
        await using var app = factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration(
            configuration => configuration.AddInMemoryCollection([new("App:Origin", "")])));
        var email = NewAddress();

        await using var scope = app.Services.CreateAsyncScope();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            scope.ServiceProvider.GetRequiredService<Invitations>().InviteAsync(
                organisation.OrganisationId, DisplayName, email, UserRole.Worker, organisation.ManagerId));

        Assert.StartsWith("App:Origin is not set", error.Message, StringComparison.Ordinal);
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.Null(await users.FindByEmailAsync(email));
    }

    private async Task AssertCreatedInvitedAsync(UserRole role, Guid? invitedByUserId)
    {
        var clock = new FakeTimeProvider(TestClock.Start);
        await using var app = factory.WithWebHostBuilder(builder => builder.UseClock(clock));
        var email = NewAddress();
        var user = await InviteAsync(app.Services, email, role, invitedByUserId);

        await using var scope = app.Services.CreateAsyncScope();
        var stored = await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Users
            .Where(row => row.Id == user.Id)
            .Select(row => new StoredUser(
                row.OrganisationId, row.DisplayName, row.Role, row.Status, row.InvitedAtUtc, row.InvitedByUserId,
                row.ActivatedAtUtc, row.UserName, row.Email, row.EmailConfirmed, row.PasswordHash))
            .SingleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(
            new StoredUser(
                organisation.OrganisationId, DisplayName, role, UserStatus.Invited, TestClock.Start.UtcDateTime,
                invitedByUserId, ActivatedAtUtc: null, UserName: email, Email: email, EmailConfirmed: false,
                PasswordHash: null),
            stored);
    }

    private async Task<ApplicationUser> InviteAsync(
        IServiceProvider services, string email, UserRole role = UserRole.Worker, Guid? invitedByUserId = null) =>
        (await InviteWithLinkAsync(services, email, role, invitedByUserId)).User;

    private async Task<(ApplicationUser User, string SetupLink)> InviteWithLinkAsync(
        IServiceProvider services, string email, UserRole role = UserRole.Worker, Guid? invitedByUserId = null)
    {
        await using var scope = services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<Invitations>().InviteAsync(
            organisation.OrganisationId, DisplayName, email, role, invitedByUserId);
        Assert.True(result.Succeeded, string.Join(" ", result.Errors.Select(error => error.Code)));
        return (result.User, result.SetupLink);
    }

    // An address that no other test or run of this one uses.
    private static string NewAddress() => $"sam.taylor.{Guid.NewGuid():N}@example.org";

    // As the SPA reads the fragment, with URLSearchParams, which ParseQuery matches: + is a space, then %XX is decoded.
    private static Dictionary<string, StringValues> ReadFragment(string setupLink) =>
        QueryHelpers.ParseQuery(setupLink[(setupLink.IndexOf('#', StringComparison.Ordinal) + 1)..]);

    private static async Task<bool> VerifySetupTokenAsync(IServiceProvider services, Guid userId, string token)
    {
        await using var scope = services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        return await users.VerifyUserTokenAsync(
            await FindUserAsync(users, userId), SetupTokenProvider.ProviderName, SetupTokenProvider.Purpose, token);
    }

    private static async Task<ApplicationUser> FindUserAsync(UserManager<ApplicationUser> users, Guid userId)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        Assert.NotNull(user);
        return user;
    }

    private sealed record StoredUser(
        Guid OrganisationId,
        string DisplayName,
        UserRole Role,
        UserStatus Status,
        DateTime InvitedAtUtc,
        Guid? InvitedByUserId,
        DateTime? ActivatedAtUtc,
        string? UserName,
        string? Email,
        bool EmailConfirmed,
        string? PasswordHash);
}
