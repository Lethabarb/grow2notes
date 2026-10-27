namespace Grow2Notes.Web.Platform;

/// <summary>
/// What <see cref="IfMatch.CheckFor"/> makes of a change's version, split by when the refusal is given: the app refuses
/// at once (<see cref="RefuseNow"/>), and an assistant's tool holds the refusal back until the save finds that the call
/// would change something (<see cref="RefuseIfChanged"/>), as a call that changes nothing answers <c>changed: false</c>
/// whatever the version (mcp-server.md §5.1). At most one is set.
/// </summary>
/// <param name="RefuseNow">
/// The code to refuse the change with before anything else is done, or <see langword="null"/>.
/// </param>
/// <param name="RefuseIfChanged">
/// The code to refuse the change with unless it would change nothing, or <see langword="null"/>; it goes to
/// <see cref="Configuration.ConfigurationChanges.SaveAsync"/>.
/// </param>
internal readonly record struct VersionCheck(ErrorCode? RefuseNow, ErrorCode? RefuseIfChanged);
