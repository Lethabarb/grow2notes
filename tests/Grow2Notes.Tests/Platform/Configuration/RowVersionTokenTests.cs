using Grow2Notes.Web.Platform;
using Grow2Notes.Web.Platform.Configuration;

namespace Grow2Notes.Tests.Platform.Configuration;

/// <summary>
/// The version of a configuration row, on stand-in rows with the 8 bytes of a SQL Server <c>rowversion</c>, most
/// significant first.
/// </summary>
public sealed class RowVersionTokenTests
{
    // RowVersion 2001, and 2000, the same row before it last changed.
    private static readonly Row Current = new([0, 0, 0, 0, 0, 0, 0x07, 0xD1]);
    private static readonly Row Stale = new([0, 0, 0, 0, 0, 0, 0x07, 0xD0]);

    [Theory]
    [InlineData(new byte[] { 0, 0, 0, 0, 0, 0, 0x07, 0xD1 }, "AAAAAAAAB9E=")]
    // Base64 itself, with + and /, not base64url's - and _.
    [InlineData(new byte[] { 0, 0, 0, 0, 0, 0x3E, 0xFB, 0xFF }, "AAAAAAA++/8=")]
    public void The_token_is_the_RowVersion_bytes_as_base64(byte[] rowVersion, string token) =>
        Assert.Equal(token, RowVersionToken.Of(new Row(rowVersion)));

    [Fact]
    public void A_rows_ETag_is_the_one_If_Match_value_the_check_passes_for_it()
    {
        var token = RowVersionToken.Of(Current);

        Assert.Null(IfMatch.Check(RowVersionToken.ETag(Current), token));
        Assert.Same(ErrorCode.PreconditionFailed, IfMatch.Check(RowVersionToken.ETag(Stale), token));
        Assert.Same(ErrorCode.PreconditionFailed, IfMatch.Check(token, token));
    }

    [Fact]
    public void A_row_not_yet_saved_has_no_ETag() =>
        Assert.Throws<ArgumentException>(() => RowVersionToken.ETag(new Row([])));

    private sealed record Row(byte[] RowVersion) : IVersionedRow;
}
