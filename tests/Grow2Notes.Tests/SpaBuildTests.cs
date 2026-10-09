using System.Text.RegularExpressions;
using Grow2Notes.Tests.Fixtures;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests;

/// <summary>
/// The production build stays within the strict Content Security Policy, which allows scripts, styles and images only
/// from the app's own files (design.md §7.2, §9.7). <see cref="CspBlockedContent"/> finds what the policy would
/// block, and <see cref="CspBlockedContentTests"/> shows it finding each kind of thing on a short sample.
/// </summary>
[Collection<SqlServerCollection>]
public sealed partial class SpaBuildTests(Grow2NotesFactory factory) : IClassFixture<Grow2NotesFactory>
{
    [Fact]
    public async Task The_page_has_no_inline_script_or_style_and_no_data_uri()
    {
        using var client = factory.CreateClient();

        var page = await client.GetStringAsync("/", TestContext.Current.CancellationToken);

        Assert.Empty(CspBlockedContent.InPage(page));
    }

    [Fact]
    public async Task No_file_under_assets_holds_a_data_uri()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var webRoot = factory.Services.GetRequiredService<IWebHostEnvironment>().WebRootPath;
        using var client = factory.CreateClient();
        var bundle = await client.FetchHashedScriptPathAsync();

        List<string> assets = [];
        List<string> found = [];
        var folder = Path.Combine(webRoot, "assets");
        foreach (var file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
        {
            var asset = "/" + Path.GetRelativePath(webRoot, file).Replace(Path.DirectorySeparatorChar, '/');
            assets.Add(asset);
            var text = await File.ReadAllTextAsync(file, cancellationToken);
            found.AddRange(CspBlockedContent.DataUris(text).Select(uri => $"{asset}: {uri}"));
        }

        // The page's own script bundle is among the files read, so the check cannot pass on an empty or wrong folder.
        Assert.Contains(bundle, assets);
        Assert.Empty(found);
    }

    [Fact]
    public async Task Vite_is_configured_never_to_inline_assets()
    {
        var webProject = factory.Services.GetRequiredService<IWebHostEnvironment>().ContentRootPath;
        var viteConfig = await File.ReadAllTextAsync(
            Path.Combine(webProject, "..", "grow2notes-spa", "vite.config.ts"), TestContext.Current.CancellationToken);

        // Checked in the source because the build output cannot show it yet: the SPA imports no asset small enough
        // that Vite would otherwise inline it as a data: URI.
        Assert.Matches(AssetsInlineLimitZero(), viteConfig);
    }

    // Anchored to the start of a line, so a commented-out setting does not count.
    [GeneratedRegex(@"^\s*assetsInlineLimit:\s*0\b", RegexOptions.Multiline)]
    private static partial Regex AssetsInlineLimitZero();
}
