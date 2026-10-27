using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Features.Mcp.OAuth;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Time.Testing;

namespace Grow2Notes.Tests.Features.Mcp.OAuth;

/// <summary>
/// The handle of an authorise request that waits for consent, and the decision on it (mcp-server.md §4.1): both last 10
/// minutes from the request's start, by the app's clock, and neither can stand in for the other.
/// </summary>
public sealed class ConsentRequestsTests
{
    private const string Query = "client_id=c&state=s&resource=r";

    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private readonly FakeTimeProvider clock = new(TestClock.Start);
    private readonly ConsentRequests consents;

    public ConsentRequestsTests() => consents = new(new EphemeralDataProtectionProvider(), clock);

    [Fact]
    public void A_handle_reads_back_at_exactly_10_minutes_and_is_refused_a_second_later()
    {
        var handle = consents.Protect(Query);

        clock.Advance(TimeSpan.FromMinutes(10));
        var atTheDeadline = consents.Read(handle);
        clock.Advance(TimeSpan.FromSeconds(1));
        var afterIt = consents.Read(handle);

        Assert.Equal(Query, atTheDeadline?.Query);
        Assert.Null(afterIt);
    }

    [Fact]
    public void A_tampered_handle_is_refused()
    {
        var handle = consents.Protect(Query);

        Assert.Null(consents.Read(Tamper(handle)));
        Assert.Null(consents.Read("not a handle"));
        Assert.Null(consents.Read(""));
    }

    [Fact]
    public void A_handle_cannot_be_read_as_a_decision_and_a_decision_cannot_be_read_as_a_handle()
    {
        var handle = consents.Protect(Query);
        var decision = consents.Decide(consents.Read(handle)!, UserId, allow: true);

        Assert.Null(consents.ReadDecision(handle, Query, UserId));
        Assert.Null(consents.Read(decision));
    }

    [Fact]
    public void A_decision_is_read_for_the_same_query_and_the_same_person()
    {
        var decision = consents.Decide(consents.Read(consents.Protect(Query))!, UserId, allow: false);

        var read = consents.ReadDecision(decision, Query, UserId);

        Assert.NotNull(read);
        Assert.False(read.Allow);
        Assert.Equal(UserId, read.UserId);
    }

    [Fact]
    public void A_decision_is_refused_for_another_person()
    {
        var decision = consents.Decide(consents.Read(consents.Protect(Query))!, UserId, allow: true);

        Assert.Null(consents.ReadDecision(decision, Query, Guid.NewGuid()));
    }

    [Fact]
    public void A_decision_is_refused_for_a_query_that_differs_by_one_character()
    {
        var decision = consents.Decide(consents.Read(consents.Protect(Query))!, UserId, allow: true);

        Assert.Null(consents.ReadDecision(decision, Query[..^1] + "s", UserId));
        Assert.Null(consents.ReadDecision(decision, "", UserId));
    }

    [Fact]
    public void A_tampered_decision_is_refused()
    {
        var decision = consents.Decide(consents.Read(consents.Protect(Query))!, UserId, allow: true);

        Assert.Null(consents.ReadDecision(Tamper(decision), Query, UserId));
    }

    [Fact]
    public void A_decision_expires_10_minutes_after_the_request_started_not_after_it_was_made()
    {
        var request = consents.Read(consents.Protect(Query))!;
        clock.Advance(TimeSpan.FromMinutes(6));
        var decision = consents.Decide(request, UserId, allow: true);

        clock.Advance(TimeSpan.FromMinutes(4));
        var atTheDeadline = consents.ReadDecision(decision, Query, UserId);
        clock.Advance(TimeSpan.FromSeconds(1));
        var afterIt = consents.ReadDecision(decision, Query, UserId);

        Assert.NotNull(atTheDeadline);
        Assert.Null(afterIt);
    }

    // A character from the middle, as the last one of a base64url text can carry bits that decode to nothing.
    private static string Tamper(string token)
    {
        var middle = token.Length / 2;
        return token[..middle] + (token[middle] == 'A' ? 'B' : 'A') + token[(middle + 1)..];
    }
}
