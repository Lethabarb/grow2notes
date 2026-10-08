namespace Grow2Notes.Tests.Fixtures;

/// <summary>
/// The two made-up organisations that <see cref="SqlServerFixture"/> adds to the app's database after migrating it, for
/// the two-organisation tests (design.md §5.9 item 7): a test acts in <see cref="A"/> and checks that nothing of
/// <see cref="B"/> reaches it.
/// </summary>
public sealed record SeededOrganisations(SeededOrganisation A, SeededOrganisation B);

/// <summary>
/// A seeded organisation and its two users, an Active manager and an Active worker, as setup leaves them: with a
/// confirmed <c>example.org</c> address and a security stamp, but no passkey or password yet.
/// </summary>
public sealed record SeededOrganisation(Guid OrganisationId, Guid ManagerId, Guid WorkerId);
