using System.Diagnostics.CodeAnalysis;
using Grow2Notes.Web.Data;
using Microsoft.AspNetCore.Identity;

namespace Grow2Notes.Web.Features.Users;

/// <summary>
/// What <see cref="Invitations.InviteAsync"/> did: either it created the Invited user and made their setup link, or
/// Identity refused the user, with its errors, and nothing was created.
/// </summary>
internal sealed class InviteResult
{
    private InviteResult(ApplicationUser? user, string? setupLink, IReadOnlyList<IdentityError> errors)
    {
        User = user;
        SetupLink = setupLink;
        Errors = errors;
    }

    [MemberNotNullWhen(true, nameof(User), nameof(SetupLink))]
    public bool Succeeded => User is not null && SetupLink is not null;

    /// <summary>The Invited user, as saved.</summary>
    public ApplicationUser? User { get; }

    /// <summary>
    /// <c>&lt;App:Origin&gt;/setup#u=&lt;user ID&gt;&amp;t=&lt;setup token&gt;</c> (design.md §8.1 step 2), which goes
    /// to the user and is kept nowhere, because it holds the token.
    /// </summary>
    public string? SetupLink { get; }

    /// <summary>
    /// Why Identity refused the user, such as <c>DuplicateEmail</c> for an address that another account holds; empty
    /// when it did not.
    /// </summary>
    public IReadOnlyList<IdentityError> Errors { get; }

    public static InviteResult Invited(ApplicationUser user, string setupLink) => new(user, setupLink, []);

    public static InviteResult Refused(IEnumerable<IdentityError> errors) => new(null, null, [.. errors]);
}
