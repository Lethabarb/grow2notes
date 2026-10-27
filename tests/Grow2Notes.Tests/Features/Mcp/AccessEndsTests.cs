using System.Net;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Grow2Notes.Tests.Features.Mcp;

/// <summary>
/// Access that ends on the next call (S07.02.02, mcp-server.md §4.6): each event that changes a connected person is
/// done through the real endpoint or operator command, and is shown to end the person's assistant connection in its own
/// transaction, not merely at the next call that finds the person changed. A row connects the person's assistant, shows
/// one <c>/mcp</c> call succeed, and does its event; then, before any refresh or <c>/mcp</c> call, every authorisation
/// the person holds is <c>Revoked</c> (none are left once the registration is removed, which deletes them); only then
/// do the next <c>/mcp</c> call and the refresh meet the refusal. The person's session in the app then meets what the
/// event does to it, which <see cref="SignedInDevices"/> checks: the event rotates the security stamp or it does not.
/// </summary>
/// <remarks>
/// <see cref="Events"/> is the list of rows. T36 and T37 add the rows of the changes their tools make, each with the
/// tool call as its <see cref="AccessEnd.Happens"/>. The bypasses of <see cref="AssistantFlow"/> that change the
/// person's row directly (<see cref="AssistantFlow.DeactivateAsync"/> and the others) are not used here, as they
/// revoke nothing and so would prove only the check at the next call. The assistant is connected from one browser and
/// the app's own session is checked on another device: every session of the person holds the same stamp, so the second
/// meets what the first would.
/// </remarks>
[Collection<SqlServerCollection>]
public sealed class AccessEndsTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    : IClassFixture<Grow2NotesFactory>, IAsyncLifetime
{
    /// <summary>
    /// Each event with how it is done and what it does to the person's session on a device: <c>401</c> when it rotates
    /// the security stamp, <c>200</c> when it does not.
    /// </summary>
    public static TheoryData<AccessEnd> Events { get; } =
    [
        new AccessEnd("deactivate", scene => scene.PostAsync("deactivate"), HttpStatusCode.Unauthorized),
        new AccessEnd("reset sign-in", scene => scene.PostAsync("setup-link"), HttpStatusCode.Unauthorized),
        new AccessEnd(
            "admin reset-signin", scene => scene.ResetSignInAsync(), HttpStatusCode.Unauthorized),
        new AccessEnd(
            "admin signout-all", scene => scene.RunAdminCommandAsync("signout-all"), HttpStatusCode.Unauthorized),
        new AccessEnd("made a worker", scene => scene.PutAsync("Worker"), HttpStatusCode.OK),
        new AccessEnd(
            "email changed",
            scene => scene.PutAsync("Manager", $"{Guid.NewGuid():N}@example.org"),
            HttpStatusCode.Unauthorized),
        new AccessEnd(
            "signed out on another device", scene => scene.SignOutOnAnotherDeviceAsync(), HttpStatusCode.Unauthorized),
        new AccessEnd(
            "registration removed",
            scene => scene.RunAdminCommandAsync("mcp-client", "remove", AssistantFlow.ClientId),
            HttpStatusCode.OK,
            RemovesTheRegistration: true),
    ];

    private AssistantFlow flow = null!;
    private Guid personId;
    private HttpClient browser = null!;
    private HttpClient actor = null!;
    private HttpClient otherDevice = null!;

    public async ValueTask InitializeAsync()
    {
        flow = await AssistantFlow.StartAsync(factory, sqlServer, "McpAccessEnds");

        // A second manager does what needs one, as the last active manager cannot be made a worker or deactivated.
        personId = await flow.AddUserAsync(UserRole.Manager);
        var actorId = await flow.AddUserAsync(UserRole.Manager);
        browser = await flow.SignInAsync(personId);
        otherDevice = await flow.SignInAsync(personId);
        actor = await flow.SignInAsync(actorId);
    }

    public async ValueTask DisposeAsync()
    {
        actor.Dispose();
        otherDevice.Dispose();
        browser.Dispose();
        await flow.DisposeAsync();
    }

