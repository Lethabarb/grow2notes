using System.Net;
using System.Text.Json;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Features.Auth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;

namespace Grow2Notes.Tests.Features.Auth;

/// <summary>
/// <c>POST /api/auth/setup/passkey/failure</c> (design.md §6.2, D103), called as the SPA calls it when the browser's
/// passkey prompt fails at setup: signed out, over HTTPS, after <c>POST /api/auth/setup/start</c> with a link of the
/// test's own (<see cref="SetupLinks"/>), with the one antiforgery token that the SPA fetches before the start. Its log
/// entries are recorded. The endpoint matrix covers callers with no enrolment cookie.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class SetupPasskeyFailureEndpointTests : IClassFixture<Grow2NotesFactory>, IAsyncDisposable
{
    private const string Path = "/api/auth/setup/passkey/failure";

    // U+2028, which ends a line as a line break does, and U+202E, which makes the text after it read right to left.
    private const char LineSeparator = (char)0x2028;
    private const char RightToLeftOverride = (char)0x202E;

    private readonly RecordingLoggerProvider logs = new();
    private readonly Grow2NotesFactory factory;
    private readonly SeededOrganisations seeded;
    private readonly WebApplicationFactory<Program> app;

    public SetupPasskeyFailureEndpointTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    {
        this.factory = factory;
        seeded = sqlServer.Seeded;
        app = factory.WithWebHostBuilder(builder => builder.ConfigureLogging(logging => logging.AddProvider(logs)));
    }

    // Safari's words for a prompt that ended without a passkey, as SimpleWebAuthn passes them on. The line break, the
    // line separator and the bidi override are made spaces, so that the entry stays on one line of the console log and
    // reads as it is. The setup link still works afterwards.
    [Fact]
    public async Task A_failure_is_logged_on_one_line_with_the_browser_s_words_and_changes_nothing()
    {
        var link = await InviteAsync();
        var stamp = await factory.SecurityStampAsync(link.UserId);
        using var client = app.CreateHttpsClient();
        var (token, _) = await client.StartSetupAsync(link);

        using var response = await client.PostWithTokenAsync(Path, token, new
        {
            name = "NotAllowedError",
            code = "ERROR_PASSTHROUGH_SEE_CAUSE_PROPERTY",
            message = "The operation couldn’t be completed.\nError" + LineSeparator + "31." + RightToLeftOverride,
        });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var entry = Assert.Single(
            logs.Entries, logged => logged.Category == typeof(SetupPasskeyFailureEndpoint).FullName);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Equal(
            "Passkey setup failed in the browser: NotAllowedError, code ERROR_PASSTHROUGH_SEE_CAUSE_PROPERTY: "
            + "The operation couldn’t be completed. Error 31. ",
            entry.Message);
        Assert.False(entry.Holds(link.Email));
        Assert.Equal(stamp, await factory.SecurityStampAsync(link.UserId));
    }

    // A plain Error, such as SimpleWebAuthn's own when the browser has no WebAuthn, has no code.
    [Fact]
    public async Task A_failure_with_no_code_logs_none_for_it()
    {
        var link = await InviteAsync();
        using var client = app.CreateHttpsClient();
        var (token, _) = await client.StartSetupAsync(link);

        using var response = await client.PostWithTokenAsync(Path, token, new
        {
            name = "Error",
            message = "WebAuthn is not supported in this browser",
        });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var entry = Assert.Single(
            logs.Entries, logged => logged.Category == typeof(SetupPasskeyFailureEndpoint).FullName);
        Assert.Equal(
            "Passkey setup failed in the browser: Error, code none: WebAuthn is not supported in this browser",
            entry.Message);
    }

    // With no body, which the status code pages make problem details with no design.md §6.9 code.
    [Fact]
    public async Task Without_the_enrolment_cookie_a_failure_gets_401_and_logs_nothing()
    {
        using var client = app.CreateHttpsClient();

        using var response = await client.PostWithTokenAsync(
            Path, await client.FetchTokenAsync(), new { name = "NotAllowedError" });

        await response.ReadProblemAsync(HttpStatusCode.Unauthorized);
        Assert.DoesNotContain(logs.Entries, logged => logged.Category == typeof(SetupPasskeyFailureEndpoint).FullName);
    }

    // The SPA cuts each value to its length before it sends it, so a longer one is not the SPA's, and a body without
    // the error's name is not either.
    [Theory]
    [InlineData(101, 0, 0, "name")]
    [InlineData(5, 101, 0, "code")]
    [InlineData(5, 0, 501, "message")]
    [InlineData(0, 0, 0, "name")]
    public async Task A_value_over_its_length_or_no_name_gets_422_for_it_and_logs_nothing(
        int nameLength, int codeLength, int messageLength, string field)
    {
        var link = await InviteAsync();
        using var client = app.CreateHttpsClient();
        var (token, _) = await client.StartSetupAsync(link);

        using var response = await client.PostWithTokenAsync(Path, token, new
        {
            name = nameLength == 0 ? null : new string('n', nameLength),
            code = new string('c', codeLength),
            message = new string('m', messageLength),
        });

        using var problem = JsonDocument.Parse(await response.ReadProblemAsync(HttpStatusCode.UnprocessableEntity));
        Assert.Equal("validation.failed", problem.RootElement.GetProperty("code").GetString());
        Assert.Equal(
            [field], problem.RootElement.GetProperty("errors").EnumerateObject().Select(error => error.Name));
        Assert.DoesNotContain(logs.Entries, logged => logged.Category == typeof(SetupPasskeyFailureEndpoint).FullName);
    }

    public ValueTask DisposeAsync() => app.DisposeAsync();

    private Task<SetupLink> InviteAsync() => factory.InviteAsync(seeded.A);
}
