namespace Grow2Notes.Web.Features.Participants;

/// <summary>
/// What <see cref="ParticipantChanges.AddAsync"/> did: added the participant, whose new ID is <paramref name="Id"/>
/// and whose <paramref name="Errors"/> are <see langword="null"/>; or refused the details, with each field's messages
/// in <paramref name="Errors"/>, and added no one, its <paramref name="Id"/> then <see cref="Guid.Empty"/>.
/// </summary>
internal sealed record AddParticipantResult(Guid Id, IReadOnlyDictionary<string, string[]>? Errors);
