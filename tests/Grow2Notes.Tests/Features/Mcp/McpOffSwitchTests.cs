using System.Net;
using Grow2Notes.Tests.Fixtures;

namespace Grow2Notes.Tests.Features.Mcp;

/// <summary>
/// With <c>Mcp:Enabled</c> unset, which the default factory leaves it, every address of the AI-assistant access and of
/// its discovery documents answers a 404 problem, never the SPA page (mcp-server.md §3.5, §10 item 6). The endpoint
/// matrix covers the four callers, so this one is anonymous. The consent and <c>/mcp</c> cases join it with the tasks
/// that add those endpoints.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class McpOffSwitchTests(Grow2NotesFactory factory) : IClassFixture<Grow2NotesFactory>
{
    public static MatrixTheoryData<string, string> Addresses { get; } = new(
        ["GET", "POST"],
        [
            "/mcp",
            "/oauth/authorize",
            "/oauth/token",
            "/oauth/revoke",
            "/.well-known/oauth-authorization-server",
            "/.well-known/openid-configuration",
            "/.well-known/oauth-protected-resource",
            "/.well-known/oauth-protected-resource/mcp",
        ]);

    [Theory]
    [MemberData(nameof(Addresses))]
    public async Task Every_address_of_the_access_is_a_404_problem_while_it_is_off(string method, string path)
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), path);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        await response.ReadProblemAsync(HttpStatusCode.NotFound);
    }
}
