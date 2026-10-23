using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Grow2Notes.Web.Platform.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests.Fixtures;

/// <summary>
/// A participant that a test adds to a seeded organisation, so that it changes one of its own and never the seeded
/// participant (tests/README.md): their ID, and their <c>ETag</c>, the <c>If-Match</c> that a change to them sends.
/// </summary>
internal sealed record TestParticipant(Guid Id, string ETag);

internal static class TestParticipants
{
    /// <summary>When a participant that <see cref="AddParticipantAsync"/> adds archived was archived.</summary>
    public static readonly DateTime ArchivedAtUtc = new(2026, 10, 2, 3, 4, 5, 678, DateTimeKind.Utc);

    private static readonly DateTime AddedAtUtc = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Adds a participant to <paramref name="organisation"/>, through the app's context under its tenant, as the app
    /// adds one: <paramref name="givenName"/> <paramref name="familyName"/>, Jane Citizen unless given, born on 7
    /// March 1987, active, or, with <paramref name="archived"/>, archived at <see cref="ArchivedAtUtc"/>. Each call
    /// adds one more, with an ID of its own.
    /// </summary>
    public static async Task<TestParticipant> AddParticipantAsync(
        this IServiceProvider services,
        SeededOrganisation organisation,
        bool archived = false,
        string givenName = "Jane",
        string familyName = "Citizen")
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        var participant = new Participant
        {
            OrganisationId = organisation.OrganisationId,
            GivenName = givenName,
            FamilyName = familyName,
            DateOfBirth = new DateOnly(1987, 3, 7),
            CreatedAtUtc = AddedAtUtc,
            Status = archived ? ParticipantStatus.Archived : ParticipantStatus.Active,
            ArchivedAtUtc = archived ? ArchivedAtUtc : null,
        };
        db.Participants.Add(participant);
        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(organisation.OrganisationId))
        {
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // EF Core reads back the version that SQL Server gave the row as it saved it.
        return new(participant.Id, RowVersionToken.ETag(participant));
    }
}
