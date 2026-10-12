using Grow2Notes.Web.Features.Auth;

namespace Grow2Notes.Tests.Features.Auth;

/// <summary>
/// The code check (<see cref="OneTimeCode"/>): a code as people type it, grouped, around separators or in full-width
/// digits, reads as its six ASCII digits, as the SPA's <c>code.ts</c> reads it, and anything else is badly formed.
/// </summary>
public sealed class OneTimeCodeTests
{
    // In order: as the app shows it; in two groups, split by a space, a no-break space, a tab, a hyphen-minus, a
    // hyphen, an en dash, an em dash or a minus sign; in three; with spaces around it, as a paste can carry; in
    // full-width digits; and split by a full-width hyphen-minus, which NFKC makes a hyphen-minus.
    [Theory]
    [InlineData("123456")]
    [InlineData("123 456")]
    [InlineData("123\u00A0456")]
    [InlineData("123\t456")]
    [InlineData("123-456")]
    [InlineData("123\u2010456")]
    [InlineData("123\u2013456")]
    [InlineData("123\u2014456")]
    [InlineData("123\u2212456")]
    [InlineData("12 34 56")]
    [InlineData(" 123456 ")]
    [InlineData("\uFF11\uFF12\uFF13\uFF14\uFF15\uFF16")]
    [InlineData("123\uFF0D456")]
    public void A_code_as_people_type_it_reads_as_its_six_digits(string text)
    {
        Assert.True(OneTimeCode.TryRead(text, out var code));
        Assert.Equal("123456", code);
    }

    // In order: nothing; separators alone; five digits; seven; letters; a letter among digits; Arabic-Indic digits,
    // which NFKC leaves as they are; and digits split by an underscore or a full stop, neither of them a separator.
    [Theory]
    [InlineData("")]
    [InlineData(" - ")]
    [InlineData("12345")]
    [InlineData("1234567")]
    [InlineData("abcdef")]
    [InlineData("12345a")]
    [InlineData("\u0661\u0662\u0663\u0664\u0665\u0666")]
    [InlineData("123_456")]
    [InlineData("123.456")]
    public void Anything_else_is_badly_formed(string text)
    {
        Assert.False(OneTimeCode.TryRead(text, out var code));
        Assert.Null(code);
    }
}
