using Microsoft.AspNetCore.Identity;

namespace Grow2Notes.Web.Data;

/// <summary>
/// A worker or manager of one organisation: a row of <c>AspNetUsers</c>. Identity's own columns hold the email, the
/// sign-in methods and the security stamp; these are the columns Grow2Notes adds (design.md §5.3).
/// </summary>
internal sealed class ApplicationUser : IdentityUser<Guid>
{
    public required Guid OrganisationId { get; set; }

    /// <summary>Shown as the author, editor and reviewer.</summary>
    public required string DisplayName { get; set; }

    public required UserRole Role { get; set; }

    public required UserStatus Status { get; set; }

    public required DateTime InvitedAtUtc { get; set; }

    /// <summary>Null only for the first manager, whom the <c>admin bootstrap</c> command invites.</summary>
    public Guid? InvitedByUserId { get; set; }

    /// <summary>Set when the user completes setup.</summary>
    public DateTime? ActivatedAtUtc { get; set; }

    public DateTime? DeactivatedAtUtc { get; set; }
}
