using System.Text.RegularExpressions;

namespace Grow2Notes.Tests.Fixtures;

/// <summary>The SPA's page, <c>index.html</c>, as the app serves it from the Vite build in wwwroot.</summary>
internal static partial class SpaPage
{
    /// <summary>
    /// Fetches the page with <paramref name="client"/> and returns the path of the script bundle it names under
    /// <c>/assets</c>, a hashed asset. The bundle's file name changes with its content, so it is read from the page
    /// rather than written in a test.
    /// </summary>
    public static async Task<string> FetchHashedScriptPathAsync(this HttpClient client)
    {
        var page = await client.GetStringAsync("/", TestContext.Current.CancellationToken);
        var bundle = HashedScript().Match(page);
        Assert.True(bundle.Success, "index.html names no script under /assets.");
        return bundle.Groups["path"].Value;
    }

    [GeneratedRegex("""<script\b[^>]*\bsrc="(?<path>/assets/[^"]+)"[^>]*>""", RegexOptions.IgnoreCase)]
    private static partial Regex HashedScript();
}
