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

    // The values as an endpoint reads them from its request, each header field one value.
    private static StringValues IfMatchSent(params string[] ifMatch)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.IfMatch = ifMatch;
        return context.Request.Headers.IfMatch;
    }
}
