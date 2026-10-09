using System.Diagnostics;
using System.Text;

namespace Grow2Notes.Tests.Fixtures;

/// <summary>
/// D42's check: the parent company's name appears in no file that the repository tracks, in no tracked file's path
/// and in no file of the SPA build, so not in the source, the documents, <c>index.html</c>, the stylesheets, the
/// bundle or a file name, nor in the setup email's template, the passkey domain or the authenticator issuer, which
/// their stories keep in tracked files (design.md §8.1, §8.4). The name comes from <c>FORBIDDEN_NAME</c>, which CI's
/// Test step fills from the repository's Actions secret, so it is never written in the repository; tests/README.md
/// says how to run the check on a machine. It reads the files as they are in the working tree, not the history or
/// commit messages, and needs no database. The other tests show <see cref="ForbiddenName"/> finding a made-up name in
/// each form it takes, and not finding its words apart.
/// </summary>
public sealed class ForbiddenNameTests
{
    // Made up for these tests.
    private const string MadeUpName = "Quokka Lantern";

    // Where the SPA build goes (vite.config.ts), from the repository's root; .gitignore keeps it out of the index.
    private const string Build = "src/Grow2Notes.Web/wwwroot";

    private const string EmailTemplate = """
        <!doctype html>
        <html lang="en-AU">
          <body>
            <p>You have been invited to Grow2Notes.</p>
            <p>Quokka Lantern</p>
          </body>
        </html>
        """;

    private const string PasskeyDomain = """
        using 'main.bicep'

        param environmentName = 'prod'
        param passkeyDomain = 'notes.quokka-lantern.example'
        """;

    private const string IssuerConstant = """
        namespace Grow2Notes.Web.Features.Auth;

        internal static class Authenticator
        {
            public const string Issuer = "QuokkaLantern";
        }
        """;

    private const string BuiltScript = """const a="Grow2Notes",b="quokka lantern";export{a,b};""";

    private readonly ForbiddenName madeUp = new(MadeUpName);

    [Fact]
    public void The_name_is_in_no_tracked_file_or_path_and_in_no_file_of_the_SPA_build()
    {
        var name = Environment.GetEnvironmentVariable(ForbiddenName.Variable);
        if (string.IsNullOrWhiteSpace(name))
        {
            // GitHub Actions sets CI, so there a missing secret fails the run rather than skipping the check unseen.
            var inCi = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI"));
            Assert.False(
                inCi,
                $"{ForbiddenName.Variable} is empty, so D42's check cannot run: CI's Test step fills it from the " +
                "repository's Actions secret of that name, which is missing or not given to this run.");
            Assert.Skip($"{ForbiddenName.Variable} is not set, so D42's check did not run (tests/README.md).");
        }

        var forbidden = new ForbiddenName(name);
        var root = Repository.Root();
        List<string> read = [];
        List<string> found = [];
        foreach (var path in TrackedFiles(root).Concat(BuiltFiles(root)))
        {
            var file = Path.Combine(root, path);
            // git lists a file deleted in the working tree until the deletion is staged.
            if (File.Exists(file))
            {
                read.Add(path);
                found.AddRange(forbidden.FindIn(path, File.ReadAllText(file)));
            }
        }

        if (found.Count > 0)
        {
            Assert.Fail($"The name in {ForbiddenName.Variable} appears in:\n{string.Join('\n', found)}");
        }

        // A tracked file, the built page and the bundle it names were read, so the search cannot pass on no files. The
        // failure prints only these paths: one that was read may hold the name.
        var script = Build + SpaPage.HashedScriptPath(File.ReadAllText(Path.Combine(root, Build, "index.html")));
        string[] expected = ["src/grow2notes-spa/src/copy/strings.ts", $"{Build}/index.html", script];
        Assert.Empty(expected.Except(read));
    }

    [Fact]
    public void It_is_found_in_a_sentence()
    {
        const string text = """
            # About

            Grow2Notes is made for the programs of Quokka Lantern.
            """;

        Assert.Equal(["docs/about.md: line 3"], madeUp.FindIn("docs/about.md", text));
    }

