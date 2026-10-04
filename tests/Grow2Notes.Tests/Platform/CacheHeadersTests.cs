using System.Net;
using System.Text.RegularExpressions;
using Grow2Notes.Tests.Fixtures;

namespace Grow2Notes.Tests.Platform;

[Collection<SqlServerCollection>]
public sealed partial class CacheHeadersTests(Grow2NotesFactory factory) : IClassFixture<Grow2NotesFactory>
{
    public static MatrixTheoryData<string, string> UnknownApiRequests { get; } =
        new(["GET", "POST", "PUT", "DELETE"], ["/api", "/api/no-such-endpoint"]);

    [Fact]
    public async Task Hashed_assets_are_cached_for_a_year_as_immutable()
    {
        using var client = factory.CreateClient();
        var page = await client.GetStringAsync("/", TestContext.Current.CancellationToken);
        // The bundle's file name changes with its content, so it is read from the page rather than written here.
        var bundle = HashedScript().Match(page);
        Assert.True(bundle.Success, "index.html names no script under /assets.");

        using var response = await client.GetAsync(bundle.Groups["path"].Value, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("public, max-age=31536000, immutable", CacheControl(response));
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/index.html")]
    [InlineData("/manage/anything")]
    public async Task Index_html_is_revalidated_on_every_load(string path)
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-cache", CacheControl(response));
    }

    [Theory]
    [MemberData(nameof(UnknownApiRequests))]
    public async Task Api_responses_are_never_stored(string method, string path)
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), path);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal("no-store", CacheControl(response));
    }

    // The header exactly as the app sent it. The typed CacheControl property would reformat it from parsed directives.
    private static string? CacheControl(HttpResponseMessage response) =>
        response.Headers.NonValidated.TryGetValues("Cache-Control", out var values) ? values.ToString() : null;

    [GeneratedRegex("""<script\b[^>]*\bsrc="(?<path>/assets/[^"]+)"[^>]*>""", RegexOptions.IgnoreCase)]
    private static partial Regex HashedScript();
}
