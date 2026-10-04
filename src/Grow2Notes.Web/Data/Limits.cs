namespace Grow2Notes.Web.Data;

/// <summary>
/// The length limits, in characters (design.md §5.1, A6). A limit on a stored field sets both its column's size, through
/// <c>HasMaxLength</c>, and the request validation for that field, so the two cannot drift apart. Request validation
/// must use these constants, never a number of its own: <c>[MaxLength(Limits.GroupName)]</c> on a request type, or
/// <c>name.Length &gt; Limits.GroupName</c> in a check written by hand. Most of them belong to entities that later
/// stories add.
/// </summary>
internal static class Limits
{
    /// <summary><c>Organisation.Name</c>, printed on report and export headers (§5.3).</summary>
    public const int OrganisationName = 200;

    /// <summary><c>Organisation.GuidePrompts</c> (A6).</summary>
    public const int GuidePrompts = 1_000;

    /// <summary><see cref="ApplicationUser.DisplayName"/> (§5.1).</summary>
    public const int DisplayName = 100;

    /// <summary>
    /// The <c>AspNetUsers</c> columns that hold the email address: <c>Email</c>, <c>UserName</c> and their normalised
    /// copies (§5.3). The design gives no size, so this keeps the one Identity gives them.
    /// </summary>
    public const int Email = 256;

    /// <summary><c>Participant.GivenName</c> and <c>Participant.FamilyName</c>, each (§5.1).</summary>
    public const int ParticipantName = 100;

    /// <summary>
    /// The wording of a goal or a common item: <c>Goal.Text</c>, <c>CommonItem.Text</c>, and their snapshots in
    /// <c>NoteDraft.Items</c> and <c>NoteVersionItem.Text</c>, which hold goals and common items alike (A6).
    /// </summary>
    public const int ItemText = 200;

    /// <summary>
    /// <c>CommonItemGroup.Name</c> and its snapshots in <c>NoteDraft.Groups</c> and <c>NoteVersionGroup.Name</c> (A6).
    /// </summary>
    public const int GroupName = 200;

    /// <summary><c>NoteDraft.FlagReason</c> and <c>NoteVersion.FlagReason</c> (A6).</summary>
    public const int FlagReason = 200;

    /// <summary><c>NoteReview.Comment</c> (A6).</summary>
    public const int ReviewComment = 500;

    /// <summary>
    /// The Guided notes text: <c>NoteDraft.Narrative</c> and <c>NoteVersion.Narrative</c> (A6). It is longer than the
    /// 4,000 characters an <c>nvarchar(n)</c> can hold, so the columns are <c>nvarchar(max)</c> and only validation
    /// enforces it (§5.1).
    /// </summary>
    public const int GuidedNotes = 20_000;

    /// <summary><c>AuditEvent.EventType</c>, a <c>varchar</c> (§5.3).</summary>
    public const int AuditEventType = 64;

    /// <summary><c>AuditEvent.EntityType</c>, a <c>varchar</c> (§5.3).</summary>
    public const int AuditEntityType = 32;

    /// <summary>
    /// <c>AuditEvent.IpAddress</c>, a <c>varchar</c> (§5.3): the longest text form of an IP address, an IPv6 address
    /// ending in an IPv4 one.
    /// </summary>
    public const int IpAddress = 45;

    /// <summary>The shortest password Identity accepts (A22, §8.4): a minimum, where the others are maximums.</summary>
    public const int PasswordMinLength = 12;
}
