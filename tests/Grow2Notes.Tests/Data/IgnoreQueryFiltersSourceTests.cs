using System.Text.RegularExpressions;

namespace Grow2Notes.Tests.Data;

/// <summary>
/// <c>IgnoreQueryFilters</c> lifts the tenant filter, so only the operator commands may use it (design.md §5.9 item 2),
/// and they live in <c>src/Grow2Notes.Web/Platform/OperatorCommands/</c> (S00.03.05, S00.03.06). The test reads the
/// source itself, so it needs neither the app nor a database. Any mention counts, one in a comment included: that keeps
/// the scanner a plain search, with no C# parsing that a call could slip past.
/// </summary>
public sealed partial class IgnoreQueryFiltersSourceTests
{
    // Relative to src/. The folder need not exist: until the operator commands are written, nothing may use it.
    private const string OperatorCommands = "Grow2Notes.Web/Platform/OperatorCommands";

    private const string PlantedCall = """
        using Microsoft.EntityFrameworkCore;

        namespace Grow2Notes.Web;

        internal static class Planted
        {
            public static int CountEveryOrganisation(DbContext db) => db.Set<object>().IgnoreQueryFilters().Count();
        }
        """;

    [Fact]
    public void No_source_file_outside_the_operator_commands_uses_IgnoreQueryFilters()
    {
        Assert.Empty(FindIgnoreQueryFiltersOutside(Path.Combine(RepositoryRoot(), "src"), OperatorCommands));
    }

    [Theory]
    [InlineData("Grow2Notes.Web/Features/Notes/NoteQueries.cs")]
    [InlineData("Grow2Notes.Web/Platform/Planted.cs")]
    [InlineData("Grow2Notes.Web/Platform/OperatorCommandsHelpers/Planted.cs")] // only starts with the folder's name
    [InlineData("Planted.cs")]
    public void The_scanner_finds_a_call_outside_the_operator_commands(string path)
    {
        Assert.Equal([new Offence(path, 7)], ScanPlanted(path, PlantedCall));
    }

    [Theory]
    [InlineData($"{OperatorCommands}/ResetSignInCommand.cs")]
    [InlineData($"{OperatorCommands}/Bootstrap/BootstrapCommand.cs")]
    [InlineData("Grow2Notes.Web/bin/Release/net10.0/Planted.cs")]
    [InlineData("Grow2Notes.Web/obj/Debug/net10.0/Planted.cs")]
    [InlineData("Grow2Notes.Web/Features/Notes/NoteQueries.md")]
    public void The_scanner_passes_a_call_in_the_operator_commands_the_build_output_or_a_non_CSharp_file(string path)
    {
        Assert.Empty(ScanPlanted(path, PlantedCall));
    }

    [Fact]
    public void The_scanner_finds_a_mention_in_a_comment()
    {
        const string path = "Grow2Notes.Web/Data/Planted.cs";
        const string content = """
            namespace Grow2Notes.Web;

            /// <summary>Lifts the tenant filter with <c>IgnoreQueryFilters</c>.</summary>
            internal static class Planted;
            """;

        Assert.Equal([new Offence(path, 3)], ScanPlanted(path, content));
    }

    /// <summary>
    /// Each line of a <c>.cs</c> file under <paramref name="srcRoot"/> that names <c>IgnoreQueryFilters</c>, outside
    /// <paramref name="allowedFolder"/> (relative to <paramref name="srcRoot"/>, with <c>/</c> between folders) and
    /// each project's <c>bin</c> and <c>obj</c> build output. The allowed folder is matched case-sensitively, as Linux
    /// CI's file system would match it, so a run on Windows finds what CI does.
    /// </summary>
    private static List<Offence> FindIgnoreQueryFiltersOutside(string srcRoot, string allowedFolder) =>
    [
        .. from file in Directory.EnumerateFiles(srcRoot, "*.cs", SearchOption.AllDirectories)
           let path = Path.GetRelativePath(srcRoot, file).Replace(Path.DirectorySeparatorChar, '/')
           where path.Split('/') is not [_, "bin" or "obj", ..]
               && !path.StartsWith(allowedFolder + "/", StringComparison.Ordinal)
           from line in File.ReadLines(file).Select((text, index) => (Text: text, Number: index + 1))
           where IgnoreQueryFilters().IsMatch(line.Text)
           select new Offence(path, line.Number),
    ];

    // Each case gets a src/ folder of its own, so the cases stay independent and nothing is left behind.
    private static List<Offence> ScanPlanted(string path, string content)
    {
        var srcRoot = Directory.CreateTempSubdirectory("grow2notes-src-").FullName;
        try
        {
            var file = Path.Combine(srcRoot, path);
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllText(file, content);

            return FindIgnoreQueryFiltersOutside(srcRoot, OperatorCommands);
        }
        finally
        {
            Directory.Delete(srcRoot, recursive: true);
        }
    }

    // Found from the test's build output, which is inside the repository on a machine and in CI alike, so the test
    // needs no app host for its content root, as the tests that read the web project's files through the factory do.
    private static string RepositoryRoot()
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            if (File.Exists(Path.Combine(folder.FullName, "Grow2Notes.slnx")))
            {
                return folder.FullName;
            }
        }

        throw new InvalidOperationException($"No folder above {AppContext.BaseDirectory} holds Grow2Notes.slnx.");
    }

    [GeneratedRegex(@"\bIgnoreQueryFilters\b")]
    private static partial Regex IgnoreQueryFilters();

    private sealed record Offence(string File, int Line);
}
