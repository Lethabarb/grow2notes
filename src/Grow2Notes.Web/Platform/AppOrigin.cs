namespace Grow2Notes.Web.Platform;

/// <summary>
/// The address the app is reached at, from the <c>App:Origin</c> setting: a scheme, a host and, where it is not the
/// scheme's default, a port, with no path, such as <c>https://app-grow2notes-test.azurewebsites.net</c> in test. The
/// links the app gives out start with it, such as an invite's setup link (design.md §8.1 step 2), because they are
/// made where there is no request to take the address from, as in an operator command (§7.4). main.bicep sets it in
/// Azure.
/// </summary>
internal static class AppOrigin
{
    /// <summary>The setting's name.</summary>
    public const string Key = "App:Origin";

    /// <summary>The origin that <paramref name="configuration"/> sets, or null when it sets none.</summary>
    /// <exception cref="UriFormatException">The setting is not an absolute address.</exception>
    public static Uri? Find(IConfiguration configuration) =>
        configuration[Key] is { Length: > 0 } origin ? new Uri(origin, UriKind.Absolute) : null;
}
