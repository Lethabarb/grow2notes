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
    private static readonly DateTime AddedAtUtc = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Adds an active participant to <paramref name="organisation"/>, through the app's context under its tenant, as
    /// the app adds one: Jane Citizen, born on 7 March 1987. Each call adds one more, with an ID of its own.
    /// </summary>
    public static async Task<TestParticipant> AddParticipantAsync(
        this IServiceProvider services, SeededOrganisation organisation)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        var participant = new Participant
        {
            OrganisationId = organisation.OrganisationId,
            GivenName = "Jane",
            FamilyName = "Citizen",
            DateOfBirth = new DateOnly(1987, 3, 7),
            CreatedAtUtc = AddedAtUtc,
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
