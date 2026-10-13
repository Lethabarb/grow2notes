using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests.Features.Users;

/// <summary>
/// <c>GET /api/admin/users</c> (design.md §6.6), called with the test-only sign-in (<see cref="TestSignIn"/>): a
/// manager gets every user of their own organisation and none of the other's (§5.9 items 5 and 7), each in §6.6's
/// shape, as JSON that is never stored (§6.1), and no call writes an audit event (A28). The tests add their own users
/// to seeded organisation A, an Invited worker and a Deactivated manager with addresses of their own, and change no
/// seeded row (tests/README.md). The endpoint matrix covers who may call it, and <see cref="SignedOutTests"/> what a
/// signed-out caller gets.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class UserListEndpointTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    : IClassFixture<Grow2NotesFactory>
{
    private const string Path = "/api/admin/users";

    // Made up (A35), with milliseconds, which datetime2(3) keeps, so the answer shows them.
    private static readonly DateTime InvitedAtUtc = new(2026, 10, 2, 3, 4, 5, 678, DateTimeKind.Utc);
    private static readonly DateTime ActivatedAtUtc = new(2026, 10, 3, 4, 5, 6, 789, DateTimeKind.Utc);
    private static readonly DateTime DeactivatedAtUtc = new(2026, 10, 4, 5, 6, 7, 891, DateTimeKind.Utc);

    private readonly SeededOrganisations seeded = sqlServer.Seeded;

    [Fact]
    public async Task A_s_manager_gets_every_user_of_A_and_none_of_B_s_as_JSON_that_is_never_stored()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var invited = await AddInvitedWorkerAsync();
        var deactivated = await AddDeactivatedManagerAsync();
        using var client = ClientFor(Caller.ManagerOfA);

        using var response = await client.GetAsync(Path, cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var ids = UserIds(await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken));
        Assert.Equal((await StoredUserIdsAsync(seeded.A.OrganisationId)).Order(), ids.Order());
        foreach (var userOfA in new[] { seeded.A.ManagerId, seeded.A.WorkerId, invited, deactivated })
        {
            Assert.Contains(userOfA, ids);
        }

        Assert.DoesNotContain(seeded.B.ManagerId, ids);
        Assert.DoesNotContain(seeded.B.WorkerId, ids);
    }

    [Fact]
    public async Task An_Invited_user_has_section_6_6_s_shape_with_names_UTC_times_and_their_stored_stamp()
    {
        var invited = await AddInvitedWorkerAsync();

        var user = (await ListAsync(Caller.ManagerOfA))[invited];

        AssertShape(user);
        Assert.Equal(invited, user.GetProperty("id").GetGuid());
        Assert.Equal(await StoredEmailAsync(invited), user.GetProperty("email").GetString());
        Assert.Equal("Alex Park", user.GetProperty("displayName").GetString());
        Assert.Equal("Worker", user.GetProperty("role").GetString());
        Assert.Equal("Invited", user.GetProperty("status").GetString());
        Assert.Equal("2026-10-02T03:04:05.678Z", user.GetProperty("invitedAtUtc").GetString());
        Assert.Equal(JsonValueKind.Null, user.GetProperty("activatedAtUtc").ValueKind);
        Assert.Equal(JsonValueKind.Null, user.GetProperty("deactivatedAtUtc").ValueKind);
        Assert.Equal(await StoredStampAsync(invited), user.GetProperty("concurrencyStamp").GetString());
    }

    [Fact]
    public async Task A_Deactivated_user_has_section_6_6_s_shape_with_names_UTC_times_and_their_stored_stamp()
    {
        var deactivated = await AddDeactivatedManagerAsync();

        var user = (await ListAsync(Caller.ManagerOfA))[deactivated];

        AssertShape(user);
        Assert.Equal(deactivated, user.GetProperty("id").GetGuid());
        Assert.Equal(await StoredEmailAsync(deactivated), user.GetProperty("email").GetString());
        Assert.Equal("Chris Ng", user.GetProperty("displayName").GetString());
        Assert.Equal("Manager", user.GetProperty("role").GetString());
        Assert.Equal("Deactivated", user.GetProperty("status").GetString());
        Assert.Equal("2026-10-02T03:04:05.678Z", user.GetProperty("invitedAtUtc").GetString());
        Assert.Equal("2026-10-03T04:05:06.789Z", user.GetProperty("activatedAtUtc").GetString());
        Assert.Equal("2026-10-04T05:06:07.891Z", user.GetProperty("deactivatedAtUtc").GetString());
        Assert.Equal(await StoredStampAsync(deactivated), user.GetProperty("concurrencyStamp").GetString());
    }

    // As SqlServerFixture seeds them: Active, invited and activated, and never deactivated.
    [Fact]
    public async Task The_seeded_manager_and_worker_have_section_6_6_s_shape_as_their_rows_hold_them()
    {
        var users = await ListAsync(Caller.ManagerOfA);

        foreach (var (userId, role) in new[] { (seeded.A.ManagerId, "Manager"), (seeded.A.WorkerId, "Worker") })
        {
            var user = users[userId];
            var stored = await StoredUserAsync(userId);

            AssertShape(user);
            Assert.Equal(userId, user.GetProperty("id").GetGuid());
            Assert.Equal(stored.Email, user.GetProperty("email").GetString());
            Assert.Equal(stored.DisplayName, user.GetProperty("displayName").GetString());
            Assert.Equal(role, user.GetProperty("role").GetString());
            Assert.Equal("Active", user.GetProperty("status").GetString());
            Assert.Equal(stored.InvitedAtUtc, UtcTime(user.GetProperty("invitedAtUtc")));
            Assert.Equal(stored.ActivatedAtUtc, UtcTime(user.GetProperty("activatedAtUtc")));
            Assert.Equal(JsonValueKind.Null, user.GetProperty("deactivatedAtUtc").ValueKind);
            Assert.Equal(stored.ConcurrencyStamp, user.GetProperty("concurrencyStamp").GetString());
        }
    }

    // Identity gives a user a new ConcurrencyStamp on every update, so the If-Match that the old one makes is stale.
    [Fact]
    public async Task Once_a_user_s_name_changes_through_UserManager_the_list_gives_their_new_stamp()
    {
        var invited = await AddInvitedWorkerAsync();
        var before = (await ListAsync(Caller.ManagerOfA))[invited].GetProperty("concurrencyStamp").GetString();

        await RenameAsync(invited, "Alex Parker");
        var after = (await ListAsync(Caller.ManagerOfA))[invited];

        Assert.Equal("Alex Parker", after.GetProperty("displayName").GetString());
        Assert.Equal(await StoredStampAsync(invited), after.GetProperty("concurrencyStamp").GetString());
        Assert.NotEqual(before, after.GetProperty("concurrencyStamp").GetString());
    }

    [Fact]
    public async Task B_s_manager_gets_every_user_of_B_and_none_of_A_s()
    {
        var invited = await AddInvitedWorkerAsync();
        var deactivated = await AddDeactivatedManagerAsync();

        var ids = (await ListAsync(Caller.ManagerOfB)).Keys;

        Assert.Equal((await StoredUserIdsAsync(seeded.B.OrganisationId)).Order(), ids.Order());
        Assert.Contains(seeded.B.ManagerId, ids);
        Assert.Contains(seeded.B.WorkerId, ids);
        foreach (var userOfA in new[] { seeded.A.ManagerId, seeded.A.WorkerId, invited, deactivated })
        {
            Assert.DoesNotContain(userOfA, ids);
        }
    }

    [Fact]
    public async Task A_s_worker_gets_a_403_problem_that_is_never_stored()
    {
        using var client = ClientFor(Caller.WorkerOfA);

        using var response = await client.GetAsync(Path, TestContext.Current.CancellationToken);

        await response.ReadProblemAsync(HttpStatusCode.Forbidden);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
    }

    // design.md §5.4 lists no event for reading, whether the list is given or refused (A28).
    [Fact]
    public async Task The_calls_write_no_audit_event()
    {
        var lastEventId = await LastEventIdAsync();

        foreach (var (caller, status) in new[]
        {
            (Caller.ManagerOfA, HttpStatusCode.OK),
            (Caller.ManagerOfB, HttpStatusCode.OK),
            (Caller.WorkerOfA, HttpStatusCode.Forbidden),
        })
        {
            using var client = ClientFor(caller);
            using var response = await client.GetAsync(Path, TestContext.Current.CancellationToken);

            Assert.Equal(status, response.StatusCode);
        }

        Assert.Equal(lastEventId, await LastEventIdAsync());
    }

    // §6.6's properties, in its order, and no others.
    private static void AssertShape(JsonElement user) =>
        Assert.Equal(
            [
                "id", "email", "displayName", "role", "status", "invitedAtUtc", "activatedAtUtc", "deactivatedAtUtc",
                "concurrencyStamp",
            ],
            user.EnumerateObject().Select(property => property.Name));

    // A time in UTC is written with Z, and read back as UTC.
    private static DateTime UtcTime(JsonElement time)
    {
        var text = time.GetString();
        Assert.NotNull(text);
        Assert.EndsWith("Z", text, StringComparison.Ordinal);
        var parsed = DateTime.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        Assert.Equal(DateTimeKind.Utc, parsed.Kind);
        return parsed;
    }

    private static List<Guid> UserIds(JsonElement users) =>
        [.. users.EnumerateArray().Select(user => user.GetProperty("id").GetGuid())];

    // Over HTTPS, as the SPA calls the /api group (tests/README.md).
    private HttpClient ClientFor(Caller caller) => factory.CreateHttpsClient().SignInAs(caller, seeded);

    // The answer's users by their IDs. The ToDictionary throws on an ID listed twice.
    private async Task<Dictionary<Guid, JsonElement>> ListAsync(Caller caller)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = ClientFor(caller);

        using var response = await client.GetAsync(Path, cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var users = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return users.EnumerateArray().ToDictionary(user => user.GetProperty("id").GetGuid());
    }

    private Task<Guid> AddInvitedWorkerAsync() =>
        AddUserAsync(
            "Alex Park",
            "alex.park",
            UserRole.Worker,
            UserStatus.Invited,
            activatedAtUtc: null,
            deactivatedAtUtc: null);

    // Activated, then deactivated, as Deactivate leaves an Active user (design.md §6.6).
    private Task<Guid> AddDeactivatedManagerAsync() =>
        AddUserAsync(
            "Chris Ng", "chris.ng", UserRole.Manager, UserStatus.Deactivated, ActivatedAtUtc, DeactivatedAtUtc);

    // To seeded organisation A, through the app's UserManager, as the app creates accounts, with an address that no
    // other test or run uses.
    private async Task<Guid> AddUserAsync(
        string displayName,
        string mailbox,
        UserRole role,
        UserStatus status,
        DateTime? activatedAtUtc,
        DateTime? deactivatedAtUtc)
    {
        var email = $"{mailbox}.{Guid.NewGuid():N}@example.org";
        await using var scope = factory.Services.CreateAsyncScope();
        var user = new ApplicationUser
        {
            OrganisationId = seeded.A.OrganisationId,
            DisplayName = displayName,
            Role = role,
            Status = status,
            InvitedAtUtc = InvitedAtUtc,
            InvitedByUserId = seeded.A.ManagerId,
            ActivatedAtUtc = activatedAtUtc,
            DeactivatedAtUtc = deactivatedAtUtc,
            UserName = email,
            Email = email,
        };
        var created = await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().CreateAsync(user);
        Assert.True(created.Succeeded);
        return user.Id;
    }

    private async Task RenameAsync(Guid userId, string displayName)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByIdAsync(userId.ToString());
        Assert.NotNull(user);
        user.DisplayName = displayName;
        Assert.True((await users.UpdateAsync(user)).Succeeded);
    }

    // As the row holds it, read in a scope of its own, as a request reads it.
    private async Task<ApplicationUser> StoredUserAsync(Guid userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var user = await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>()
            .FindByIdAsync(userId.ToString());
        Assert.NotNull(user);
        return user;
    }

    private async Task<string?> StoredEmailAsync(Guid userId) => (await StoredUserAsync(userId)).Email;

    private async Task<string?> StoredStampAsync(Guid userId) => (await StoredUserAsync(userId)).ConcurrencyStamp;

    // AspNetUsers has no tenant filter, so the query names the organisation itself (design.md §5.9 item 5). The tests
    // of the collection run one at a time, so no user is added between this and the call it is compared with.
    private async Task<List<Guid>> StoredUserIdsAsync(Guid organisationId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Users
            .Where(user => user.OrganisationId == organisationId)
            .Select(user => user.Id)
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    // The ID of the last event written, by any test: the tests of the collection run one at a time, so none writes
    // one while a test reads it before and after its requests.
    private async Task<long?> LastEventIdAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Database
            .SqlQuery<long?>($"SELECT MAX([Id]) AS [Value] FROM [AuditEvent]")
            .SingleAsync(TestContext.Current.CancellationToken);
    }
}
