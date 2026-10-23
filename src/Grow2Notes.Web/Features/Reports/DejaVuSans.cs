using System.Buffers.Binary;
using System.Reflection;
using System.Text;
using PdfSharp.Fonts;

namespace Grow2Notes.Web.Features.Reports;

/// <summary>
/// The DejaVu Sans font that the PDF embeds, and which characters it draws. The PDF has no other font, so a character
/// the font lacks would print as a blank box; <see cref="Drawable"/> says U+FFFD in its place instead (A59, A81).
/// </summary>
internal static class DejaVuSans
{
    private const int LastBasicMultilingualPlane = 0xFFFF;

    public static readonly byte[] Bytes = ReadResource("DejaVuSans.ttf");

    // Ranges of code points, in the order the font lists them.
    private static readonly (uint First, uint Last)[] Ranges = ReadRanges(Bytes);

    /// <summary>
    /// Whether the font has a glyph for <paramref name="rune"/>, in the Basic Multilingual Plane only (A81). This is
    /// the one place that limit is applied.
    /// </summary>
    public static bool Draws(Rune rune) =>
        rune.Value <= LastBasicMultilingualPlane && Ranges.Any(r => r.First <= rune.Value && rune.Value <= r.Last);

    /// <summary><paramref name="text"/> with U+FFFD in place of each character the font does not draw.</summary>
    public static string Drawable(string text)
    {
        var drawable = new StringBuilder(text.Length);
        foreach (var rune in text.EnumerateRunes())
        {
            drawable.Append(Draws(rune) ? rune.ToString() : "\uFFFD");
        }

        return drawable.ToString();
    }

    private static byte[] ReadResource(string name)
    {
        using var stream = typeof(DejaVuSans).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"The embedded resource {name} is missing.");
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }

    // The font's (3, 10) cmap subtable, format 12: groups of consecutive code points that map to consecutive glyphs,
    // from a first glyph. Glyph 0 is "missing", so a group starting at glyph 0 draws from its second code point.
    private static (uint First, uint Last)[] ReadRanges(byte[] font)
    {
        var data = font.AsSpan();
        var cmap = Table(data, "cmap");
        var subtable = 0u;
        for (var i = 0; i < BinaryPrimitives.ReadUInt16BigEndian(data[(int)(cmap + 2)..]); i++)
        {
            var record = data[(int)(cmap + 4 + (8 * i))..];
            if (BinaryPrimitives.ReadUInt16BigEndian(record) == 3
                && BinaryPrimitives.ReadUInt16BigEndian(record[2..]) == 10)
            {
                subtable = cmap + BinaryPrimitives.ReadUInt32BigEndian(record[4..]);
            }
        }

        if (subtable == 0)
        {
            throw new InvalidOperationException("The embedded font has no format 12 cmap subtable.");
        }

        var groups = data[(int)subtable..];
        var ranges = new List<(uint, uint)>();
        for (var i = 0; i < BinaryPrimitives.ReadUInt32BigEndian(groups[12..]); i++)
        {
            var group = groups[(16 + (12 * i))..];
            var glyph = BinaryPrimitives.ReadUInt32BigEndian(group[8..]);
            var first = BinaryPrimitives.ReadUInt32BigEndian(group) + (glyph == 0 ? 1u : 0u);
            var last = BinaryPrimitives.ReadUInt32BigEndian(group[4..]);
            if (first <= last)
            {
                ranges.Add((first, last));
            }
        }

        return [.. ranges];
    }

    private static uint Table(ReadOnlySpan<byte> font, string tag)
    {
        for (var i = 0; i < BinaryPrimitives.ReadUInt16BigEndian(font[4..]); i++)
        {
            var record = font[(12 + (16 * i))..];
            if (Encoding.ASCII.GetString(record[..4]) == tag)
            {
                return BinaryPrimitives.ReadUInt32BigEndian(record[8..]);
            }
        }

        throw new InvalidOperationException($"The embedded font has no {tag} table.");
    }
}

/// <summary>
/// Tells PDFsharp to use DejaVu Sans for every family, bold and italic included: the one regular face is slanted and
/// thickened by PDFsharp itself, so the app carries one font file.
/// </summary>
internal sealed class DejaVuFontResolver : IFontResolver
{
    public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic) =>
        new("DejaVuSans", isBold, isItalic);

    public byte[]? GetFont(string faceName) => DejaVuSans.Bytes;
}
