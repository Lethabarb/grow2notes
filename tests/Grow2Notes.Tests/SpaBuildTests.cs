using System.Text.RegularExpressions;
using Grow2Notes.Tests.Fixtures;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests;

/// <summary>
/// The production build stays within the strict Content Security Policy, which allows scripts, styles and images only
/// from the app's own files (design.md §7.2, §9.7).
/// </summary>
[Collection<SqlServerCollection>]
public sealed partial class SpaBuildTests(Grow2NotesFactory factory) : IClassFixture<Grow2NotesFactory>
{
    [Fact]
    public async Task The_page_has_no_inline_script_or_style()
    {
        using var client = factory.CreateClient();

        var page = await client.GetStringAsync("/", TestContext.Current.CancellationToken);

        var scripts = ScriptTag().Matches(page);
        Assert.NotEmpty(scripts);
        Assert.All(scripts, script => Assert.Matches(SrcAttribute(), script.Value));
        Assert.DoesNotMatch(StyleTag(), page);
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

    [GeneratedRegex(@"<script\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex ScriptTag();

    [GeneratedRegex(@"\ssrc\s*=", RegexOptions.IgnoreCase)]
    private static partial Regex SrcAttribute();

    [GeneratedRegex(@"<style\b", RegexOptions.IgnoreCase)]
    private static partial Regex StyleTag();

    // Anchored to the start of a line, so a commented-out setting does not count.
    [GeneratedRegex(@"^\s*assetsInlineLimit:\s*0\b", RegexOptions.Multiline)]
    private static partial Regex AssetsInlineLimitZero();
}
