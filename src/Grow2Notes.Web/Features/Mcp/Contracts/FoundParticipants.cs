namespace Grow2Notes.Web.Features.Mcp.Contracts;

/// <summary>
/// What <c>find_participants</c> returns (mcp-server.md §5.2): at most 10 participants in the app's list order.
/// </summary>
/// <param name="Participants">The matches; an empty list when nobody matches, which is not an error.</param>
/// <param name="MoreMatches">
/// <see cref="ParticipantLookups.MoreMatches"/> when an eleventh match exists, else null, which leaves the key out and
/// out of the output schema's required list.
/// </param>
internal sealed record FoundParticipants(IReadOnlyList<ParticipantMatch> Participants, string? MoreMatches = null);
