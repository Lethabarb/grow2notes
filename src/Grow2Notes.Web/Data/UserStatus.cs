namespace Grow2Notes.Web.Data;

/// <summary>
/// Where an account is in its life (design.md §5.3). Only an Active user can sign in. Stored as <c>tinyint</c> with
/// fixed values, so a stored status never changes meaning.
/// </summary>
internal enum UserStatus : byte
{
    Invited = 0,
    Active = 1,
    Deactivated = 2,
}
