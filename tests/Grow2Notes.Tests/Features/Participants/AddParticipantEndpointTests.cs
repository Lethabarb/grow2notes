using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Grow2Notes.Tests.Features.Participants;

/// <summary>
/// <c>POST /api/admin/participants</c> (design.md §6.6, S01.02.02), called with the test-only sign-in
/// (<see cref="TestSignIn"/>) on a fake clock: a manager adds a participant to their own organisation, Active, with
/// the names as typed but for their trimmed ends and a date of birth before today in Melbourne, and gets <c>201</c>
/// with the new ID alone; a field that breaks a rule gets <c>422 validation.failed</c> naming it in participants.md's
/// words, and adds no one and writes no event. The tests add their own participants to the seeded organisations and
/// change no seeded row (tests/README.md). The endpoint matrix covers the statuses of every caller, and
/// <see cref="Platform.Audit.AuditEventsTests"/> the <c>participant.created</c> that an add writes.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class AddParticipantEndpointTests : IClassFixture<Grow2NotesFactory>, IAsyncDisposable
{
    private const string Path = "/api/admin/participants";

    // 00:30 on 10 October 2026 in Melbourne, daylight saving time (UTC+11), while it is still 9 October in UTC, so that
    // the date of birth is checked against Melbourne's date, not UTC's or the machine's.
    private static readonly DateOnly Today = new(2026, 10, 10);

    private readonly WebApplicationFactory<Program> app;

    private readonly SeededOrganisations seeded;

    public AddParticipantEndpointTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    {
        app = factory.WithWebHostBuilder(builder => builder.UseClock(new FakeTimeProvider(TestClock.Start)));
        seeded = sqlServer.Seeded;
    }

    public static TheoryData<string?> BlankNames { get; } = new("", "   ", "\t \n", null);

    [Fact]
    public async Task A_manager_s_add_answers_201_with_the_new_ID_alone_and_stores_them_Active_in_their_organisation()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        using var response = await PostAsync(
            Caller.ManagerOfA, new { givenName = "Jane", familyName = "Citizen", dateOfBirth = "1987-03-07" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Null(response.Headers.Location);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        Assert.Equal(["id"], body.EnumerateObject().Select(property => property.Name));
        var id = body.GetProperty("id").GetGuid();
        var stored = await StoredAsync(seeded.A, id);
        Assert.NotNull(stored);
        Assert.Equal(
            ("Jane", "Citizen", new DateOnly(1987, 3, 7), ParticipantStatus.Active, (DateTime?)null),
            (stored.GivenName, stored.FamilyName, stored.DateOfBirth, stored.Status, stored.ArchivedAtUtc));
        Assert.Equal(TestClock.Start.UtcDateTime, stored.CreatedAtUtc);
        Assert.Contains(id, await ListedIdsAsync(Caller.ManagerOfA));
    }

    // The organisation is the session's, never the body's, so B's manager adds to B, and A's manager never sees them.
    [Fact]
    public async Task B_s_manager_adds_to_B_alone()
    {
        using var response = await PostAsync(
            Caller.ManagerOfB, new { givenName = "Tom", familyName = "Archer", dateOfBirth = "1990-05-01" });

        var id = await NewIdAsync(response);
        Assert.NotNull(await StoredAsync(seeded.B, id));
        Assert.Null(await StoredAsync(seeded.A, id));
        Assert.Contains(id, await ListedIdsAsync(Caller.ManagerOfB));
        Assert.DoesNotContain(id, await ListedIdsAsync(Caller.ManagerOfA));
    }

    // Every character accepted and nothing re-cased (participants.md B5): only the ends are trimmed, spaces, a tab and
    // a line break among them.
    [Theory]
    [InlineData("  Ravi ", "de Silva\t", "Ravi", "de Silva")]
    [InlineData("Seán", " O'Brien", "Seán", "O'Brien")]
    [InlineData("Lan", "Nguyễn\n", "Lan", "Nguyễn")]
    [InlineData("\tMary ", "Smith-Jones  ", "Mary", "Smith-Jones")]
    public async Task Names_are_stored_and_listed_as_typed_with_only_their_ends_trimmed(
        string givenName, string familyName, string storedGivenName, string storedFamilyName)
    {
        using var response = await PostAsync(
            Caller.ManagerOfA, new { givenName, familyName, dateOfBirth = "1987-03-07" });

        var id = await NewIdAsync(response);
        var stored = await StoredAsync(seeded.A, id);
        Assert.NotNull(stored);
        Assert.Equal((storedGivenName, storedFamilyName), (stored.GivenName, stored.FamilyName));
        var listed = (await ListAsync(Caller.ManagerOfA)).Single(participant => Id(participant) == id);
        Assert.Equal(storedGivenName, listed.GetProperty("givenName").GetString());
        Assert.Equal(storedFamilyName, listed.GetProperty("familyName").GetString());
    }

    [Theory]
    [MemberData(nameof(BlankNames))]
    public async Task A_blank_or_missing_given_name_is_refused_and_adds_no_one(string? givenName)
    {
        await AssertRefusedAsync(
            new { givenName, familyName = "Citizen", dateOfBirth = "1987-03-07" },
            ("givenName", "Enter the given name"));
    }

    [Theory]
    [MemberData(nameof(BlankNames))]
    public async Task A_blank_or_missing_family_name_is_refused_and_adds_no_one(string? familyName)
    {
        await AssertRefusedAsync(
            new { givenName = "Jane", familyName, dateOfBirth = "1987-03-07" },
            ("familyName", "Enter the family name"));
    }

    [Fact]
    public async Task A_name_of_101_characters_is_refused_and_adds_no_one()
    {
        var tooLong = new string('a', Limits.ParticipantName + 1);

        await AssertRefusedAsync(
            new { givenName = tooLong, familyName = "Citizen", dateOfBirth = "1987-03-07" },
            ("givenName", "Given name must be 100 characters or less"));
        await AssertRefusedAsync(
            new { givenName = "Jane", familyName = tooLong, dateOfBirth = "1987-03-07" },
            ("familyName", "Family name must be 100 characters or less"));
    }

    // The limit counts the name once its ends are trimmed, as it is stored.
    [Fact]
    public async Task A_name_of_100_characters_is_stored_whatever_spaces_are_around_it()
    {
        var longest = new string('a', Limits.ParticipantName - 1) + "z";

        using var response = await PostAsync(
            Caller.ManagerOfA, new { givenName = $"  {longest}  ", familyName = longest, dateOfBirth = "1987-03-07" });

        var stored = await StoredAsync(seeded.A, await NewIdAsync(response));
        Assert.NotNull(stored);
        Assert.Equal((longest, longest), (stored.GivenName, stored.FamilyName));
    }

    [Theory]
    [InlineData("2026-10-10")] // today in Melbourne, though 9 October in UTC
    [InlineData("2026-10-11")]
    [InlineData("2030-01-01")]
    public async Task A_date_of_birth_of_today_or_later_in_Melbourne_is_refused_and_adds_no_one(string dateOfBirth)
    {
        await AssertRefusedAsync(
            new { givenName = "Jane", familyName = "Citizen", dateOfBirth },
            ("dateOfBirth", "Date of birth must be in the past"));
    }

    // Today in UTC, and yesterday in Melbourne.
    [Fact]
    public async Task A_date_of_birth_before_today_in_Melbourne_is_stored()
    {
        using var response = await PostAsync(
            Caller.ManagerOfA, new { givenName = "Jane", familyName = "Citizen", dateOfBirth = "2026-10-09" });

        var stored = await StoredAsync(seeded.A, await NewIdAsync(response));
        Assert.Equal(Today.AddDays(-1), stored?.DateOfBirth);
    }

    [Fact]
    public async Task A_missing_date_of_birth_is_refused_and_adds_no_one()
    {
        await AssertRefusedAsync(
            new { givenName = "Jane", familyName = "Citizen", dateOfBirth = (string?)null },
            ("dateOfBirth", "Enter the date of birth"));
    }

    // Each field's first failing check, all three at once, in the fields' order, as the screen lists them.
    [Fact]
    public async Task Every_refused_field_is_named_at_once()
    {
        await AssertRefusedAsync(
            new
            {
                givenName = " ",
                familyName = new string('a', Limits.ParticipantName + 1),
                dateOfBirth = "2026-10-10",
            },
            ("givenName", "Enter the given name"),
            ("familyName", "Family name must be 100 characters or less"),
            ("dateOfBirth", "Date of birth must be in the past"));
    }

    // design.md §5.3's fields alone, and no NDIS number or other government identifier (A5): the body's other fields,
    // its own ID, status and organisation among them, are passed over.
    [Fact]
    public async Task Nothing_beyond_the_three_details_is_taken_from_the_body()
    {
        const string ndisNumber = "430000001";
        var madeUpId = Guid.NewGuid();

        using var response = await PostAsync(Caller.ManagerOfA, new
        {
            id = madeUpId,
            organisationId = seeded.B.OrganisationId,
            givenName = "Jane",
            familyName = "Citizen",
            dateOfBirth = "1987-03-07",
            status = "Archived",
            ndisNumber,
        });

        var id = await NewIdAsync(response);
        Assert.NotEqual(madeUpId, id);
        var stored = await StoredAsync(seeded.A, id);
        Assert.NotNull(stored);
        Assert.Equal(ParticipantStatus.Active, stored.Status);
        Assert.Null(await StoredAsync(seeded.B, id));
        var columns = await StoredColumnsAsync(id);
        Assert.Equal(
            ["ArchivedAtUtc", "CreatedAtUtc", "DateOfBirth", "FamilyName", "GivenName", "Id", "OrganisationId",
                "RowVersion", "Status"],
            columns.Keys.Order(StringComparer.Ordinal));
        Assert.DoesNotContain(columns.Values, value => value?.ToString()?.Contains(ndisNumber) == true);
    }

    // The /api/admin group's policy refuses the worker before the handler, which would otherwise add them.
    [Fact]
    public async Task A_worker_gets_403_and_nothing_is_added()
    {
        var familyName = $"Worker-{Guid.NewGuid():N}";
        var participantsBefore = await ParticipantCountAsync(seeded.A);
        var lastEventId = await LastEventIdAsync();

        using var response = await PostAsync(
            Caller.WorkerOfA, new { givenName = "Jane", familyName, dateOfBirth = "1987-03-07" });

        await response.ReadProblemAsync(HttpStatusCode.Forbidden);
        Assert.Equal(participantsBefore, await ParticipantCountAsync(seeded.A));
        Assert.Equal(lastEventId, await LastEventIdAsync());
    }

    public ValueTask DisposeAsync() => app.DisposeAsync();

    private static Guid Id(JsonElement participant) => participant.GetProperty("id").GetGuid();

    private static async Task<Guid> NewIdAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        return Id(body);
    }

    /// <summary>
    /// Sends <paramref name="body"/> as A's manager, and asserts that it gets <c>422 validation.failed</c> with exactly
    /// <paramref name="errors"/>, each field by its name in the body's JSON with its one message, in that order, and
    /// that no participant was added to A and no event written.
    /// </summary>
    private async Task AssertRefusedAsync(object body, params (string Field, string Message)[] errors)
    {
        var participantsBefore = await ParticipantCountAsync(seeded.A);
        var lastEventId = await LastEventIdAsync();

        using var response = await PostAsync(Caller.ManagerOfA, body);

        using var problem = JsonDocument.Parse(await response.ReadProblemAsync(HttpStatusCode.UnprocessableEntity));
        Assert.Equal(
            ErrorCode.ValidationFailed.Code, problem.RootElement.GetProperty(ErrorCode.Extension).GetString());
        Assert.Equal(
            errors,
            problem.RootElement.GetProperty("errors").EnumerateObject()
                .Select(field => (field.Name, Assert.Single(field.Value.EnumerateArray()).GetString() ?? "")));
        Assert.Equal(participantsBefore, await ParticipantCountAsync(seeded.A));
        Assert.Equal(lastEventId, await LastEventIdAsync());
    }

    // As the SPA adds one: over HTTPS, with a token fetched as the caller, and the body as JSON.
    private async Task<HttpResponseMessage> PostAsync(Caller caller, object body)
    {
        using var client = app.CreateHttpsClient().SignInAs(caller, seeded);
        return await client.PostWithTokenAsync(Path, await client.FetchTokenAsync(), body);
    }

    // The caller's list, archived participants and all, in its order.
    private async Task<List<JsonElement>> ListAsync(Caller caller)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = app.CreateHttpsClient().SignInAs(caller, seeded);
        using var response = await client.GetAsync($"{Path}?includeArchived=true", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return [.. (await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).EnumerateArray()];
    }

    private async Task<List<Guid>> ListedIdsAsync(Caller caller) => [.. (await ListAsync(caller)).Select(Id)];

    // The participant as the row holds them in the organisation, through the app's context under its tenant, or null
    // when the organisation has none of that ID.
    private async Task<Participant?> StoredAsync(SeededOrganisation organisation, Guid participantId)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(organisation.OrganisationId))
        {
            return await db.Participants.AsNoTracking()
                .SingleOrDefaultAsync(p => p.Id == participantId, TestContext.Current.CancellationToken);
        }
    }

    // Every column of the participant's row, by name, read in SQL, so that a column the model does not know shows too.
    private async Task<Dictionary<string, object?>> StoredColumnsAsync(Guid participantId)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = app.Services.CreateAsyncScope();
        var connection = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Database.GetDbConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM [Participant] WHERE [Id] = @id";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@id";
        parameter.Value = participantId;
        command.Parameters.Add(parameter);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        Assert.True(await reader.ReadAsync(cancellationToken));
        return Enumerable.Range(0, reader.FieldCount).ToDictionary(
            reader.GetName, column => reader.IsDBNull(column) ? null : reader.GetValue(column));
    }

    // Read in SQL, with no tenant needed. The tests of the collection run one at a time, so none adds a participant or
    // writes an event while a test compares these before and after its request.
    private async Task<int> ParticipantCountAsync(SeededOrganisation organisation)
    {
        await using var scope = app.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Database
            .SqlQuery<int>(
                $"SELECT COUNT(*) AS [Value] FROM [Participant] WHERE [OrganisationId] = {organisation.OrganisationId}")
            .SingleAsync(TestContext.Current.CancellationToken);
    }

    private async Task<long?> LastEventIdAsync()
    {
        await using var scope = app.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Database
            .SqlQuery<long?>($"SELECT MAX([Id]) AS [Value] FROM [AuditEvent]")
            .SingleAsync(TestContext.Current.CancellationToken);
    }
}
