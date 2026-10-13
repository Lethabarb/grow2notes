using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Grow2Notes.Web.Platform.Audit;
using Grow2Notes.Web.Platform.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Grow2Notes.Tests.Platform.Configuration;

/// <summary>
/// The one save of a configuration change, end to end on SQL Server (design.md §6.6, §6.7, D84): test-only <c>GET</c>
/// and <c>PUT</c> endpoints in the <c>/api/admin</c> group read and change the caller's organisation's guide prompts
/// with the row's version token, <see cref="IfMatch.Check"/> and <see cref="ConfigurationChanges"/>, as S01.04.01's
/// <c>/api/admin/guide-prompts</c> will. Each test adds an organisation of its own with two managers, so no seeded row
/// changes and the organisation's audit events are the test's alone. <see cref="ConfigurationChangesTests"/> covers
/// the save without a database.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class ConfigurationChangesOnSqlServerTests : IClassFixture<Grow2NotesFactory>, IAsyncDisposable
{
    private const string Path = "/api/admin/test-only/guide-prompts";

    private const string StoredText = "What did they enjoy today?\nWas anything hard?";

    private const string FirstManagersText = "What did they enjoy today?\nWas anything hard?\nWho did they see?";

    private const string SecondManagersText = "What went well today?";

    private static readonly DateTime AddedAtUtc = new(2026, 10, 13, 0, 0, 0, DateTimeKind.Utc);

    private readonly WebApplicationFactory<Program> app;

    public ConfigurationChangesOnSqlServerTests(Grow2NotesFactory factory) =>
        app = factory.WithWebHostBuilder(builder => builder.ConfigureServices(
            services => services.AddSingleton<IStartupFilter>(new TestOnlyEndpoints())));

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
        Assert.Equal(new GuidePromptsVersion(FirstManagersText, ETagOf(response)), stored);
        Assert.NotEqual(loaded.ETag, stored.ETag);
        Assert.Equal(
            new StoredEvent(
                organisation.Id, organisation.FirstManagerId, AuditEventTypes.GuidePrompts.Updated,
                AuditEntityTypes.Organisation, organisation.Id, ParticipantId: null,
                Details(StoredText, FirstManagersText)),
            Assert.Single(await EventsAsync(organisation.Id)));
    }

    [Fact]
    public async Task The_second_manager_s_PUT_with_the_old_ETag_answers_412_and_changes_nothing_after_the_first_s()
    {
        var organisation = await AddOrganisationAsync();
        using var first = ClientFor(organisation.FirstManagerId);
        using var second = ClientFor(organisation.SecondManagerId);
        var firstLoaded = await GetAsync(first);
        var secondLoaded = await GetAsync(second);
        using (var firstSaved = await PutAsync(first, FirstManagersText, firstLoaded.ETag))
        {
            Assert.Equal(HttpStatusCode.OK, firstSaved.StatusCode);
        }

        var afterFirst = await StoredAsync(organisation.Id);

        using var response = await PutAsync(second, SecondManagersText, secondLoaded.ETag);

        await AssertProblemAsync(response, ErrorCode.PreconditionFailed);
        Assert.Equal(FirstManagersText, afterFirst.Text);
        Assert.Equal(afterFirst, await StoredAsync(organisation.Id));
        Assert.Equal(organisation.FirstManagerId, Assert.Single(await EventsAsync(organisation.Id)).ActorUserId);
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

    // The race that If-Match cannot catch: the row changes between a PUT's check and its save.
    [Fact]
    public async Task A_row_another_scope_changed_after_this_one_loaded_it_gives_PreconditionFailed_and_writes_nothing()
    {
        var organisation = await AddOrganisationAsync();
        await using var first = app.Services.CreateAsyncScope();
        await using var second = app.Services.CreateAsyncScope();
        var loadedByFirst = await LoadAsync(first.ServiceProvider, organisation.Id);
        var loadedBySecond = await LoadAsync(second.ServiceProvider, organisation.Id);
        Assert.Same(
            ConfigurationSaveResult.Saved,
            await ChangeInScopeAsync(
                second.ServiceProvider, organisation.SecondManagerId, loadedBySecond, SecondManagersText));

        var result = await ChangeInScopeAsync(
            first.ServiceProvider, organisation.FirstManagerId, loadedByFirst, FirstManagersText);

        Assert.Same(ConfigurationSaveResult.PreconditionFailed, result);
        Assert.Equal(SecondManagersText, (await StoredAsync(organisation.Id)).Text);
        Assert.Equal(
            new StoredEvent(
                organisation.Id, organisation.SecondManagerId, AuditEventTypes.GuidePrompts.Updated,
                AuditEntityTypes.Organisation, organisation.Id, ParticipantId: null,
                Details(StoredText, SecondManagersText)),
            Assert.Single(await EventsAsync(organisation.Id)));
    }

    public ValueTask DisposeAsync() => app.DisposeAsync();

    /// <summary>
    /// The change a <c>PUT</c> makes, as S01.04.01's handler will make it: the organisation's guide prompts set to
    /// <paramref name="text"/> on the row as the caller's scope loaded it, and saved by <paramref name="actor"/> with
    /// its <c>guide_prompts.updated</c> event, whose <c>Details</c> give the old and new text as <c>text</c>, the name
    /// that design.md §6.6 gives it.
    /// </summary>
    private static Task<ConfigurationSaveResult> ChangeTextAsync(
        ConfigurationChanges changes,
        Actor actor,
        Organisation organisation,
        string text,
        CancellationToken cancellationToken)
    {
        var before = new JsonObject { ["text"] = organisation.GuidePrompts };
        organisation.GuidePrompts = text;
        return changes.SaveAsync(
            actor, AuditEventTypes.GuidePrompts.Updated, AuditEntityTypes.Organisation, organisation.Id,
            participantId: null, before, new JsonObject { ["text"] = organisation.GuidePrompts },
            cancellationToken: cancellationToken);
    }

    // In the organisation's tenant, as a request signed in as one of its managers has it.
    private static async Task<ConfigurationSaveResult> ChangeInScopeAsync(
        IServiceProvider scope, Guid managerId, Organisation loaded, string text)
    {
        using (scope.GetRequiredService<ITenantContext>().Use(loaded.Id))
        {
            return await ChangeTextAsync(
                scope.GetRequiredService<ConfigurationChanges>(), new Actor(managerId, Channel.App), loaded, text,
                TestContext.Current.CancellationToken);
        }
    }

    // Tracked by the scope's context, as a handler loads the row it changes.
    private static Task<Organisation> LoadAsync(IServiceProvider scope, Guid organisationId) =>
        scope.GetRequiredService<Grow2NotesDbContext>().Organisations
            .SingleAsync(o => o.Id == organisationId, TestContext.Current.CancellationToken);

    private static async Task<GuidePromptsVersion> GetAsync(HttpClient client)
    {
        using var response = await client.GetAsync(Path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        var text = body.GetProperty("text").GetString();
        Assert.NotNull(text);
        return new(text, ETagOf(response));
    }

    // As the SPA saves: with a token fetched as the caller, the text as JSON, and, unless null, the ETag as If-Match.
    private static async Task<HttpResponseMessage> PutAsync(HttpClient client, string text, string? ifMatch)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, Path) { Content = JsonContent.Create(new { text }) };
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

    // As ChangeDetails writes a change of the text.
    private static string Details(string oldText, string newText) =>
        new JsonObject
        {
            ["old"] = new JsonObject { ["text"] = oldText },
            ["new"] = new JsonObject { ["text"] = newText },
        }.ToJsonString();

    private HttpClient ClientFor(Guid managerId) => app.CreateHttpsClient().SignInAs(managerId);

    // With guide prompts already stored, and two Active managers, as setup leaves them (design.md §8.1) apart from the
    // passkey or password, which the test-only sign-in does not need. An organisation is the tenant, not tenant-owned,
    // and AspNetUsers is not filtered, so this needs no tenant.
    private async Task<AddedOrganisation> AddOrganisationAsync()
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var organisation = new Organisation
        {
            Name = "Configuration changes test",
            GuidePrompts = StoredText,
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
    private async Task<GuidePromptsVersion> StoredAsync(Guid organisationId)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var organisation = await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Organisations
            .AsNoTracking()
            .SingleAsync(o => o.Id == organisationId, TestContext.Current.CancellationToken);
        return new(organisation.GuidePrompts, RowVersionToken.ETag(organisation));
    }

    // Read in SQL, so the query filter, which would need a tenant, does not decide what is seen.
    private async Task<List<StoredEvent>> EventsAsync(Guid organisationId)
    {
        await using var scope = app.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Database.SqlQuery<StoredEvent>($"""
            SELECT [OrganisationId], [ActorUserId], [EventType], [EntityType], [EntityId], [ParticipantId], [Details]
            FROM [AuditEvent]
            WHERE [OrganisationId] = {organisationId}
            """).ToListAsync(TestContext.Current.CancellationToken);
    }

    private sealed record AddedOrganisation(Guid Id, Guid FirstManagerId, Guid SecondManagerId);

    /// <summary>The guide prompts' text with the ETag of the row that holds it.</summary>
    private sealed record GuidePromptsVersion(string Text, string ETag);

    private sealed record StoredEvent(
        Guid OrganisationId,
        Guid? ActorUserId,
        string EventType,
        string? EntityType,
        Guid? EntityId,
        Guid? ParticipantId,
        string? Details);

    /// <summary>The body of both endpoints, <c>{text}</c> (design.md §6.6).</summary>
    private sealed record GuidePromptsBody(string Text);

    /// <summary>
    /// Maps a <c>GET</c> and a <c>PUT</c> of the caller's organisation's guide prompts at <see cref="Path"/>, in the
    /// app's <c>/api/admin</c> group, as S01.04.01 will map <c>/api/admin/guide-prompts</c> (design.md §6.6): the
    /// <c>GET</c> answers <c>{text}</c> with the row's <c>ETag</c>; the <c>PUT</c> of <c>{text}</c> checks its
    /// <c>If-Match</c> against the row as loaded, saves the change with its event through
    /// <see cref="ConfigurationChanges"/> and answers <c>200</c> with the row's <c>ETag</c> after the save, the same
    /// one when nothing changed (D84), or the problem of the §6.9 code it was given. They are routed ahead of the app's
    /// own routing, which then leaves the endpoint already chosen, so they run where the app's own endpoints do, after
    /// the app's authorization.
    /// </summary>
    /// <remarks>The text's limit of 1,000 characters (A6) is S01.04.01's, with its own tests.</remarks>
    private sealed class TestOnlyEndpoints : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.UseRouting();
            next(app);
            app.UseEndpoints(endpoints =>
            {
                var admin = endpoints.MapApiGroup().MapAdminGroup();
                admin.MapGet("/test-only/guide-prompts", ReadAsync);
                admin.MapPut("/test-only/guide-prompts", WriteAsync);
            });
        };

        private static async Task<Ok<GuidePromptsBody>> ReadAsync(
            HttpResponse response, Grow2NotesDbContext db, ITenantContext tenant, CancellationToken cancellationToken)
        {
            var organisation = await LoadTenantAsync(db, tenant, cancellationToken);
            response.Headers.ETag = RowVersionToken.ETag(organisation);
            return TypedResults.Ok(new GuidePromptsBody(organisation.GuidePrompts));
        }

        private static async Task<Results<Ok, ProblemHttpResult>> WriteAsync(
            GuidePromptsBody body,
            HttpRequest request,
            HttpResponse response,
            ClaimsPrincipal user,
            IOptions<IdentityOptions> identityOptions,
            Grow2NotesDbContext db,
            ITenantContext tenant,
            ConfigurationChanges changes,
            CancellationToken cancellationToken)
        {
            var organisation = await LoadTenantAsync(db, tenant, cancellationToken);
            if (IfMatch.Check(request.Headers.IfMatch, RowVersionToken.Of(organisation)) is { } refused)
            {
                return refused.Problem();
            }

            var saved = await ChangeTextAsync(
                changes, Actor.InApp(user, identityOptions.Value), organisation, body.Text, cancellationToken);
            if (saved.Error is { } error)
            {
                return error.Problem();
            }

            // EF Core reads back the version that SQL Server gave the row as it saved it, so this is the new ETag.
            response.Headers.ETag = RowVersionToken.ETag(organisation);
            return TypedResults.Ok();
        }

        // The organisation is the tenant, so the caller can load no other, and there is no 404 to give.
        private static Task<Organisation> LoadTenantAsync(
            Grow2NotesDbContext db, ITenantContext tenant, CancellationToken cancellationToken) =>
            db.Organisations.SingleAsync(o => o.Id == tenant.OrganisationId, cancellationToken);
    }
}
