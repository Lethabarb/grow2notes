using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Identity;

namespace Grow2Notes.Web.Features.Users;

/// <summary>
/// The checks of a user's name, address and role that Invite user and Edit user share (design.md §4.11, §6.6), with
/// users.md's words for each field (Invite user's States, which Edit user's Field errors repeat), which mcp-server.md
/// §5.5 gives the tools too: <see cref="UserInvite"/> and <see cref="UserEdit"/> check a
/// <see cref="UserDetailsRequest"/> with <see cref="Check"/> before anything is read, and give Identity's refusal of
/// the address with <see cref="Refused"/>, so the two refuse a field alike.
/// </summary>
internal static class UserDetailsChecks
{
    public const string NameMissing = "Enter the person's name";

    public const string EmailMissing = "Enter an email address";

    public const string EmailNotAnAddress = "Enter an email address in the correct format, like name@example.com";

    public const string RoleMissing = "Choose Worker, Manager or Support";

    public static readonly string NameTooLong = $"Name must be {Limits.DisplayName} characters or less";

    /// <summary>
    /// Each field of <paramref name="request"/> at fault, by the request member's C# name, with its error: a missing or
    /// overlong name, a missing or overlong address, and a missing role or one that is no role's name. Every field is
    /// checked, so that the screen shows each one's error at once; an address that is no address, or that cannot be a
    /// user name, is Identity's to find as it saves the user (<see cref="Refused"/>).
    /// </summary>
    public static Dictionary<string, string[]> Check(UserDetailsRequest request)
    {
        var nameError = request.DisplayName switch
        {
            var name when string.IsNullOrWhiteSpace(name) => NameMissing,
            { Length: > Limits.DisplayName } => NameTooLong,
            _ => null,
        };

        // The length is checked here, because Identity's check passes an address longer than the columns that hold it,
        // which would fail as a 500.
        var emailError = request.Email switch
        {
            var email when string.IsNullOrWhiteSpace(email) => EmailMissing,
            { Length: > Limits.Email } => EmailNotAnAddress,
            _ => null,
        };
        var roleError = RoleNamed(request.Role) is null ? RoleMissing : null;

        return new (string Field, string? Error)[]
            {
                (nameof(UserDetailsRequest.DisplayName), nameError),
                (nameof(UserDetailsRequest.Email), emailError),
                (nameof(UserDetailsRequest.Role), roleError),
            }
            .Where(check => check.Error is not null)
            .ToDictionary(check => check.Field, check => new[] { check.Error! });
    }

    /// <summary>
    /// The role named <paramref name="name"/> exactly, as <c>GET /api/admin/users</c> gives it, or
    /// <see langword="null"/> for any other value, never inferred (mcp-server.md §3.5, convention 4):
    /// <see cref="Enum.TryParse{TEnum}(string?, out TEnum)"/> would also take a number or another letter case.
    /// </summary>
    public static UserRole? RoleNamed(string? name) =>
        Enum.GetValues<UserRole>().Cast<UserRole?>().SingleOrDefault(role => role.ToString() == name);

    /// <summary>
    /// The refusal for the errors with which Identity refused to save a user, having saved nothing: as it creates them
    /// or as it saves their changes, it checks the address and the user name, which holds the address too (§5.3).
    /// </summary>
    /// <returns>
    /// <see cref="ErrorCode.EmailInUse"/> for an address that another account holds, in any organisation and any
    /// letter case (A27); or <see cref="ErrorCode.ValidationFailed"/> for the address that Identity finds is not an
    /// address or cannot be a user name.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Identity refused the user for a reason that only a fault can cause.
    /// </exception>
    public static UserChangeResult Refused(IReadOnlyList<IdentityError> refusal) => refusal switch
    {
        // The user name holds the address too (§5.3), so Identity also gives DuplicateUserName for it.
        _ when HasCode(refusal, nameof(IdentityErrorDescriber.DuplicateEmail)) =>
            UserChangeResult.Refused(ErrorCode.EmailInUse),

        // Identity's check of an address is loose, and the user name's characters are fewer, so one with a space, an
        // invisible character or a letter outside ASCII, which the screen's own check passes, is refused here.
        _ when HasCode(refusal, nameof(IdentityErrorDescriber.InvalidEmail))
            || HasCode(refusal, nameof(IdentityErrorDescriber.InvalidUserName)) =>
            UserChangeResult.Invalid(new Dictionary<string, string[]>
            {
                [nameof(UserDetailsRequest.Email)] = [EmailNotAnAddress],
            }),

        _ => throw new InvalidOperationException(
            $"Identity refused the user: {string.Join(" ", refusal.Select(error => error.Code))}."),
    };

    private static bool HasCode(IReadOnlyList<IdentityError> refusal, string code) =>
        refusal.Any(error => error.Code == code);
}
