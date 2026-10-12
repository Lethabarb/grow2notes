using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;

namespace Grow2Notes.Tests.Fixtures;

/// <summary>
/// The codes that an authenticator app shows for a key, for the tests of setup's code step and sign-in's: RFC 6238's
/// time-based codes, as Identity's authenticator provider checks them, an HMAC-SHA1 of the 30-second step since 1970,
/// cut to six digits, from the key in base32 as the invite stores it. Identity's provider makes no codes itself.
/// </summary>
/// <remarks>
/// Identity checks a code against the system clock, not the app's <see cref="TimeProvider"/>, so a test computes its
/// codes from <see cref="Now"/>, the real time, even when it hosts the app on a fake clock (tests/README.md). Identity
/// accepts the codes of the steps either side of now, so a wrong code is the right key's code for a step well away
/// from them, such as ten minutes ahead (<see cref="At"/>).
/// </remarks>
internal static class AuthenticatorCodes
{
    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    private static readonly TimeSpan Step = TimeSpan.FromSeconds(30);

    /// <summary>The code that an authenticator app holding <paramref name="key"/> shows now.</summary>
    public static string Now(string key) => At(key, DateTimeOffset.UtcNow);

    /// <summary>
    /// The code that an authenticator app holding <paramref name="key"/> shows at <paramref name="time"/>.
    /// </summary>
    public static string At(string key, DateTimeOffset time)
    {
        Span<byte> counter = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64BigEndian(counter, time.ToUnixTimeSeconds() / (long)Step.TotalSeconds);
        var hash = HMACSHA1.HashData(FromBase32(key), counter);

        // RFC 4226's dynamic truncation: the last byte's low four bits say where to read four bytes, less the top bit.
        var offset = hash[^1] & 0x0F;
        var value = BinaryPrimitives.ReadInt32BigEndian(hash.AsSpan(offset)) & int.MaxValue;
        return (value % 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
    }

    // RFC 4648's base32, without padding, as Identity writes its keys: five bits for each character, most significant
    // first.
    private static byte[] FromBase32(string key)
    {
        var bytes = new byte[key.Length * 5 / 8];
        var buffer = 0;
        var bits = 0;
        var index = 0;
        foreach (var character in key)
        {
            var value = Base32Alphabet.IndexOf(character, StringComparison.Ordinal);
            if (value < 0)
            {
                throw new ArgumentException($"'{character}' is not a base32 character.", nameof(key));
            }

            buffer = (buffer << 5) | value;
            bits += 5;
            if (bits >= 8)
            {
                bits -= 8;
                bytes[index++] = (byte)(buffer >> bits);
                buffer &= (1 << bits) - 1;
            }
        }

        return bytes;
    }
}
