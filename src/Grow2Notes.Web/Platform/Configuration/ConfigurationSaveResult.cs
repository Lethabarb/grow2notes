namespace Grow2Notes.Web.Platform.Configuration;

/// <summary>
/// What <see cref="ConfigurationChanges.SaveAsync"/> did with a change: saved it with its audit event, found nothing to
/// change, found the row changed since it was loaded, or refused it as the caller asked. The constructor is private, so
/// these are the only ways.
/// </summary>
internal sealed class ConfigurationSaveResult
{
    /// <summary>The change and its audit event are saved.</summary>
    public static readonly ConfigurationSaveResult Saved = new(changed: true, error: null);

    /// <summary>No value changed, so nothing was saved and no event written (D84).</summary>
    public static readonly ConfigurationSaveResult Unchanged = new(changed: false, error: null);

    /// <summary>
    /// The row changed after it was loaded, or was made up with another organisation's key (A48), so the save matched
    /// no row, and neither the change nor its event was written.
    /// </summary>
    public static readonly ConfigurationSaveResult PreconditionFailed =
        new(changed: false, ErrorCode.PreconditionFailed);

    /// <summary>
    /// The change would have changed a value and the caller asked for it to be refused then (<paramref name="error"/>),
    /// so neither the change nor its event was written.
    /// </summary>
    public static ConfigurationSaveResult Refused(ErrorCode error) => new(changed: false, error);

    private ConfigurationSaveResult(bool changed, ErrorCode? error)
    {
        Changed = changed;
        Error = error;
    }

    /// <summary>
    /// Whether the change was saved, which Release 2's tools give as <c>changed</c> (mcp-server.md §5.1).
    /// </summary>
    public bool Changed { get; }

    /// <summary>
    /// The design.md §6.9 code that the handler answers with, or <see langword="null"/> when it answers as a save does.
    /// </summary>
    public ErrorCode? Error { get; }
}
