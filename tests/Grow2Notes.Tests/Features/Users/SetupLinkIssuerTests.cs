using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Features.Auth;
using Grow2Notes.Web.Features.Users;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests.Features.Users;

/// <summary>
/// The setup-link step (design.md §8.1 steps 1 and 2), through the app's <see cref="SetupLinkIssuer"/>, for a user who
/// has a link already, as a resend and a sign-in reset have. Each test invites a user of its own into seeded
/// organisation A, as tests/README.md's seeded-rows rule asks, through the invite core, which takes the step too. Each
/// step runs in a scope of its own, as each request has one, and reads the user afresh.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class SetupLinkIssuerTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    : IClassFixture<Grow2NotesFactory>
{
    private readonly SeededOrganisation organisation = sqlServer.Seeded.A;

    [Fact]
    public async Task A_new_link_has_a_new_key_and_stamp_so_only_it_works_and_it_has_the_invite_s_form()
    {
        var first = await factory.InviteAsync(organisation);
        var before = await ReadAsync(first.UserId);

        var setupLink = await IssueAsync(factory, first.UserId);

        Assert.NotNull(setupLink);
        var after = await ReadAsync(first.UserId);
        Assert.NotEqual(before.SecurityStamp, after.SecurityStamp);
        Assert.NotEqual(before.AuthenticatorKey, after.AuthenticatorKey);

        Assert.StartsWith($"{Grow2NotesFactory.Origin}/setup#u={first.UserId}&t=", setupLink, StringComparison.Ordinal);
        Assert.DoesNotContain("+", setupLink, StringComparison.Ordinal);
        var fragment = QueryHelpers.ParseQuery(setupLink[(setupLink.IndexOf('#', StringComparison.Ordinal) + 1)..]);
        Assert.Equal(["t", "u"], fragment.Keys.Order());
        Assert.True(await VerifyAsync(first.UserId, fragment["t"].ToString()));
        Assert.False(await VerifyAsync(first.UserId, first.Token));
    }

    // As when another request has given the user a link, or changed them otherwise, since this one loaded them.
    [Fact]
    public async Task A_user_changed_since_they_were_loaded_gets_no_link_and_nothing_is_saved()
    {
        var invited = await factory.InviteAsync(organisation);
        await using var stale = factory.Services.CreateAsyncScope();
        var staleUser = await stale.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>()
            .FindByIdAsync(invited.UserId);
        Assert.NotNull(staleUser);

        var newer = await IssueAsync(factory, invited.UserId);
        Assert.NotNull(newer);
        var saved = await ReadAsync(invited.UserId);

        Assert.Null(await stale.ServiceProvider.GetRequiredService<SetupLinkIssuer>().IssueAsync(staleUser));

        Assert.Equal(saved, await ReadAsync(invited.UserId));
        var newerToken = QueryHelpers.ParseQuery(newer[(newer.IndexOf('#', StringComparison.Ordinal) + 1)..])["t"];
        Assert.True(await VerifyAsync(invited.UserId, newerToken.ToString()));
    }

    [Fact]
    public async Task With_no_App_Origin_it_stops_before_it_changes_anything()
    {
        var invited = await factory.InviteAsync(organisation);
        var before = await ReadAsync(invited.UserId);
        await using var app = factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration(
            configuration => configuration.AddInMemoryCollection([new("App:Origin", "")])));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => IssueAsync(app, invited.UserId));

        Assert.StartsWith("App:Origin is not set", error.Message, StringComparison.Ordinal);
        Assert.Equal(before, await ReadAsync(invited.UserId));
        Assert.True(await VerifyAsync(invited.UserId, invited.Token));
    }

    // In a scope of its own, as a request would.
    private static async Task<string?> IssueAsync(WebApplicationFactory<Program> app, string userId)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var user = await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByIdAsync(userId);
        Assert.NotNull(user);
        return await scope.ServiceProvider.GetRequiredService<SetupLinkIssuer>().IssueAsync(user);
    }

    private async Task<StoredUser> ReadAsync(string userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByIdAsync(userId);
        Assert.NotNull(user);
        return new(user.SecurityStamp, user.ConcurrencyStamp, await users.GetAuthenticatorKeyAsync(user));
    }

    private async Task<bool> VerifyAsync(string userId, string token)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByIdAsync(userId);
        Assert.NotNull(user);
        return await users.VerifyUserTokenAsync(
            user, SetupTokenProvider.ProviderName, SetupTokenProvider.Purpose, token);
    }

    private sealed record StoredUser(string? SecurityStamp, string? ConcurrencyStamp, string? AuthenticatorKey);
}
