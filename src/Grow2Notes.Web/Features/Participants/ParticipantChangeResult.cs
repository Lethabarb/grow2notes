using Grow2Notes.Web.Platform;
using Grow2Notes.Web.Platform.Configuration;

namespace Grow2Notes.Web.Features.Participants;

/// <summary>
/// What <see cref="ParticipantChanges"/> did with a change to a participant who exists: found no such participant in
/// the tenant's organisation, another organisation's included (design.md §9.2); refused the change with a design.md
/// §6.9 code, <c>If-Match</c>'s or the save's (<see cref="Error"/>); refused fields, with each one's messages
/// (<see cref="Errors"/>); or took it, and saved it with its audit event only when a value changed
/// (<see cref="Changed"/>), which Release 2's tools give as <c>changed</c> (mcp-server.md §5.1).
/// </summary>
internal sealed record ParticipantChangeResult
{
    /// <summary>No participant of that ID is in the tenant's organisation, so nothing was changed.</summary>
    public static readonly ParticipantChangeResult NotFound = new() { Found = false };

    private ParticipantChangeResult()
    {
    }

    public bool Found { get; private init; } = true;

    /// <summary>The code that the change was refused with, or <see langword="null"/>.</summary>
    public ErrorCode? Error { get; private init; }

    /// <summary>
    /// Each refused field's messages, keyed by its member's name in the request, or <see langword="null"/>.
    /// </summary>
    public IReadOnlyDictionary<string, string[]>? Errors { get; private init; }

    /// <summary>
    /// Whether the change was saved, with its event: <see langword="false"/> for one that changed no value.
    /// </summary>
    public bool Changed { get; private init; }

    /// <summary>The change was refused with <paramref name="error"/> before anything was saved.</summary>
    public static ParticipantChangeResult Refused(ErrorCode error) => new() { Error = error };

    /// <summary>The change was refused for the fields <paramref name="errors"/> name, and nothing was saved.</summary>
    public static ParticipantChangeResult Invalid(IReadOnlyDictionary<string, string[]> errors) =>
        new() { Errors = errors };

    /// <summary>
    /// What <see cref="ConfigurationChanges.SaveAsync"/> did: saved the change, found nothing to change, or found the
    /// participant changed since it was loaded, which refuses it with <see cref="ErrorCode.PreconditionFailed"/>.
    /// </summary>
    public static ParticipantChangeResult Of(ConfigurationSaveResult saved) =>
        new() { Error = saved.Error, Changed = saved.Changed };
}
