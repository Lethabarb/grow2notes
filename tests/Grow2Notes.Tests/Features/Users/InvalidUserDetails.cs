using Grow2Notes.Web.Data;
using Grow2Notes.Web.Features.Users;

namespace Grow2Notes.Tests.Features.Users;

/// <summary>
/// The names, addresses and roles that Invite user and Edit user refuse alike (<see cref="UserDetailsChecks"/>), each
/// with the error that its field's <c>422</c> gives, for the theories of <see cref="InviteUserEndpointTests"/> and
/// <see cref="UserEditEndpointTests"/>.
/// </summary>
public static class InvalidUserDetails
{
    public static TheoryData<string?, string> Names { get; } = new()
    {
        { null, UserDetailsChecks.NameMissing },
        { "", UserDetailsChecks.NameMissing },
        { "   ", UserDetailsChecks.NameMissing },
        { new string('n', Limits.DisplayName + 1), UserDetailsChecks.NameTooLong },
    };

    // An address that the server refuses but the screen's loose check passes (one "@" with text either side) gets the
    // format message too: one with a letter outside ASCII, which Identity's user name refuses, and one longer than the
    // columns that hold an address, which Identity's check of an address passes.
    public static TheoryData<string?, string> Emails { get; } = new()
    {
        { null, UserDetailsChecks.EmailMissing },
        { "", UserDetailsChecks.EmailMissing },
        { "alex", UserDetailsChecks.EmailNotAnAddress },
        { "zoë@example.org", UserDetailsChecks.EmailNotAnAddress },
        {
            new string('a', Limits.Email + 1 - "@example.org".Length) + "@example.org",
            UserDetailsChecks.EmailNotAnAddress
        },
    };

    // A role is named exactly, never by its number or in another letter case (mcp-server.md §3.5, convention 4).
    public static TheoryData<string?> Roles { get; } = [(string?)null, "", "Admin", "worker", "1"];
}
