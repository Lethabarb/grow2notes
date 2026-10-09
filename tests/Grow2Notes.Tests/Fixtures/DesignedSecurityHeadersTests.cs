namespace Grow2Notes.Tests.Fixtures;

/// <summary>
/// Reading design.md §9.7's headers (<see cref="DesignedSecurityHeaders"/>) from samples of the design, as
/// <c>ErrorCodeTests</c> reads §6.9's codes, so the header tests are shown to check what the design says.
/// </summary>
public sealed class DesignedSecurityHeadersTests
{
    [Fact]
    public void Reading_9_7_takes_each_header_with_its_wrapped_lines_joined_and_its_text_block_alone()
    {
        const string design = """
            ### 9.7 Security headers and CSP

            Set by middleware on every response.

            ```text
            Content-Security-Policy: default-src 'self'; img-src 'self';
              font-src 'self';
              upgrade-insecure-requests
            X-Content-Type-Options: nosniff
            ```

            Prose after the block.

            ### 9.8 CSRF

            ```text
            Next-Section: not in 9.7
            ```
            """;

        Assert.Equal(
            new Dictionary<string, string>
            {
                ["Content-Security-Policy"] =
                    "default-src 'self'; img-src 'self'; font-src 'self'; upgrade-insecure-requests",
                ["X-Content-Type-Options"] = "nosniff",
            },
            DesignedSecurityHeaders.In(design));
    }

    [Theory]
    [InlineData("```text\n  font-src 'self'\n```", "\"  font-src 'self'\"")]
    [InlineData("```text\nX-Content-Type-Options nosniff\n```", "\"X-Content-Type-Options nosniff\"")]
    [InlineData("```text\nReferrer-Policy: no-referrer\nreferrer-policy: none\n```", "referrer-policy twice")]
    [InlineData("```text\n```", "no header")]
    [InlineData("```text\nReferrer-Policy: no-referrer\n```\n\n```text\nX-Frame-Options: DENY\n```", "4 fences")]
    public void Reading_9_7_refuses_what_it_cannot_read(string body, string named)
    {
        var design = $"""
            ### 9.7 Security headers and CSP

            {body}
            """;

        var ex = Assert.Throws<InvalidOperationException>(() => DesignedSecurityHeaders.In(design));

        Assert.Contains(named, ex.Message, StringComparison.Ordinal);
    }
}
