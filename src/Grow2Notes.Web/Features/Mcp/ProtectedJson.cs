using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;

namespace Grow2Notes.Web.Features.Mcp;

/// <summary>
/// The pieces that turn a small record into a token on the app's Data Protection key ring and back, shared by the
/// consent screen's tokens and the confirmation step of the access tools: base64url text that nothing stores.
/// </summary>
internal static class ProtectedJson
{
    /// <summary>
    /// The base64url text of <paramref name="value"/> as JSON, protected by <paramref name="protector"/>.
    /// </summary>
    public static string Seal<T>(IDataProtector protector, T value) =>
        WebEncoders.Base64UrlEncode(protector.Protect(JsonSerializer.SerializeToUtf8Bytes(value)));

    /// <summary>The value that <paramref name="text"/> holds, or null if this protector did not seal it.</summary>
    public static T? Open<T>(IDataProtector protector, string text)
        where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(protector.Unprotect(WebEncoders.Base64UrlDecode(text)));
        }
        catch (Exception exception) when (exception is CryptographicException or FormatException or JsonException)
        {
            return null;
        }
    }

    /// <summary>The SHA-256 hash of <paramref name="text"/>, base64url.</summary>
    public static string Hash(string text) =>
        WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}
