using System.Globalization;
using System.Text.Json.Nodes;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Grow2Notes.Web.Platform.Audit;
using Grow2Notes.Web.Platform.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;

namespace Grow2Notes.Web.Features.Participants;

/// <summary>
/// The handler that changes an organisation's participants (design.md §6.6), as a handler class that takes the
/// <see cref="Actor"/> (mcp-server.md §3.5, convention 1): <c>POST /api/admin/participants</c>,
/// <c>PUT /api/admin/participants/{id}</c> and <c>POST /api/admin/participants/{id}/archive</c> and <c>/restore</c>
/// call it with <see cref="Actor.InApp"/>, and the assistant's tools call it with <see cref="Channel.Mcp"/>.
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
    /// Corrects a participant's details in the tenant's organisation (S01.02.03): loads them through the tenant filter,
    /// so another organisation's is not found; checks <paramref name="ifMatch"/> against their version; reads
    /// <paramref name="request"/> by the add's rules (<see cref="ParticipantDetails.TryRead"/>); then saves the details
    /// with <c>participant.updated</c>, whose <c>Details</c> hold the old and new value of each changed field alone;
    /// or, when no value changed, saves nothing and writes no event, and answers as a save does (D84). An archived
    /// participant is not corrected (D86, D93): they are refused with <see cref="ErrorCode.ParticipantArchived"/>
    /// before <paramref name="ifMatch"/> is looked at. A call by <see cref="Channel.Mcp"/> may leave a field out, which
    /// keeps its stored value (<see cref="ParticipantDetailsRequest.OrStored"/>); the app's correction replaces all
    /// three.
    /// </summary>
    /// <param name="actor">Who corrects them.</param>
    /// <param name="participantId">The participant.</param>
    /// <param name="ifMatch">
    /// The request's <c>If-Match</c> values, as <c>Headers.IfMatch</c> gives them, or Release 2's
    /// <c>expectedVersion</c> through <see cref="IfMatch.ETag"/>; <see cref="IfMatch.CheckFor"/> gives the app's
    /// refusal at once and Release 2's when a value would change.
    /// </param>
    /// <param name="request">The details as sent.</param>
    /// <param name="cancellationToken">Cancels the correction.</param>
    public Task<ParticipantChangeResult> UpdateAsync(
        Actor actor,
        Guid participantId,
        StringValues ifMatch,
        ParticipantDetailsRequest request,
        CancellationToken cancellationToken) =>
        ChangeAsync(
            actor,
            participantId,
            ifMatch,
            refuseArchived: true,
            async (participant, refuseIfChanged) =>
            {
                var sent = actor.Channel == Channel.Mcp ? request.OrStored(participant) : request;
                if (!ParticipantDetails.TryRead(sent, clock.Today(), out var details, out var errors))
                {
                    return ParticipantChangeResult.Invalid(errors);
                }

                var before = Values(participant);
                participant.GivenName = details.GivenName;
                participant.FamilyName = details.FamilyName;
                participant.DateOfBirth = details.DateOfBirth;
                return await SaveAsync(
                    actor, AuditEventTypes.Participant.Updated, participant, before, Values(participant),
                    refuseIfChanged, cancellationToken);
            },
            cancellationToken);

    /// <summary>
    /// Archives a participant in the tenant's organisation (design.md §3.7, S01.02.04), once <paramref name="ifMatch"/>
    /// is their version, as <see cref="UpdateAsync"/> checks it: sets <c>ArchivedAtUtc</c> through
    /// <see cref="Archiving.Archive"/> and <c>Status</c> beside it, and saves them with <c>participant.archived</c>,
    /// whose <c>Details</c> hold both before and after. A participant archived already keeps the time they were
    /// archived at, so nothing is saved and no event written, and the answer is as a save's (D84).
    /// </summary>
    public Task<ParticipantChangeResult> ArchiveAsync(
        Actor actor, Guid participantId, StringValues ifMatch, CancellationToken cancellationToken) =>
        ChangeAsync(
            actor,
            participantId,
            ifMatch,
            refuseArchived: false,
            (participant, refuseIfChanged) =>
            {
                var before = StatusValues(participant);
                Archiving.Archive(participant, timeProvider);
                participant.Status = ParticipantStatus.Archived;
                return SaveAsync(
                    actor, AuditEventTypes.Participant.Archived, participant, before, StatusValues(participant),
                    refuseIfChanged, cancellationToken);
            },
            cancellationToken);

    /// <summary>
    /// Restores a participant in the tenant's organisation (design.md §3.7, S01.02.04), once <paramref name="ifMatch"/>
    /// is their version: sets <c>Status</c> back to active and clears <c>ArchivedAtUtc</c> itself, as
    /// <see cref="Archiving.Restore"/> is for a row of an ordered list, and saves them with
    /// <c>participant.restored</c>. An active participant changes nothing, so nothing is saved and no event written,
    /// and the answer is as a save's (D84). It is how an archived participant's details and goals can be changed
    /// again (D93).
    /// </summary>
    public Task<ParticipantChangeResult> RestoreAsync(
        Actor actor, Guid participantId, StringValues ifMatch, CancellationToken cancellationToken) =>
        ChangeAsync(
            actor,
            participantId,
            ifMatch,
            refuseArchived: false,
            (participant, refuseIfChanged) =>
            {
                var before = StatusValues(participant);
                participant.Status = ParticipantStatus.Active;
                participant.ArchivedAtUtc = null;
                return SaveAsync(
                    actor, AuditEventTypes.Participant.Restored, participant, before, StatusValues(participant),
                    refuseIfChanged, cancellationToken);
            },
            cancellationToken);

    /// <summary>
    /// Loads the participant through the tenant filter, so that another organisation's is not found (design.md §9.2),
    /// refuses an archived one when <paramref name="refuseArchived"/> (D93), and checks <paramref name="ifMatch"/>
    /// against their version (<see cref="IfMatch.CheckFor"/>) before <paramref name="change"/> changes and saves them,
    /// with the refusal to pass on to the save when the channel holds it back.
    /// </summary>
    /// <param name="refuseArchived">
    /// Whether an archived participant is refused: for a correction, not for archiving or restoring them, which is how
    /// an archived participant is restored.
    /// </param>
    private async Task<ParticipantChangeResult> ChangeAsync(
        Actor actor,
        Guid participantId,
        StringValues ifMatch,
        bool refuseArchived,
        Func<Participant, ErrorCode?, Task<ParticipantChangeResult>> change,
        CancellationToken cancellationToken)
    {
        var participant = await db.Participants.SingleOrDefaultAsync(p => p.Id == participantId, cancellationToken);
        if (participant is null)
        {
            return ParticipantChangeResult.NotFound;
        }

        if (refuseArchived && participant.Status == ParticipantStatus.Archived)
        {
            return ParticipantChangeResult.Refused(ErrorCode.ParticipantArchived);
        }

        var version = IfMatch.CheckFor(actor, ifMatch, RowVersionToken.Of(participant));
        return version.RefuseNow is { } refused
            ? ParticipantChangeResult.Refused(refused)
            : await change(participant, version.RefuseIfChanged);
    }

    // The change to a participant who exists, with its event, whose entity and participant are both them; or nothing
    // when no value changed.
    private async Task<ParticipantChangeResult> SaveAsync(
        Actor actor,
        string eventType,
        Participant participant,
        JsonObject before,
        JsonObject after,
        ErrorCode? refuseIfChanged,
        CancellationToken cancellationToken) =>
        ParticipantChangeResult.Of(await changes.SaveAsync(
            actor,
            eventType,
            AuditEntityTypes.Participant,
            participant.Id,
            participant.Id,
            before,
            after,
            refuseIfChanged: refuseIfChanged,
            cancellationToken: cancellationToken));

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

    /// <summary>
    /// A participant's status and the time they were archived at, the values that archiving and restoring change, as
    /// their events record them: <c>status</c> by its name, and <c>archivedAtUtc</c> as stored, or JSON <c>null</c>
    /// (<see cref="ChangeDetails"/>).
    /// </summary>
    private static JsonObject StatusValues(Participant participant) => new()
    {
        ["status"] = participant.Status.ToString(),
        ["archivedAtUtc"] = participant.ArchivedAtUtc,
    };
}
