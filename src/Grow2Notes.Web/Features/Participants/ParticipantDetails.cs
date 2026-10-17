using System.Diagnostics.CodeAnalysis;
using Grow2Notes.Web.Data;

namespace Grow2Notes.Web.Features.Participants;

/// <summary>
/// A participant's details as they are stored (design.md §5.3, A5): the given and family names, each with its ends
/// trimmed and nothing else changed, never re-cased, and the date of birth.
/// </summary>
internal sealed record ParticipantDetails(string GivenName, string FamilyName, DateOnly DateOfBirth)
{
    /// <summary>The message for a given name that is missing, or nothing once its ends are trimmed.</summary>
    public const string EnterGivenName = "Enter the given name";

    /// <summary>The message for a family name that is missing, or nothing once its ends are trimmed.</summary>
    public const string EnterFamilyName = "Enter the family name";

    /// <summary>The message for a date of birth that is missing.</summary>
    public const string EnterDateOfBirth = "Enter the date of birth";

    /// <summary>The message for a date of birth that is today or later in Melbourne.</summary>
    public const string DateOfBirthInThePast = "Date of birth must be in the past";

    /// <summary>The message for a given name longer than <see cref="Limits.ParticipantName"/> once trimmed.</summary>
    public static readonly string GivenNameTooLong = TooLong("Given name");

    /// <summary>The message for a family name longer than <see cref="Limits.ParticipantName"/> once trimmed.</summary>
    public static readonly string FamilyNameTooLong = TooLong("Family name");

    /// <summary>
    /// Reads <paramref name="request"/> by participants.md's Details rules, as the server checks them: given and family
    /// names of 1 to <see cref="Limits.ParticipantName"/> characters once their ends are trimmed, and a date of birth
    /// before <paramref name="today"/>. Every field is checked, and each refused one gets its first failing check's
    /// message, worded as the screens word it, which Release 2's tools give too (mcp-server.md §5.5).
    /// </summary>
    /// <param name="request">The details as sent.</param>
    /// <param name="today">The date in Melbourne (<c>MelbourneClock</c>), never the machine's.</param>
    /// <param name="details">The details to store, when no field was refused.</param>
    /// <param name="errors">
    /// Each refused field's message, keyed by its member's name in <see cref="ParticipantDetailsRequest"/>, in the
    /// fields' order; empty when none was refused.
    /// </param>
    public static bool TryRead(
        ParticipantDetailsRequest request,
        DateOnly today,
        [NotNullWhen(true)] out ParticipantDetails? details,
        out IReadOnlyDictionary<string, string[]> errors)
    {
        Dictionary<string, string[]> refused = [];
        var givenName = Name(
            request.GivenName, EnterGivenName, GivenNameTooLong, nameof(request.GivenName), refused);
        var familyName = Name(
            request.FamilyName, EnterFamilyName, FamilyNameTooLong, nameof(request.FamilyName), refused);
        var dateOfBirth = BornBefore(today, request.DateOfBirth, nameof(request.DateOfBirth), refused);

        errors = refused;
        details = givenName is not null && familyName is not null && dateOfBirth is { } born
            ? new(givenName, familyName, born)
            : null;
        return details is not null;
    }

    // microcopy.md §4's Field error, too long, with the name's limit.
    private static string TooLong(string field) => $"{field} must be {Limits.ParticipantName} characters or less";

    // The name with its ends trimmed, or null, with its message, when it is missing, empty or too long. Its length is
    // counted in UTF-16 units, as the column's size and the SPA's check count it.
    private static string? Name(
        string? typed, string enter, string tooLong, string field, Dictionary<string, string[]> refused)
    {
        var name = typed?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            refused[field] = [enter];
            return null;
        }

        if (name.Length > Limits.ParticipantName)
        {
            refused[field] = [tooLong];
            return null;
        }

        return name;
    }

    // The date of birth, or null, with its message, when it is missing or not before today.
    private static DateOnly? BornBefore(
        DateOnly today, DateOnly? sent, string field, Dictionary<string, string[]> refused)
    {
        if (sent is not { } dateOfBirth)
        {
            refused[field] = [EnterDateOfBirth];
            return null;
        }

        if (dateOfBirth >= today)
        {
            refused[field] = [DateOfBirthInThePast];
            return null;
        }

        return dateOfBirth;
    }
}
