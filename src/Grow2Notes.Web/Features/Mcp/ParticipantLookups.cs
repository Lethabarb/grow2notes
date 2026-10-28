using Grow2Notes.Web.Data;
using Grow2Notes.Web.Features.Mcp.Contracts;
using Grow2Notes.Web.Features.Participants;
using Grow2Notes.Web.Platform.Configuration;
using Microsoft.EntityFrameworkCore;

namespace Grow2Notes.Web.Features.Mcp;

/// <summary>
/// The reads behind the participant tools (mcp-server.md §3.5): names, status, goals and versions, projected so that
/// the date of birth is never loaded. Every read is <c>AsNoTracking</c> under the tenant query filter, which keeps it
/// to the token's organisation (design.md §5.9 item 2).
/// </summary>
internal sealed class ParticipantLookups(Grow2NotesDbContext db)
{
    /// <summary>What <see cref="FoundParticipants.MoreMatches"/> says.</summary>
    public const string MoreMatches = "More matches: narrow the search.";

    private const int MostMatches = 10;

    /// <summary>
    /// The participants whose given or family name holds every word of <paramref name="search"/>
    /// (<see cref="Matches"/>), at most 10, in the app's list order (<see cref="ParticipantList.InListOrder"/>), the
    /// archived ones only with <paramref name="includeArchived"/>.
    /// </summary>
    /// <remarks>
    /// The names of the organisation are read and matched in memory, as an organisation has tens of participants
    /// (design.md §6.1), so the search text never reaches SQL or the logs of EF Core. Reading an eleventh match decides
    /// <see cref="FoundParticipants.MoreMatches"/>; it is not returned.
    /// </remarks>
    public async Task<FoundParticipants> FindAsync(
        string search, bool includeArchived, CancellationToken cancellationToken)
    {
        var words = search.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var participants = db.Participants.AsNoTracking();
        if (!includeArchived)
        {
            participants = participants.Where(participant => participant.Status == ParticipantStatus.Active);
        }

        var names = await ParticipantList.InListOrder(participants)
            .Select(participant => new
            {
                participant.Id,
                participant.GivenName,
                participant.FamilyName,
                participant.Status,
            })
            .ToListAsync(cancellationToken);

        var matches = names
            .Where(name => Matches(words, name.GivenName, name.FamilyName))
            .Take(MostMatches + 1)
            .ToList();
        return new(
            [.. matches.Take(MostMatches).Select(
                match => new ParticipantMatch(match.Id, match.GivenName, match.FamilyName, StatusOf(match.Status)))],
            matches.Count > MostMatches ? MoreMatches : null);
    }

    /// <summary>
    /// The participant of <paramref name="id"/> with their goals, the active ones first, then the archived ones, each
    /// in the list's order (<see cref="GoalList.InListOrder"/>); or null when the organisation has no such
    /// participant, another organisation's included (design.md §9.2). An archived participant is returned.
    /// </summary>
    /// <remarks>
    /// The participant and the goals are two reads, and each row carries its own version, so a change between them
    /// does no harm.
    /// </remarks>
    public async Task<ParticipantWithGoals?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var participant = await db.Participants.AsNoTracking()
            .Where(participant => participant.Id == id)
            .Select(participant => new
            {
                participant.GivenName,
                participant.FamilyName,
                participant.Status,
                participant.RowVersion,
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (participant is null)
        {
            return null;
        }

        var goals = await GoalList.InListOrder(db.Goals.AsNoTracking().Where(goal => goal.ParticipantId == id))
            .Select(goal => new { goal.Id, goal.Text, Archived = goal.ArchivedAtUtc != null, goal.RowVersion })
            .ToListAsync(cancellationToken);

        return new(
            id,
            participant.GivenName,
            participant.FamilyName,
            StatusOf(participant.Status),
            RowVersionToken.Of(participant.RowVersion),
            [.. goals.OrderBy(goal => goal.Archived).Select(
                goal => new ParticipantGoal(goal.Id, goal.Text, goal.Archived, RowVersionToken.Of(goal.RowVersion)))]);
    }

    /// <summary>
    /// The goal of <paramref name="goalId"/> with its current version, archived or not, or null when the organisation
    /// has no such goal, another organisation's included (design.md §9.2). One untracked, projected read.
    /// </summary>
    public async Task<ParticipantGoal?> GetGoalAsync(Guid goalId, CancellationToken cancellationToken)
    {
        var goal = await db.Goals.AsNoTracking()
            .Where(goal => goal.Id == goalId)
            .Select(goal => new { goal.Id, goal.Text, Archived = goal.ArchivedAtUtc != null, goal.RowVersion })
            .SingleOrDefaultAsync(cancellationToken);
        return goal is null ? null : new(goal.Id, goal.Text, goal.Archived, RowVersionToken.Of(goal.RowVersion));
    }

    /// <summary>
    /// Whether every word is held, ignoring case, by the given name or the family name, in any order. Nothing is
    /// normalised: <c>zoe</c> does not match <c>Zoë</c>, and the two apostrophes are two characters (A124).
    /// </summary>
    public static bool Matches(IReadOnlyCollection<string> words, string givenName, string familyName) =>
        words.All(word => givenName.Contains(word, StringComparison.OrdinalIgnoreCase)
            || familyName.Contains(word, StringComparison.OrdinalIgnoreCase));

    /// <summary>The status as the tools say it, in lower case (A126).</summary>
    public static string StatusOf(ParticipantStatus status) => status switch
    {
        ParticipantStatus.Active => "active",
        ParticipantStatus.Archived => "archived",
    };
}
