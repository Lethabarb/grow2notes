using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Grow2Notes.Tests.Platform;

public sealed class IfMatchTests
{
    // A RowVersion of 2001, as base64: a configuration row's token.
    private const string Token = "AAAAAAAAB9E=";

    [Theory]
    [InlineData(Token)]
    // A user's ConcurrencyStamp.
    [InlineData("0f8fad5b-d9cb-469f-a165-70867728950e")]
    public void The_current_token_as_a_strong_entity_tag_in_quotes_passes(string token) =>
        Assert.Null(IfMatch.Check(IfMatchSent($"\"{token}\""), token));

    [Fact]
    public void A_request_without_If_Match_gives_precondition_required()
    {
        var missing = new DefaultHttpContext().Request.Headers.IfMatch;

        Assert.Same(ErrorCode.PreconditionRequired, IfMatch.Check(missing, Token));
    }

    [Fact]
    public void An_empty_If_Match_gives_precondition_required_as_a_missing_one_does() =>
        Assert.Same(ErrorCode.PreconditionRequired, IfMatch.Check(IfMatchSent(""), Token));

    [Theory]
    // Stale: the ETag of RowVersion 2000, from before the row last changed.
    [InlineData("\"AAAAAAAAB9A=\"")]
    // The current token, but unquoted, weak, in another case, or without its closing quote.
    [InlineData("AAAAAAAAB9E=")]
    [InlineData("W/\"AAAAAAAAB9E=\"")]
    [InlineData("\"aaaaaaaab9e=\"")]
    [InlineData("\"AAAAAAAAB9E=")]
    // Any version at all, and the empty tag.
    [InlineData("*")]
    [InlineData("\"\"")]
    // A list that holds the current ETag, in one field and in two.
    [InlineData("\"AAAAAAAAB9E=\", \"AAAAAAAAB9A=\"")]
    [InlineData("\"AAAAAAAAB9E=\"", "\"AAAAAAAAB9A=\"")]
    public void Any_other_If_Match_gives_precondition_failed(params string[] ifMatch) =>
        Assert.Same(ErrorCode.PreconditionFailed, IfMatch.Check(IfMatchSent(ifMatch), Token));

    [Fact]
    public void A_rows_ETag_is_its_token_in_quotes() =>
        Assert.Equal("\"AAAAAAAAB9E=\"", IfMatch.ETag(Token));

    [Fact]
    public void An_empty_token_throws_rather_than_matching_the_empty_tag() =>
        Assert.Throws<ArgumentException>(() => IfMatch.Check(IfMatchSent("\"\""), ""));

    // The one channel rule for the version: the app is refused at once, a tool only if the call changes something.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_matching_ETag_is_refused_for_neither_channel(bool mcp) =>
        Assert.Equal(
            new VersionCheck(null, null), IfMatch.CheckFor(ActorOf(mcp), IfMatchSent($"\"{Token}\""), Token));

    [Theory]
    [InlineData("\"AAAAAAAAB9A=\"", "precondition.failed")]
    [InlineData("", "precondition.required")]
    public void The_app_is_refused_at_once_for_a_stale_or_missing_ETag(string ifMatch, string code)
    {
        var check = IfMatch.CheckFor(ActorOf(mcp: false), IfMatchSent(ifMatch), Token);

        Assert.Equal(code, check.RefuseNow?.Code);
        Assert.Null(check.RefuseIfChanged);
    }

    [Theory]
    [InlineData("\"AAAAAAAAB9A=\"", "precondition.failed")]
    [InlineData("", "precondition.required")]
    public void A_tool_s_refusal_is_held_back_for_the_save(string ifMatch, string code)
    {
        var check = IfMatch.CheckFor(ActorOf(mcp: true), IfMatchSent(ifMatch), Token);

        Assert.Null(check.RefuseNow);
        Assert.Equal(code, check.RefuseIfChanged?.Code);
    }

    // A tool's expectedVersion is the token without quotes, as the look-ups give it.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void A_missing_or_empty_expected_version_names_no_version(string? expectedVersion)
    {
        var sent = IfMatch.FromExpectedVersion(expectedVersion);

        Assert.Equal(0, sent.Count);
        Assert.Same(ErrorCode.PreconditionRequired, IfMatch.Check(sent, Token));
    }

    [Fact]
    public void An_expected_version_is_sent_as_the_rows_ETag_so_the_current_one_passes()
    {
        var sent = IfMatch.FromExpectedVersion(Token);

        Assert.Equal(IfMatch.ETag(Token), Assert.Single(sent));
        Assert.Null(IfMatch.Check(sent, Token));
    }

    [Fact]
    public void A_stale_expected_version_gives_precondition_failed() =>
        Assert.Same(ErrorCode.PreconditionFailed, IfMatch.Check(IfMatch.FromExpectedVersion("AAAAAAAAB9A="), Token));

    private static Actor ActorOf(bool mcp) =>
        new(Guid.Parse("3a9e2c4b-5d6f-4a7b-8c9d-0e1f2a3b4c5d"), mcp ? Channel.Mcp : Channel.App);

    // The values as an endpoint reads them from its request, each header field one value.
    private static StringValues IfMatchSent(params string[] ifMatch)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.IfMatch = ifMatch;
        return context.Request.Headers.IfMatch;
    }
}
