using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;
using static Grow2Notes.Tests.Features.Mcp.McpAuditRows;
using static Grow2Notes.Tests.Features.Mcp.ToolCalls;

namespace Grow2Notes.Tests.Features.Mcp;

/// <summary>
/// <c>list_users</c> (mcp-server.md §5.2, S07.02.05) through the real <c>/mcp</c>: which accounts it lists and in what
/// order, what it says of each, what it searches, and the one audit row each call writes. The guards that every tool
/// shares are in <see cref="ToolMatrixTests"/>. The cases share the app of <see cref="ToolWorld"/>, so each adds users
/// whose names and addresses hold a hex tag of its own, and searches for it.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class UsersToolsTests(ToolWorld world) : IClassFixture<ToolWorld>
{
    private const string Tool = "list_users";

    // {tag} is the case's own tag.
    public static TheoryData<string> SearchesThatFindPia { get; } =
    [
        "Quill{tag}",
        "QUILL{tag}",
        "quill{tag} pia{tag}",
        "PIA{tag} QUILL{tag}",
        "q.{tag}@EXAMPLE.test",
        "pia.q.{tag}",
        "  Quill{tag}  ",
        "{tag}",
    ];

    public static TheoryData<string> SearchesThatFindNobody { get; } =
    [
        "Quill{tag} nobody",
        "pia{tag}x",
        "{tag}@example.org",
    ];

    public static TheoryData<int> RefusedCharacters { get; } = [0x0007, 0x000A, 0x202E];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private ToolScene Scene => world.Scene;

    [Fact]
    public async Task Invited_and_active_users_of_the_organisation_are_listed_by_name_then_email_and_no_others()
    {
        var tag = NewTag();
        var cyB = await AddAsync(tag, $"Cy{tag}", "cy.b");
        var cyA = await AddAsync(tag, $"cy{tag}", "cy.a");
        var ada = await AddAsync(tag, $"Ada{tag}", "ada");
        var bo = await AddAsync(tag, $"Bo{tag}", "bo", UserStatus.Invited);
        await AddAsync(tag, $"Di{tag}", "di", UserStatus.Deactivated);
        await AddAsync(tag, $"Eve{tag}", "eve", organisation: Scene.OtherOrganisation);

        var (result, _) = await world.CallAsync(Tool, new() { ["search"] = tag });

        Assert.NotEqual(true, result.IsError);
        Assert.Equal([ada, bo, cyA, cyB], EmailsOf(result));
    }

    [Fact]
    public async Task Each_user_has_exactly_the_five_keys_with_a_lower_case_role_and_status_and_the_stamp_as_version()
    {
        var tag = NewTag();
        var manager = await AddAsync(tag, $"Mia{tag}", "mia", role: UserRole.Manager);
        var support = await AddAsync(tag, $"Sol{tag}", "sol", role: UserRole.Support);
        var worker = await AddAsync(tag, $"Wes{tag}", "wes", UserStatus.Invited);

        var (result, _) = await world.CallAsync(Tool, new() { ["search"] = tag });

        var users = Content(result).GetProperty("users").EnumerateArray().ToList();
        Assert.Equal([manager, support, worker], users.Select(user => user.GetProperty("email").GetString()));
        Assert.All(
            users,
            user => Assert.Equal(
                ["displayName", "email", "expectedVersion", "role", "status"],
                user.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal)));
        Assert.Equal(
            [$"Mia{tag}", $"Sol{tag}", $"Wes{tag}"], users.Select(user => user.GetProperty("displayName").GetString()));
        Assert.Equal(["manager", "support", "worker"], users.Select(user => user.GetProperty("role").GetString()));
        Assert.Equal(["active", "active", "invited"], users.Select(user => user.GetProperty("status").GetString()));
        Assert.Equal(
            [await StampOfAsync(manager), await StampOfAsync(support), await StampOfAsync(worker)],
            users.Select(user => user.GetProperty("expectedVersion").GetString()));
    }

    [Theory]
    [MemberData(nameof(SearchesThatFindPia))]
    public async Task A_search_finds_a_user_by_part_of_the_name_or_email_with_every_word_in_any_case(string search)
    {
        var tag = NewTag();
        var pia = await AddAsync(tag, $"Pia{tag} Quill{tag}", "pia.q");

        var (result, _) = await world.CallAsync(Tool, new() { ["search"] = search.Replace("{tag}", tag) });

        Assert.Equal([pia], EmailsOf(result));
    }

    [Theory]
    [MemberData(nameof(SearchesThatFindNobody))]
    public async Task A_search_with_a_word_that_neither_the_name_nor_the_email_holds_finds_nobody(string search)
    {
        var tag = NewTag();
        await AddAsync(tag, $"Pia{tag} Quill{tag}", "pia.q");

        var (result, row) = await world.CallAsync(Tool, new() { ["search"] = search.Replace("{tag}", tag) });

        Assert.Empty(EmailsOf(result));
        AssertRead(row, AssistantFlow.ClientId, Tool, ("userIds", []));
    }

    [Fact]
    public async Task A_deactivated_user_is_listed_only_for_their_exact_email_in_any_case()
    {
        var tag = NewTag();
        var email = await AddAsync(tag, $"Dee{tag} Voss", "dee.voss", UserStatus.Deactivated);

        var (exact, _) = await world.CallAsync(Tool, new() { ["search"] = email });
        var (changedCase, _) = await world.CallAsync(Tool, new() { ["search"] = $"  {email.ToUpperInvariant()} " });
        var (partOfEmail, _) = await world.CallAsync(Tool, new() { ["search"] = $"dee.voss.{tag}" });
        var (name, _) = await world.CallAsync(Tool, new() { ["search"] = $"Dee{tag} Voss" });
        var (everyone, _) = await world.CallAsync(Tool, new());

        foreach (var found in new[] { exact, changedCase })
        {
            var user = Assert.Single(Content(found).GetProperty("users").EnumerateArray());
            Assert.Equal(email, user.GetProperty("email").GetString());
            Assert.Equal("deactivated", user.GetProperty("status").GetString());
        }

        Assert.Empty(EmailsOf(partOfEmail));
        Assert.Empty(EmailsOf(name));
        Assert.DoesNotContain(email, EmailsOf(everyone));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_missing_or_blank_search_lists_every_invited_and_active_user_of_the_organisation(
        string? search)
    {
        var tag = NewTag();
        var active = await AddAsync(tag, $"Ann{tag}", "ann");
        var other = await AddAsync(tag, $"Eve{tag}", "eve", organisation: Scene.OtherOrganisation);

        var (result, _) = await world.CallAsync(Tool, search is null ? new() : new() { ["search"] = search });

        var emails = EmailsOf(result);
        Assert.Contains(active, emails);
        Assert.Contains(Scene.InvitedUser.Email, emails);
        Assert.DoesNotContain(Scene.DeactivatedUser.Email, emails);
        Assert.DoesNotContain(other, emails);
        Assert.All(
            Content(result).GetProperty("users").EnumerateArray(),
            user => Assert.Contains(user.GetProperty("status").GetString(), new[] { "invited", "active" }));
    }

    [Fact]
    public async Task The_audit_row_holds_exactly_the_user_IDs_listed()
    {
        var tag = NewTag();
        var first = await AddAsync(tag, $"Ann{tag}", "ann");
        var second = await AddAsync(tag, $"Bea{tag}", "bea", UserStatus.Invited);
        await AddAsync(tag, $"Cat{tag}", "cat", UserStatus.Deactivated);

        var (result, row) = await world.CallAsync(Tool, new() { ["search"] = tag });

        Assert.Equal([first, second], EmailsOf(result));
        Assert.Null(row.ParticipantId);
        AssertRead(
            row, AssistantFlow.ClientId, Tool, ("userIds", await UserIdsByEmailAsync(world.Flow, [first, second])));
    }

    [Fact]
    public async Task What_a_user_has_signed_in_with_and_when_is_never_returned()
    {
        var (result, _) = await world.CallAsync(Tool, new() { ["search"] = Scene.Token });

        Assert.Equal(
            ["displayName", "email", "expectedVersion", "role", "status", "users"],
            PropertyNames(Content(result)).Distinct().Order(StringComparer.Ordinal));
    }

    [Theory]
    [MemberData(nameof(RefusedCharacters))]
    public async Task A_search_with_a_control_or_bidi_character_is_refused_and_not_echoed(int codePoint)
    {
        var marker = NewTag();

        var (result, row) = await world.CallAsync(
            Tool, new() { ["search"] = $"{marker}{char.ConvertFromUtf32(codePoint)}x" });

        AssertValidationFailed(result, "search", "Search has a control or formatting character");
        Assert.DoesNotContain(marker, Body(result));
        AssertRefused(row, Tool, "validation.failed");
    }

    [Fact]
    public async Task A_search_of_257_characters_is_refused_and_one_of_256_is_not()
    {
        var (refused, row) = await world.CallAsync(Tool, new() { ["search"] = new string('a', 257) });
        var (allowed, _) = await world.CallAsync(Tool, new() { ["search"] = new string('a', 256) });

        AssertValidationFailed(refused, "search", "Search must be 256 characters or less");
        Assert.DoesNotContain(new string('a', 20), Body(refused));
        AssertRefused(row, Tool, "validation.failed");
        Assert.NotEqual(true, allowed.IsError);
    }

    private static string NewTag() => Guid.NewGuid().ToString("N")[..12];

    private static List<string?> EmailsOf(CallToolResult result) =>
    [
        .. Content(result).GetProperty("users").EnumerateArray().Select(user => user.GetProperty("email").GetString()),
    ];

    // A user whose address is made up from local and the case's tag, so that no other case's search finds them.
    private async Task<string> AddAsync(
        string tag,
        string displayName,
        string local,
        UserStatus status = UserStatus.Active,
        UserRole role = UserRole.Worker,
        SeededOrganisation? organisation = null)
    {
        var email = $"{local}.{tag}@example.test";
        await world.Flow.App.Services.AddUserAsync(
            organisation ?? Scene.Organisation, role, displayName, status, email);
        return email;
    }

    private async Task<string> StampOfAsync(string email)
    {
        await using var scope = world.Flow.App.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Users.AsNoTracking()
            .Where(user => user.Email == email)
            .Select(user => user.ConcurrencyStamp!)
            .SingleAsync(Ct);
    }
}