    [Theory]
    [MemberData(nameof(Events))]
    public async Task The_event_revokes_the_connection_in_its_own_change_so_the_next_call_and_the_refresh_are_refused(
        AccessEnd end)
    {
        var tokens = await flow.ConnectAsync(browser);
        using var device = await SignedInDevices.SignInAsync(flow.App, flow.Clock, personId, count: 1);
        using (var before = await flow.PostMcpAsync(tokens.AccessToken))
        {
            Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        }

        await end.Happens(new(flow, personId, actor, otherDevice, tokens));

        // Before any refresh or call to /mcp, each of which would find the person changed and revoke, had the event not.
        var statuses = await flow.AuthorisationStatusesAsync(personId);
        if (end.RemovesTheRegistration)
        {
            Assert.Empty(statuses);
        }
        else
        {
            Assert.Equal([Statuses.Revoked], statuses);
        }

        using (var after = await flow.PostMcpAsync(tokens.AccessToken))
        {
            await AssistantFlow.AssertChallengeAsync(after, Errors.InvalidToken);
        }

        using (var refresh = await flow.RefreshAsync(tokens.RefreshToken))
        {
            if (end.RemovesTheRegistration)
            {
                Assert.Equal(
                    Errors.InvalidClient, await AssistantFlow.ErrorAsync(refresh, HttpStatusCode.Unauthorized));
            }
            else
            {
                Assert.Equal(Errors.InvalidGrant, await AssistantFlow.ErrorAsync(refresh));
            }
        }

        await device.AssertNextRequestsAsync(end.Device1);
    }

    /// <summary>An event that changes the connected person, and what it does to their session on a device.</summary>
    /// <param name="Name">What happens, for the test's name.</param>
    /// <param name="Happens">Does it, through the real endpoint or command.</param>
    /// <param name="Device1">
    /// The status of the next request of the person's session on a device: <c>401</c> if the event ends the app's
    /// sessions, <c>200</c> if it leaves them.
    /// </param>
    /// <param name="RemovesTheRegistration">
    /// Whether the event removes the assistant's registration, which deletes the authorisations rather than revoking
    /// them, and makes the refresh an unknown client's.
    /// </param>
    public sealed record AccessEnd(
        string Name,
        Func<AccessEndScene, Task> Happens,
        HttpStatusCode Device1,
        bool RemovesTheRegistration = false)
    {
        public override string ToString() => Name;
    }

    /// <summary>The connected person, and the second manager and the other device that an event acts through.</summary>
    public sealed class AccessEndScene
    {
        private readonly AssistantFlow flow;
        private readonly HttpClient actor;
        private readonly HttpClient otherDevice;

        internal AccessEndScene(
            AssistantFlow flow, Guid personId, HttpClient actor, HttpClient otherDevice, AssistantTokenSet tokens)
        {
            this.flow = flow;
            this.actor = actor;
            this.otherDevice = otherDevice;
            PersonId = personId;
            Tokens = tokens;
        }

        /// <summary>The connected person, a manager.</summary>
        internal Guid PersonId { get; }

        /// <summary>The tokens of the person's connection, for the through-MCP rows of T36 and T37.</summary>
        internal AssistantTokenSet Tokens { get; }

        /// <summary>The second manager does <c>POST /api/admin/users/{id}/{action}</c> on the person.</summary>
        internal async Task PostAsync(string action)
        {
            using var response = await actor.PostWithTokenAsync(
                $"/api/admin/users/{PersonId}/{action}",
                await actor.FetchTokenAsync(),
                ifMatch: IfMatch.ETag((await flow.UserAsync(PersonId)).ConcurrencyStamp!));
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        /// <summary>
        /// The second manager does <c>PUT /api/admin/users/{id}</c> with the details the person has, but for
        /// <paramref name="role"/> and, if given, <paramref name="newEmail"/>.
        /// </summary>
        internal async Task PutAsync(string role, string? newEmail = null)
        {
            var person = await flow.UserAsync(PersonId);
            var body = new Dictionary<string, string>
            {
                ["displayName"] = person.DisplayName,
                ["email"] = newEmail ?? person.Email!,
                ["role"] = role,
            };
            using var response = await actor.PutWithTokenAsync(
                $"/api/admin/users/{PersonId}",
                await actor.FetchTokenAsync(),
                body,
                IfMatch.ETag(person.ConcurrencyStamp!));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        /// <summary>The operator runs <c>admin reset-signin</c> for the person.</summary>
        internal async Task ResetSignInAsync() =>
            await RunAdminCommandAsync("reset-signin", (await flow.UserAsync(PersonId)).Email!);

        /// <summary>The operator runs <c>admin</c> with <paramref name="arguments"/>.</summary>
        internal async Task RunAdminCommandAsync(params string[] arguments)
        {
            var run = await flow.App.RunAdminCommandAsync(arguments);
            Assert.Equal(0, run.ExitCode);
        }

        /// <summary>The person signs out of the app on a device other than the one that connected the assistant.
        /// </summary>
        internal Task SignOutOnAnotherDeviceAsync() => flow.SignOutOfTheAppAsync(otherDevice);
    }
}
