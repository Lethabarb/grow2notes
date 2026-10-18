namespace Grow2Notes.Web.Features.Today;

/// <summary>
/// The Today list, as <c>GET /api/today</c> answers it (design.md §6.3): <see cref="Date"/>, the date in Melbourne
/// from <c>MelbourneClock</c>, written <c>2026-10-01</c>, which the rows' links name; and the organisation's active
/// participants, by family name, then given name (A5).
/// </summary>
internal sealed record TodayList(DateOnly Date, IReadOnlyList<TodayParticipant> Participants);

/// <summary>
/// A participant on the Today list: their ID and their names as stored, never their date of birth, which only managers
/// get (§6.3). T09 adds today's live note.
/// </summary>
internal sealed record TodayParticipant(Guid Id, string GivenName, string FamilyName);