    [Fact]
    public void It_is_found_across_a_line_break_in_Markdown_on_the_line_it_starts_on()
    {
        const string text = """
            - Grow2Notes is made for the programs of Quokka
              Lantern, and for no one else.
            """;

        Assert.Equal(["docs/about.md: line 1"], madeUp.FindIn("docs/about.md", text));
    }

    [Theory]
    [InlineData("QUOKKA LANTERN")]
    [InlineData("quokka lantern")]
    [InlineData("qUOKKA lANTERN")]
    public void It_is_found_in_another_case(string text)
    {
        Assert.Equal(["notes.txt: line 1"], madeUp.FindIn("notes.txt", text));
    }

    [Fact]
    public void It_is_found_hyphenated_in_a_file_name_which_is_shown_without_it()
    {
        Assert.Equal(
            ["design/<FORBIDDEN_NAME>-logo.svg: its path"], madeUp.FindIn("design/quokka-lantern-logo.svg", "<svg/>"));
    }

    [Fact]
    public void It_is_found_run_together_in_a_domain()
    {
        Assert.Equal(
            ["docs/setup.md: line 1"], madeUp.FindIn("docs/setup.md", "Open https://notes.quokkalantern.example."));
    }

    [Theory]
    [InlineData("src/Grow2Notes.Web/Features/Auth/SetupEmail.html", EmailTemplate, 5)]
    [InlineData("infra/prod.bicepparam", PasskeyDomain, 4)]
    [InlineData("src/Grow2Notes.Web/Features/Auth/Authenticator.cs", IssuerConstant, 5)]
    [InlineData($"{Build}/assets/index-Oz0nqNiM.js", BuiltScript, 1)]
    public void It_is_found_in_a_planted_email_template_passkey_domain_issuer_and_built_script(
        string path, string text, int line)
    {
        Assert.Equal([$"{path}: line {line}"], madeUp.FindIn(path, text));
    }

    [Fact]
    public void Each_file_is_named_once_for_its_path_and_once_for_each_line_without_the_name()
    {
        const string text = """
            Quokka Lantern and QUOKKA-LANTERN

            quokka_lantern
            """;

        Assert.Equal(
            [
                "docs/<FORBIDDEN_NAME>.md: its path",
                "docs/<FORBIDDEN_NAME>.md: line 1",
                "docs/<FORBIDDEN_NAME>.md: line 3",
            ],
            madeUp.FindIn("docs/QuokkaLantern.md", text));
    }

    [Theory]
    [InlineData("docs/wildlife.md", "A quokka lives on Rottnest Island.")]
    [InlineData("docs/lighting.md", "Hang a lantern by the door.")]
    [InlineData("docs/quokka.md", "A quokka with a lantern.")]
    [InlineData("docs/lantern-quokka.md", "Lantern Quokka.")]
    [InlineData("docs/order.md", "lanternquokka")]
    public void It_is_not_found_when_only_one_word_appears_or_its_words_come_in_another_order(string path, string text)
    {
        Assert.Empty(madeUp.FindIn(path, text));
    }

    [Fact]
    public void A_name_with_no_letter_or_digit_is_refused_naming_the_variable()
    {
        var refused = Assert.Throws<ArgumentException>(() => new ForbiddenName(" - "));

        Assert.StartsWith(ForbiddenName.Variable, refused.Message, StringComparison.Ordinal);
    }

    // Every file in the index, by its path from the repository's root with / between folders.
    private static string[] TrackedFiles(string root)
    {
        using var git = Process.Start(new ProcessStartInfo("git", ["ls-files", "-z"])
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            StandardOutputEncoding = Encoding.UTF8,
        })!;
        var listing = git.StandardOutput.ReadToEnd();
        git.WaitForExit();
        Assert.Equal(0, git.ExitCode);

        // -z ends each path with a NUL and quotes none, whatever characters it holds.
        return listing.Split('\0', StringSplitOptions.RemoveEmptyEntries);
    }

    private static IEnumerable<string> BuiltFiles(string root) =>
        Directory.EnumerateFiles(Path.Combine(root, Build), "*", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(root, file).Replace(Path.DirectorySeparatorChar, '/'));
}
