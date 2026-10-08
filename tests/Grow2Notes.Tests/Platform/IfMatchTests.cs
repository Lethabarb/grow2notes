using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

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
        Assert.Null(IfMatch.Check(RequestWith($"\"{token}\""), token));

    [Fact]
    public void A_request_without_If_Match_answers_428_precondition_required() =>
        AssertProblem(IfMatch.Check(new DefaultHttpContext().Request, Token), 428, "precondition.required");

    [Fact]
    public void An_empty_If_Match_answers_428_as_a_missing_one_does() =>
        AssertProblem(IfMatch.Check(RequestWith(""), Token), 428, "precondition.required");

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
    public void Any_other_If_Match_answers_412_precondition_failed(params string[] ifMatch) =>
        AssertProblem(IfMatch.Check(RequestWith(ifMatch), Token), 412, "precondition.failed");

    [Fact]
    public void A_rows_ETag_is_its_token_in_quotes() =>
        Assert.Equal("\"AAAAAAAAB9E=\"", IfMatch.ETag(Token));

    [Fact]
    public void An_empty_token_throws_rather_than_matching_the_empty_tag() =>
        Assert.Throws<ArgumentException>(() => IfMatch.Check(RequestWith("\"\""), ""));

    private static HttpRequest RequestWith(params string[] ifMatch)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.IfMatch = ifMatch;
        return context.Request;
    }

    private static void AssertProblem(ProblemHttpResult? problem, int status, string code)
    {
        Assert.NotNull(problem);
        Assert.Equal(status, problem.StatusCode);
        Assert.Equal(code, problem.ProblemDetails.Extensions["code"] as string);
    }
}
