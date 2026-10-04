using Grow2Notes.Web.Data;

namespace Grow2Notes.Tests.Data;

/// <summary>
/// The values are stored in <c>AspNetUsers</c>, so changing one would change what existing rows mean
/// (mcp-server.md §3.5, convention 2).
/// </summary>
public sealed class UserEnumTests
{
    [Fact]
    public void UserRole_keeps_its_stored_values() =>
        Assert.Equal(
            new Dictionary<string, byte> { ["Worker"] = 1, ["Manager"] = 2 },
            Enum.GetValues<UserRole>().ToDictionary(r => r.ToString(), r => (byte)r));

    [Fact]
    public void UserStatus_keeps_its_stored_values() =>
        Assert.Equal(
            new Dictionary<string, byte> { ["Invited"] = 0, ["Active"] = 1, ["Deactivated"] = 2 },
            Enum.GetValues<UserStatus>().ToDictionary(s => s.ToString(), s => (byte)s));
}
