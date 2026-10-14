using System.Globalization;
using System.Text.Json.Nodes;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Grow2Notes.Web.Platform.Audit;
using Grow2Notes.Web.Platform.Configuration;

namespace Grow2Notes.Web.Features.Participants;

/// <summary>
/// The handler that changes an organisation's participants (design.md §6.6), as a handler class that takes the
/// <see cref="Actor"/> (mcp-server.md §3.5, convention 1): <c>POST /api/admin/participants</c> calls it with
/// <see cref="Actor.InApp"/>, and Release 2's <c>add_participant</c> tool will call it with <see cref="Channel.Mcp"/>.
/// Each change is saved with its audit event through <see cref="ConfigurationChanges"/>, in the tenant's organisation.
/// </summary>
internal sealed class ParticipantChanges(
    Grow2NotesDbContext db,
    ITenantContext tenant,
    ConfigurationChanges changes,
    MelbourneClock clock,
    TimeProvider timeProvider)
{
    /// <summary>
    /// Adds a participant to the tenant's organisation, Active, with the details that <paramref name="request"/> gives
    /// once <see cref="ParticipantDetails.TryRead"/> has read them against today in Melbourne, and saves them with
    /// <c>participant.created</c>, whose entity and participant are the new participant and whose <c>Details</c> hold
    /// their values as <c>new</c> alone (S01.02.02's Notes); or, when a field is refused, adds no one and writes no
    /// event.
    /// </summary>
    public async Task<AddParticipantResult> AddAsync(
        Actor actor, ParticipantDetailsRequest request, CancellationToken cancellationToken)
    {
        if (!ParticipantDetails.TryRead(request, clock.Today(), out var details, out var errors))
        {
            return new(Guid.Empty, errors);
        }

        var participant = new Participant
        {
            OrganisationId = tenant.OrganisationId,
            GivenName = details.GivenName,
            FamilyName = details.FamilyName,
            DateOfBirth = details.DateOfBirth,
            CreatedAtUtc = timeProvider.GetUtcNow().UtcDateTime,
        };
        db.Participants.Add(participant);

        // A new row has no version that could have changed since it was loaded, so the save is done or it throws.
        await changes.SaveAsync(
            actor,
            AuditEventTypes.Participant.Created,
            AuditEntityTypes.Participant,
            participant.Id,
            participant.Id,
            before: null,
            Values(participant),
            cancellationToken: cancellationToken);
        return new(participant.Id, Errors: null);
    }

    /// <summary>
    /// A participant's values as an audit event records them, named and written as design.md §6.6's list gives them,
    /// never <c>id</c> or <c>rowVersion</c> (<see cref="ChangeDetails"/>).
    /// </summary>
    private static JsonObject Values(Participant participant) => new()
    {
        ["givenName"] = participant.GivenName,
        ["familyName"] = participant.FamilyName,
        ["dateOfBirth"] = participant.DateOfBirth.ToString("O", CultureInfo.InvariantCulture),
        ["status"] = participant.Status.ToString(),
    };
}
