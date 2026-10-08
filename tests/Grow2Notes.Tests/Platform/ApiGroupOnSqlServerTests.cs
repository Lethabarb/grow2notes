using System.Net;
using Grow2Notes.Tests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Grow2Notes.Tests.Platform;

/// <summary>
/// The <c>/api</c> group's antiforgery check (design.md §9.8 item 2) for signed-in users, at a test-only endpoint in
/// the group (<see cref="TestOnlyApiEndpoint"/>), over HTTPS: a token is good only for the user it was issued to, so
/// the SPA fetches a fresh one after sign-in. One client stands for one browser, whose cookie stays the same as its
/// user signs in, or as another user signs in on it. <see cref="ApiGroupTests"/> covers the check signed out.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class ApiGroupOnSqlServerTests : IClassFixture<Grow2NotesFactory>, IAsyncDisposable
{
    private readonly SeededOrganisations seeded;
    private readonly WebApplicationFactory<Program> app;

    public ApiGroupOnSqlServerTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    {
        seeded = sqlServer.Seeded;
        app = factory.WithWebHostBuilder(TestOnlyApiEndpoint.MapIn);
    }

    [Fact]
    public async Task A_token_issued_to_the_signed_in_user_gets_through()
    {
        using var client = app.CreateHttpsClient().SignInAs(seeded.A.WorkerId);
        var token = await client.FetchTokenAsync();
        using var request = Post(token);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task A_token_issued_before_sign_in_is_refused()
    {
        using var client = app.CreateHttpsClient();
        var token = await client.FetchTokenAsync();
        client.SignInAs(seeded.A.WorkerId);
        using var request = Post(token);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        await response.ReadProblemAsync(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_token_issued_to_another_user_is_refused()
    {
        using var client = app.CreateHttpsClient().SignInAs(seeded.A.WorkerId);
        var workersToken = await client.FetchTokenAsync();
        client.SignInAs(seeded.A.ManagerId);
        using var request = Post(workersToken);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        await response.ReadProblemAsync(HttpStatusCode.BadRequest);
    }

    public ValueTask DisposeAsync() => app.DisposeAsync();

    private static HttpRequestMessage Post(string token) =>
        new(HttpMethod.Post, TestOnlyApiEndpoint.Path) { Headers = { { AntiforgeryTokens.Header, token } } };
}
