namespace Grow2Notes.Tests.Fixtures;

/// <summary>
/// The four callers of the endpoint matrix (design.md §2, §9.2): signed out, and, with the test-only sign-in, seeded
/// organisation A's worker and manager and B's manager (<see cref="SeededOrganisations"/>).
/// <see cref="TestSignIn.SignInAs(HttpClient, Caller, SeededOrganisations)"/> calls as each.
/// </summary>
public enum Caller
{
    SignedOut,
    WorkerOfA,
    ManagerOfA,
    ManagerOfB,
}
