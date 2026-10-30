using System.Text.Json;
using Grow2Notes.Web.Features.Mcp;
using Grow2Notes.Web.Features.Mcp.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using ModelContextProtocol.Protocol;

namespace Grow2Notes.Tests.Features.Mcp;

/// <summary>
/// The hour's count of the confirmed invites, sign-in resets and deactivations of a user (mcp-server.md §5.6, A139),
/// and the <c>rate_limited</c> refusal it gives, on a clock that the test moves: no host, no database. The 10 and the
/// hour are written out here and not read from the permits, so a change to the design's numbers shows as a failing
/// test.
/// </summary>
public sealed class ConfirmedChangeLimitTests
{
    private static readonly Guid UserA = Guid.Parse("5f1e8f5c-0000-4000-8000-000000000001");

    private static readonly Guid UserB = Guid.Parse("5f1e8f5c-0000-4000-8000-000000000002");

    [Fact]
    public async Task Outside_tests_the_assistant_limits_are_mcp_server_md_5_6s()
    {
        await using var provider = new ServiceCollection().AddAssistantRateLimits().BuildServiceProvider();

        Assert.Equal(
            new AssistantRateLimitPermits(60, 60, 10, 30, 10, 60),
            provider.GetRequiredService<AssistantRateLimitPermits>());
    }

    [Fact]
    public void Ten_changes_are_counted_and_the_11th_is_refused_with_the_whole_hour_to_wait()
    {
        var (limit, _) = Start();
        CountTen(limit, UserA);

        var refused = Assert.IsType<ToolError>(limit.TryCount(UserA));

        Assert.Equal("rate_limited", refused.Code);
        Assert.Null(refused.Fields);
        Assert.Equal(3600, refused.RetryAfterSeconds);
    }

    [Fact]
    public void The_refusal_says_the_time_left_of_the_hour_rounded_up_and_never_less_than_1()
    {
        var (limit, clock) = Start();
        CountTen(limit, UserA);

        clock.Advance(TimeSpan.FromMinutes(20));
        Assert.Equal(2400, limit.TryCount(UserA)!.RetryAfterSeconds);

        // 0.5 seconds are left.
        clock.Advance(TimeSpan.FromMinutes(39) + TimeSpan.FromSeconds(59.5));
        Assert.Equal(1, limit.TryCount(UserA)!.RetryAfterSeconds);
    }

    [Fact]
    public void A_change_at_the_end_of_the_hour_starts_a_new_one()
    {
        var (limit, clock) = Start();
        CountTen(limit, UserA);

        clock.Advance(TimeSpan.FromHours(1));

        CountTen(limit, UserA);
        Assert.Equal(3600, limit.TryCount(UserA)!.RetryAfterSeconds);
    }

    [Fact]
    public void A_refusal_uses_no_permit_and_does_not_move_the_hour()
    {
        var (limit, clock) = Start();
        CountTen(limit, UserA);

        Assert.NotNull(limit.TryCount(UserA));
        clock.Advance(TimeSpan.FromMinutes(10));
        Assert.Equal(3000, limit.TryCount(UserA)!.RetryAfterSeconds);

        // The hour that began with the first change ends on time, however many were refused in it.
        clock.Advance(TimeSpan.FromMinutes(50));
        Assert.Null(limit.TryCount(UserA));
    }

    [Fact]
    public void Each_user_has_a_count_of_their_own()
    {
        var (limit, _) = Start();
        CountTen(limit, UserA);

        Assert.NotNull(limit.TryCount(UserA));
        Assert.Null(limit.TryCount(UserB));
    }

    [Fact]
    public void The_hour_of_a_user_starts_with_their_first_change()
    {
        var (limit, clock) = Start();
        clock.Advance(TimeSpan.FromMinutes(30));
        CountTen(limit, UserA);

        Assert.Equal(3600, limit.TryCount(UserA)!.RetryAfterSeconds);
    }

    [Fact]
    public void The_rate_limited_result_holds_retryAfterSeconds_inside_error_beside_the_others()
    {
        var (limit, _) = Start();
        CountTen(limit, UserA);

        var result = limit.TryCount(UserA)!.ToResult();

        Assert.True(result.IsError);
        var error = Assert.NotNull(result.StructuredContent).GetProperty("error");
        Assert.Equal(["code", "message", "fields", "retryAfterSeconds"], error.EnumerateObject().Select(p => p.Name));
        Assert.Equal("rate_limited", error.GetProperty("code").GetString());
        Assert.Equal(JsonValueKind.Null, error.GetProperty("fields").ValueKind);
        Assert.Equal(3600, error.GetProperty("retryAfterSeconds").GetInt32());
        Assert.DoesNotContain("3600", error.GetProperty("message").GetString());
        Assert.Contains("retryAfterSeconds", error.GetProperty("message").GetString());
        Assert.Equal(
            Assert.NotNull(result.StructuredContent).GetRawText(),
            Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text);
    }

    [Fact]
    public void Every_other_error_holds_exactly_code_message_and_fields()
    {
        var result = ToolError.Unexpected.ToResult();

        var error = Assert.NotNull(result.StructuredContent).GetProperty("error");
        Assert.Equal(["code", "message", "fields"], error.EnumerateObject().Select(p => p.Name));
    }

    [Theory]
    [InlineData(0.2, 1)]
    [InlineData(1, 1)]
    [InlineData(1.2, 2)]
    [InlineData(2400, 2400)]
    public void The_seconds_are_rounded_up_and_at_least_1(double seconds, int expected) =>
        Assert.Equal(expected, ToolError.RateLimited(TimeSpan.FromSeconds(seconds)).RetryAfterSeconds);

    private static (ConfirmedChangeLimit Limit, FakeTimeProvider Clock) Start()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 30, 9, 0, 0, TimeSpan.Zero));
        var permits = new AssistantRateLimitPermits(60, 60, 10, 30, 10, 60);
        return (new ConfirmedChangeLimit(permits, clock), clock);
    }

    // The design's 10 an hour, written out: each is counted.
    private static void CountTen(ConfirmedChangeLimit limit, Guid user)
    {
        for (var i = 0; i < 10; i++)
        {
            Assert.Null(limit.TryCount(user));
        }
    }
}
