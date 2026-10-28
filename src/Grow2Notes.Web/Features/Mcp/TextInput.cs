using System.Globalization;
using System.Text;
using Grow2Notes.Web.Features.Mcp.Contracts;

namespace Grow2Notes.Web.Features.Mcp;

/// <summary>
/// The one check on every text that an assistant sends a tool (mcp-server.md §5.1), so that no tool lets in a
/// character that hides or reorders what a person later reads.
/// </summary>
internal static class TextInput
{
    /// <summary>
    /// The refusal of <paramref name="value"/> for <paramref name="field"/>, or null when it passes. The trimmed length
    /// is checked first, as the app counts a name (A6), then the characters of the value as it was sent. No message
    /// echoes the value.
    /// </summary>
    /// <param name="field">The argument's name.</param>
    /// <param name="label">What the messages call it, capitalised.</param>
    /// <param name="value">The argument as the assistant sent it.</param>
    /// <param name="limit">The most characters it may have once trimmed.</param>
    /// <param name="allowLineBreaks">Whether the text may hold line breaks, as the guide prompts do (A131).</param>
    public static ToolError? Refusal(string field, string label, string value, int limit, bool allowLineBreaks = false)
    {
        if (value.Trim().Length > limit)
        {
            return ToolError.ValidationFailed(
                field, string.Create(CultureInfo.InvariantCulture, $"{label} must be {limit:N0} characters or less"));
        }

        return HasRefusedCharacter(value, allowLineBreaks)
            ? ToolError.ValidationFailed(field, $"{label} has a control or formatting character")
            : null;
    }

    /// <summary>
    /// Whether <paramref name="value"/> holds a control character (which includes line breaks and tabs), a
    /// bidirectional control, or a tag character, which shows nothing. The zero-width joiner and non-joiner and the
    /// soft hyphen are allowed, as names in some scripts need them and the app stores them. With
    /// <paramref name="allowLineBreaks"/>, a line feed and a carriage return pass and nothing else changes.
    /// </summary>
    public static bool HasRefusedCharacter(string value, bool allowLineBreaks = false)
    {
        foreach (var rune in value.EnumerateRunes())
        {
            if (allowLineBreaks && rune.Value is '\n' or '\r')
            {
                continue;
            }

            if (Rune.IsControl(rune) || IsBidirectionalControl(rune.Value) || rune.Value is >= 0xE0000 and <= 0xE007F)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsBidirectionalControl(int codePoint) =>
        codePoint is 0x061C or 0x200E or 0x200F or (>= 0x202A and <= 0x202E) or (>= 0x2066 and <= 0x2069);
}
