using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Features.GuidePrompts;
using Grow2Notes.Web.Platform;
using Grow2Notes.Web.Platform.Audit;
using Grow2Notes.Web.Platform.Configuration;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests.Platform.Configuration;

/// <summary>
/// The one save of a configuration change, end to end on SQL Server (design.md §6.6, §6.7, D84), through
/// <c>GET</c> and <c>PUT /api/admin/guide-prompts</c>, which read and change the caller's organisation's guide prompts
/// with the row's version token, <see cref="IfMatch.Check"/> and <see cref="ConfigurationChanges"/>
/// (<see cref="GuidePromptsHandler"/>); and those endpoints' own rules (S01.04.01): the limit of 1,000 characters
/// (A6), an empty text, a body without one, and each organisation's prompts out of every other's reach (§5.9 item 7).
/// Each test adds organisations of its own, each with two managers, so no seeded row changes and an organisation's
/// audit events are the test's alone. <see cref="ConfigurationChangesTests"/> covers the save without a database, and
/// the endpoint matrix who may call the endpoints.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class ConfigurationChangesOnSqlServerTests(Grow2NotesFactory factory) : IClassFixture<Grow2NotesFactory>
{
    private const string Path = "/api/admin/guide-prompts";

    private const string StoredText = "What did they enjoy today?\nWas anything hard?";

    private const string FirstManagersText = "What did they enjoy today?\nWas anything hard?\nWho did they see?";

    private const string SecondManagersText = "What went well today?";

    private static readonly DateTime AddedAtUtc = new(2026, 10, 13, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Both_managers_GETs_give_the_stored_text_with_the_same_ETag_the_row_s_version()
    {
        var organisation = await AddOrganisationAsync();
        using var first = ClientFor(organisation.FirstManagerId);
        using var second = ClientFor(organisation.SecondManagerId);

        var firstLoaded = await GetAsync(first);
        var secondLoaded = await GetAsync(second);

        var stored = await StoredAsync(organisation.Id);
        Assert.Equal(StoredText, stored.Text);
        Assert.Equal(stored, firstLoaded);
        Assert.Equal(stored, secondLoaded);
    }

    [Fact]
    public async Task A_PUT_with_the_ETag_answers_200_with_a_new_ETag_and_writes_one_event_with_the_old_and_new_text()
    {
        var organisation = await AddOrganisationAsync();
        using var client = ClientFor(organisation.FirstManagerId);
        var loaded = await GetAsync(client);

        using var response = await PutAsync(client, FirstManagersText, loaded.ETag);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var stored = await StoredAsync(organisation.Id);
        Assert.Equal(new PromptsVersion(FirstManagersText, ETagOf(response)), stored);
        Assert.NotEqual(loaded.ETag, stored.ETag);
        Assert.Equal(
            [Updated(organisation, organisation.FirstManagerId, StoredText, FirstManagersText)],
            await EventsAsync(organisation.Id));
    }

    // A line break counts as one character, as the screen counts it, sent as LF (guide-prompts.md Limit and count).
    [Fact]
    public async Task A_PUT_of_1_000_characters_holding_10_line_breaks_is_stored_and_read_back_exactly()
    {
        var text = string.Join('\n', Enumerable.Range(1, 11).Select(line => $"Prompt {line:D2} ".PadRight(90, '.')));
        Assert.Equal(Limits.GuidePrompts, text.Length);
        Assert.Equal(10, text.Count(character => character == '\n'));
        var organisation = await AddOrganisationAsync();
        using var client = ClientFor(organisation.FirstManagerId);
        var loaded = await GetAsync(client);

        using var response = await PutAsync(client, text, loaded.ETag);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new PromptsVersion(text, ETagOf(response)), await GetAsync(client));
        Assert.Equal(
            [Updated(organisation, organisation.FirstManagerId, StoredText, text)],
            await EventsAsync(organisation.Id));
    }

    [Fact]
    public async Task A_PUT_of_1_001_characters_answers_422_on_text_and_changes_nothing()
    {
        var organisation = await AddOrganisationAsync();
        using var client = ClientFor(organisation.FirstManagerId);
        var loaded = await GetAsync(client);

        using var response = await PutAsync(client, new string('a', Limits.GuidePrompts + 1), loaded.ETag);

        await AssertRefusedOnTextAsync(response);
        Assert.Equal(loaded, await StoredAsync(organisation.Id));
        Assert.Empty(await EventsAsync(organisation.Id));
    }

    // Empty prompts leave the Guided notes box with no placeholder (design.md §4.10 States), and the screen sends a
    // text of whitespace alone as "" (guide-prompts.md Save).
    [Fact]
    public async Task A_PUT_of_empty_text_answers_200_stores_it_and_writes_one_event_with_the_old_and_new_text()
    {
        var organisation = await AddOrganisationAsync();
        using var client = ClientFor(organisation.FirstManagerId);
        var loaded = await GetAsync(client);

        using var response = await PutAsync(client, "", loaded.ETag);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new PromptsVersion("", ETagOf(response)), await StoredAsync(organisation.Id));
        Assert.Equal(
            [Updated(organisation, organisation.FirstManagerId, StoredText, "")],
            await EventsAsync(organisation.Id));
    }

    // With the row's own ETag, so that only the body can be refused.
    [Theory]
    [InlineData("{}")]
    [InlineData("""{"text":null}""")]
    public async Task A_PUT_with_no_text_or_a_null_one_answers_422_on_text_and_changes_nothing(string body)
    {
        var organisation = await AddOrganisationAsync();
        using var client = ClientFor(organisation.FirstManagerId);
        var loaded = await GetAsync(client);

        using var response = await PutAsync(
            client, new StringContent(body, Encoding.UTF8, "application/json"), loaded.ETag);

        await AssertRefusedOnTextAsync(response);
        Assert.Equal(loaded, await StoredAsync(organisation.Id));
        Assert.Empty(await EventsAsync(organisation.Id));
    }

    // The screen answers a 412 by loading the text and ETag afresh (guide-prompts.md Save), as A's second GET does.
    [Fact]
    public async Task A_loads_B_saves_A_s_PUT_answers_412_and_A_s_next_PUT_with_the_fresh_ETag_saves()
    {
        var organisation = await AddOrganisationAsync();
        using var a = ClientFor(organisation.FirstManagerId);
        using var b = ClientFor(organisation.SecondManagerId);
        var loadedByA = await GetAsync(a);
        var loadedByB = await GetAsync(b);
        using (var savedByB = await PutAsync(b, SecondManagersText, loadedByB.ETag))
        {
            Assert.Equal(HttpStatusCode.OK, savedByB.StatusCode);
        }

        var afterB = await StoredAsync(organisation.Id);

        using (var refused = await PutAsync(a, FirstManagersText, loadedByA.ETag))
        {
            await AssertProblemAsync(refused, ErrorCode.PreconditionFailed);
            Assert.Null(refused.Headers.ETag);
        }

        Assert.Equal(afterB, await StoredAsync(organisation.Id));
        var reloadedByA = await GetAsync(a);
        Assert.Equal(afterB, reloadedByA);

        using var response = await PutAsync(a, FirstManagersText, reloadedByA.ETag);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new PromptsVersion(FirstManagersText, ETagOf(response)), await StoredAsync(organisation.Id));
        Assert.Equal(
            [
                Updated(organisation, organisation.SecondManagerId, StoredText, SecondManagersText),
                Updated(organisation, organisation.FirstManagerId, SecondManagersText, FirstManagersText),
            ],
            await EventsAsync(organisation.Id));
    }

    [Fact]
    public async Task A_PUT_with_no_If_Match_answers_428_and_changes_nothing()
    {
        var organisation = await AddOrganisationAsync();
        using var client = ClientFor(organisation.FirstManagerId);
        var loaded = await GetAsync(client);

        using var response = await PutAsync(client, FirstManagersText, ifMatch: null);

        await AssertProblemAsync(response, ErrorCode.PreconditionRequired);
        Assert.Equal(loaded, await StoredAsync(organisation.Id));
        Assert.Empty(await EventsAsync(organisation.Id));
    }

    [Fact]
    public async Task A_PUT_of_the_stored_text_answers_200_with_the_same_ETag_and_writes_no_event()
    {
        var organisation = await AddOrganisationAsync();
        using var client = ClientFor(organisation.FirstManagerId);
        var loaded = await GetAsync(client);

        using var response = await PutAsync(client, StoredText, loaded.ETag);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(loaded.ETag, ETagOf(response));
        Assert.Equal(loaded, await StoredAsync(organisation.Id));
        Assert.Empty(await EventsAsync(organisation.Id));
    }

    // The tenant is the caller's own organisation, so a manager's GET reads, and their PUT changes, that one's row
    // alone (design.md §5.9 item 7).
    [Fact]
    public async Task Each_organisation_s_manager_reads_and_changes_their_own_organisation_s_prompts_only()
    {
        var first = await AddOrganisationAsync();
        var second = await AddOrganisationAsync(SecondManagersText);
        using var managerOfFirst = ClientFor(first.FirstManagerId);
        using var managerOfSecond = ClientFor(second.FirstManagerId);

        var loadedByFirst = await GetAsync(managerOfFirst);
        var loadedBySecond = await GetAsync(managerOfSecond);

        Assert.Equal(new PromptsVersion(StoredText, (await StoredAsync(first.Id)).ETag), loadedByFirst);
        Assert.Equal(new PromptsVersion(SecondManagersText, (await StoredAsync(second.Id)).ETag), loadedBySecond);

        using var response = await PutAsync(managerOfFirst, FirstManagersText, loadedByFirst.ETag);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(FirstManagersText, (await StoredAsync(first.Id)).Text);
        Assert.Equal(loadedBySecond, await StoredAsync(second.Id));
        Assert.Single(await EventsAsync(first.Id));
        Assert.Empty(await EventsAsync(second.Id));
    }

    // The race that If-Match cannot catch: the row changes between a PUT's check and its save. The first scope's
    // context already holds the row as it was before the second scope's save, so the handler's load there gives that
    // row, which EF Core does not overwrite with the stored one, and its check passes as if made before that save.
    [Fact]
    public async Task A_row_another_scope_changed_after_this_one_loaded_it_gives_PreconditionFailed_and_writes_nothing()
    {
        var organisation = await AddOrganisationAsync();
        var loaded = await StoredAsync(organisation.Id);
        await using var first = factory.Services.CreateAsyncScope();
        await using var second = factory.Services.CreateAsyncScope();
        await first.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Organisations
            .SingleAsync(o => o.Id == organisation.Id, TestContext.Current.CancellationToken);
        Assert.True((await SaveInScopeAsync(
            second.ServiceProvider, organisation.Id, organisation.SecondManagerId, loaded.ETag, SecondManagersText))
            .Succeeded);

        var result = await SaveInScopeAsync(
            first.ServiceProvider, organisation.Id, organisation.FirstManagerId, loaded.ETag, FirstManagersText);

        Assert.Same(ErrorCode.PreconditionFailed, result.Error);
        Assert.Equal(SecondManagersText, (await StoredAsync(organisation.Id)).Text);
        Assert.Equal(
            [Updated(organisation, organisation.SecondManagerId, StoredText, SecondManagersText)],
            await EventsAsync(organisation.Id));
    }

    // Through the scope's handler, in the organisation's tenant, as a request signed in as one of its managers has it.
    private static async Task<GuidePromptsSaveResult> SaveInScopeAsync(
        IServiceProvider scope, Guid organisationId, Guid managerId, string ifMatch, string text)
    {
        using (scope.GetRequiredService<ITenantContext>().Use(organisationId))
        {
            return await scope.GetRequiredService<GuidePromptsHandler>().SaveAsync(
                new Actor(managerId, Channel.App), ifMatch, text, TestContext.Current.CancellationToken);
        }
    }

    private static async Task<PromptsVersion> GetAsync(HttpClient client)
    {
        using var response = await client.GetAsync(Path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        var text = body.GetProperty("text").GetString();
        Assert.NotNull(text);
        return new(text, ETagOf(response));
    }

    private static Task<HttpResponseMessage> PutAsync(HttpClient client, string text, string? ifMatch) =>
        PutAsync(client, JsonContent.Create(new { text }), ifMatch);

    // As the SPA saves: with a token fetched as the caller, the body as JSON, and, unless null, the ETag as If-Match.
    private static async Task<HttpResponseMessage> PutAsync(HttpClient client, HttpContent body, string? ifMatch)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, Path) { Content = body };
        request.Headers.Add(AntiforgeryTokens.Header, await client.FetchTokenAsync());
        if (ifMatch is not null)
        {
            request.Headers.IfMatch.Add(EntityTagHeaderValue.Parse(ifMatch));
        }

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    // The header as sent, W/ and all, so a weak ETag never equals the row's strong one.
    private static string ETagOf(HttpResponseMessage response)
    {
        var etag = response.Headers.ETag;
        Assert.NotNull(etag);
        return etag.ToString();
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, ErrorCode code)
    {
        using var problem = JsonDocument.Parse(await response.ReadProblemAsync((HttpStatusCode)code.Status));
        Assert.Equal(code.Code, problem.RootElement.GetProperty(ErrorCode.Extension).GetString());
    }

    // The validation filter's answer, before the endpoint runs, naming the text alone.
    private static async Task AssertRefusedOnTextAsync(HttpResponseMessage response)
    {
        var failed = ErrorCode.ValidationFailed;
        using var problem = JsonDocument.Parse(await response.ReadProblemAsync((HttpStatusCode)failed.Status));
        Assert.Equal(failed.Code, problem.RootElement.GetProperty(ErrorCode.Extension).GetString());
        Assert.Equal("text", Assert.Single(problem.RootElement.GetProperty("errors").EnumerateObject()).Name);
    }

    // The event of a change of the text by the manager, with its Details as ChangeDetails writes them.
    private static StoredEvent Updated(
        AddedOrganisation organisation, Guid managerId, string oldText, string newText) =>
        new(
            organisation.Id, managerId, AuditEventTypes.GuidePrompts.Updated, AuditEntityTypes.Organisation,
            organisation.Id, ParticipantId: null,
            new JsonObject
            {
                ["old"] = new JsonObject { ["text"] = oldText },
                ["new"] = new JsonObject { ["text"] = newText },
            }.ToJsonString());

    private HttpClient ClientFor(Guid managerId) => factory.CreateHttpsClient().SignInAs(managerId);

    // With guide prompts already stored, and two Active managers, as setup leaves them (design.md §8.1) apart from the
    // passkey or password, which the test-only sign-in does not need. An organisation is the tenant, not tenant-owned,
    // and AspNetUsers is not filtered, so this needs no tenant.
    private async Task<AddedOrganisation> AddOrganisationAsync(string guidePrompts = StoredText)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var organisation = new Organisation
        {
            Name = "Configuration changes test",
            GuidePrompts = guidePrompts,
            CreatedAtUtc = AddedAtUtc,
        };
        db.Organisations.Add(organisation);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return new(
            organisation.Id,
            await AddManagerAsync(users, organisation.Id, "Avery Brooks"),
            await AddManagerAsync(users, organisation.Id, "Quinn Harper"));
    }

    private static async Task<Guid> AddManagerAsync(
        UserManager<ApplicationUser> users, Guid organisationId, string displayName)
    {
        // An address of its own, which no other test or run of this one uses.
        var email = $"{Guid.NewGuid():N}@example.org";
        var manager = new ApplicationUser
        {
            OrganisationId = organisationId,
            DisplayName = displayName,
            Role = UserRole.Manager,
            Status = UserStatus.Active,
            InvitedAtUtc = AddedAtUtc,
            ActivatedAtUtc = AddedAtUtc,
            UserName = email,
            Email = email,
            EmailConfirmed = true,
        };
        var result = await users.CreateAsync(manager);
        Assert.True(result.Succeeded, string.Join(" ", result.Errors.Select(error => error.Code)));
        return manager.Id;
    }

    // Read from a scope of its own, so no context's tracked row decides what is seen.
    private async Task<PromptsVersion> StoredAsync(Guid organisationId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var organisation = await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Organisations
            .AsNoTracking()
            .SingleAsync(o => o.Id == organisationId, TestContext.Current.CancellationToken);
        return new(organisation.GuidePrompts, RowVersionToken.ETag(organisation));
    }

    // Read in SQL, so the query filter, which would need a tenant, does not decide what is seen; in the order they
    // were written, which the identity key keeps.
    private async Task<List<StoredEvent>> EventsAsync(Guid organisationId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Database.SqlQuery<StoredEvent>($"""
            SELECT [OrganisationId], [ActorUserId], [EventType], [EntityType], [EntityId], [ParticipantId], [Details]
            FROM [AuditEvent]
            WHERE [OrganisationId] = {organisationId}
            ORDER BY [Id]
            """).ToListAsync(TestContext.Current.CancellationToken);
    }

    private sealed record AddedOrganisation(Guid Id, Guid FirstManagerId, Guid SecondManagerId);

    /// <summary>The guide prompts' text with the ETag of the row that holds it.</summary>
    private sealed record PromptsVersion(string Text, string ETag);

    private sealed record StoredEvent(
        Guid OrganisationId,
        Guid? ActorUserId,
        string EventType,
        string? EntityType,
        Guid? EntityId,
        Guid? ParticipantId,
        string? Details);
}
