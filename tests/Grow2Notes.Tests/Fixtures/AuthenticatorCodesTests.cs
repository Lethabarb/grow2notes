namespace Grow2Notes.Tests.Fixtures;

/// <summary>
/// The tests' codes (<see cref="AuthenticatorCodes"/>) are RFC 6238's: its appendix B's SHA-1 test values, cut to six
/// digits, for its key, "12345678901234567890", in base32.
/// </summary>
public sealed class AuthenticatorCodesTests
{
    private const string Key = "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ";

    [Theory]
    [InlineData(59L, "287082")]
    [InlineData(1_111_111_109L, "081804")]
    [InlineData(1_111_111_111L, "050471")]
    [InlineData(1_234_567_890L, "005924")]
    [InlineData(2_000_000_000L, "279037")]
    [InlineData(20_000_000_000L, "353130")]
    public void Each_code_is_RFC_6238_s_for_its_time(long unixSeconds, string code) =>
        Assert.Equal(code, AuthenticatorCodes.At(Key, DateTimeOffset.FromUnixTimeSeconds(unixSeconds)));
}
