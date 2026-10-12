using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Grow2Notes.Web.Features.Auth;

/// <summary>
/// The code check (totp-setup.md; sign-in.md <i>Interactions</i> 4): reads the 6-digit code of an authenticator app
/// as the person typed it, as the SPA's <c>code.ts</c> does, before the code reaches Identity. Setup's code step uses
/// it, and so does sign-in's (S00.04.05), so that both read a code alike.
/// </summary>
/// <remarks>
/// NFKC first, so full-width digits, from a phone keyboard set to another script, become ASCII ones; then whitespace,
/// hyphens, dashes and minus signs are removed, which people type to group the digits as the app shows them; then
/// exactly six ASCII digits remain, or the code is badly formed. Identity's authenticator provider reads a code with
/// <c>int.TryParse</c>, which refuses "123 456". The text comes from a JSON body, which never holds a lone surrogate,
/// the one thing on which normalising throws.
/// </remarks>
internal static class OneTimeCode
{
    private const int Length = 6;

    /// <summary>
    /// Reads the code in <paramref name="text"/>: <see langword="true"/>, with its six ASCII digits in
    /// <paramref name="code"/>, or <see langword="false"/> for a badly formed one.
    /// </summary>
    public static bool TryRead(string text, [NotNullWhen(true)] out string? code)
    {
        var normalised = text.Normalize(NormalizationForm.FormKC);
        var digits = string.Concat(normalised.Where(character => !IsSeparator(character)));
        code = digits.Length == Length && digits.All(char.IsAsciiDigit) ? digits : null;
        return code is not null;
    }

    // code.ts's separators: whitespace, the hyphen-minus, the hyphens and dashes from U+2010 to U+2015, and the minus
    // sign. After NFKC, .NET's whitespace and JavaScript's differ only in U+0085 and U+FEFF, which no keyboard types.
    private static bool IsSeparator(char character) =>
        char.IsWhiteSpace(character) || character is '-' or (>= '\u2010' and <= '\u2015') or '\u2212';
}
