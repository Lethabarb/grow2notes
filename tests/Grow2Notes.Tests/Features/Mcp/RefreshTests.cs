using System.Net;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Grow2Notes.Tests.Features.Mcp;

/// <summary>
/// Refreshing an approved assistant's tokens at <c>POST /oauth/token</c> (mcp-server.md §4.2): rotation, the leeway for
/// a lost response, the 30-minute lifetime that each refresh renews, the 12 hours from the person's sign-in, and the
/// end of every connection when the person changes. The app runs on a fake clock, which OpenIddict takes its lifetimes
/// from, so each test moves the time the assistant would have waited.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class RefreshTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    : IClassFixture<Grow2NotesFactory>, IAsyncLifetime
{
    private AssistantFlow flow = null!;
    private Guid managerId;
    private HttpClient browser = null!;

    public static TheoryData<string> ChangesToThePerson { get; } =
    [
        "deactivated",
        "sign-in reset",
        "made a worker",
        "signed out of the app",
    ];

    public async ValueTask InitializeAsync()
    {
        flow = await AssistantFlow.StartAsync(factory, sqlServer, "McpRefresh");
        managerId = await flow.AddUserAsync(UserRole.Manager);
        browser = await flow.SignInAsync(managerId);
    }

    public async ValueTask DisposeAsync()
    {
        browser.Dispose();
        await flow.DisposeAsync();
    }

    [Fact]
    public async Task Each_refresh_rotates_both_tokens_and_needs_no_resource()
    {
        var first = await flow.ConnectAsync(browser);

        var second = await RefreshAsync(first.RefreshToken);
        var third = await RefreshAsync(second.RefreshToken);

        Assert.NotEqual(first.RefreshToken, second.RefreshToken);
        Assert.NotEqual(second.RefreshToken, third.RefreshToken);
        Assert.NotEqual(first.AccessToken, second.AccessToken);
        Assert.Equal(600, third.ExpiresIn);
    }

    [Fact]
    public async Task A_refresh_token_used_again_within_15_seconds_is_accepted()
    {
        var first = await flow.ConnectAsync(browser);
        await RefreshAsync(first.RefreshToken);

        flow.Clock.Advance(TimeSpan.FromSeconds(10));

        await RefreshAsync(first.RefreshToken);
    }

    [Fact]
    public async Task A_refresh_token_used_again_after_the_leeway_is_refused_and_so_is_the_newest_in_its_chain()
    {
        var first = await flow.ConnectAsync(browser);
        var second = await RefreshAsync(first.RefreshToken);
        flow.Clock.Advance(TimeSpan.FromSeconds(10));
        var newest = await RefreshAsync(first.RefreshToken);

        flow.Clock.Advance(TimeSpan.FromSeconds(10));
        await AssertRefusedAsync(first.RefreshToken);

        await AssertRefusedAsync(newest.RefreshToken);
        await AssertRefusedAsync(second.RefreshToken);
    }

    [Fact]
    public async Task A_refresh_token_is_refused_31_minutes_after_the_last_refresh()
    {
        var tokens = await flow.ConnectAsync(browser);
        flow.Clock.Advance(TimeSpan.FromMinutes(20));
        tokens = await RefreshAsync(tokens.RefreshToken);

        flow.Clock.Advance(TimeSpan.FromMinutes(31));

        await AssertRefusedAsync(tokens.RefreshToken);
    }

    [Fact]
    public async Task Refreshes_every_29_minutes_go_on_to_12_hours_from_the_sign_in_and_the_next_is_refused()
    {
        var tokens = await flow.ConnectAsync(browser);
        var limit = TestClock.Start + TimeSpan.FromHours(12);
        tokens = await flow.RefreshEveryAsync(tokens, limit);

        flow.Clock.Advance(limit - flow.Clock.GetUtcNow());
        tokens = await RefreshAsync(tokens.RefreshToken);

        flow.Clock.Advance(TimeSpan.FromSeconds(1));
        await AssertRefusedAsync(tokens.RefreshToken);
    }

    [Fact]
    public async Task An_assistant_past_12_hours_is_refused_without_ending_another_connected_from_a_later_sign_in()
    {
        var a = await flow.ConnectAsync(browser);
        a = await flow.RefreshEveryAsync(a, TestClock.Start + TimeSpan.FromHours(12));
        using var laterBrowser = await flow.SignInAsync(managerId);
        var b = await flow.ConnectAsync(laterBrowser);

        flow.Clock.Advance(TimeSpan.FromMinutes(25));

        await AssertRefusedAsync(a.RefreshToken);
        Assert.All(await flow.AuthorisationStatusesAsync(managerId), status => Assert.Equal(Statuses.Valid, status));
        await RefreshAsync(b.RefreshToken);
    }

    [Theory]
    [MemberData(nameof(ChangesToThePerson))]
    public async Task When_the_person_changes_every_connection_they_have_is_refused_and_revoked(string change)
    {
        var tokens = await flow.ConnectAsync(browser);
        using var anotherBrowser = await flow.SignInAsync(managerId);
        await flow.ConnectAsync(anotherBrowser);

        await (change switch
        {
            "deactivated" => flow.DeactivateAsync(managerId),
            "sign-in reset" => flow.ResetSignInAsync(managerId),
            "made a worker" => flow.ChangeRoleAsync(managerId, UserRole.Worker),
            "signed out of the app" => flow.SignOutOfTheAppAsync(browser),
            _ => throw new ArgumentOutOfRangeException(nameof(change)),
        });
        await AssertRefusedAsync(tokens.RefreshToken);

        var statuses = await flow.AuthorisationStatusesAsync(managerId);
        Assert.Equal(2, statuses.Count);
        Assert.All(statuses, status => Assert.Equal(Statuses.Revoked, status));
    }

    [Fact]
    public async Task A_manager_since_made_Support_still_refreshes()
    {
        var tokens = await flow.ConnectAsync(browser);
        await flow.ChangeRoleAsync(managerId, UserRole.Support);

        await RefreshAsync(tokens.RefreshToken);
    }

    [Fact]
    public async Task A_refresh_token_that_was_revoked_by_the_assistant_cannot_be_refreshed()
    {
        var tokens = await flow.ConnectAsync(browser);

        using var revoked = await flow.RevokeAsync(tokens.RefreshToken, TokenTypeHints.RefreshToken);
        Assert.Equal(HttpStatusCode.OK, revoked.StatusCode);

        await AssertRefusedAsync(tokens.RefreshToken);
    }

    private async Task<AssistantTokenSet> RefreshAsync(string refreshToken)
    {
        using var response = await flow.RefreshAsync(refreshToken);
        return await AssistantFlow.ReadTokensAsync(response);
    }

    private async Task AssertRefusedAsync(string refreshToken)
    {
        using var response = await flow.RefreshAsync(refreshToken);
        Assert.Equal(Errors.InvalidGrant, await AssistantFlow.ErrorAsync(response));
    }
}
