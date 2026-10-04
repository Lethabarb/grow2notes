using System.Net;
using Grow2Notes.Tests.Fixtures;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests;

[Collection<SqlServerCollection>]
public sealed class SpaAndApiRoutingTests(Grow2NotesFactory factory) : IClassFixture<Grow2NotesFactory>
{
    public static MatrixTheoryData<string, string> UnknownApiRequests { get; } =
        new(["GET", "POST", "PUT", "DELETE"], ["/api", "/api/no-such-endpoint"]);

    [Theory]
    [MemberData(nameof(UnknownApiRequests))]
    public async Task Unknown_api_paths_return_404_rather_than_the_SPA_page(string method, string path)
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), path);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.NotEqual("text/html", response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/manage/anything")]
    public async Task Client_routes_return_index_html(string path)
    {
        var webRoot = factory.Services.GetRequiredService<IWebHostEnvironment>().WebRootPath;
        var indexHtml = await File.ReadAllTextAsync(
            Path.Combine(webRoot, "index.html"), TestContext.Current.CancellationToken);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(indexHtml, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }
}
