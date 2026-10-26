using System.Text.RegularExpressions;
using Grow2Notes.Tests.Fixtures;

namespace Grow2Notes.Tests.Platform;

/// <summary>
/// The build check for the rule that the pipeline's template holds no lock or role (design.md §10.5, S00.02.05): the
/// deployment identity's Contributor role cannot write Microsoft.Authorization resources, so they live in
/// <c>bootstrap.bicep</c> and <c>locks.bicep</c>, which the operator runs. A role or lock added to <c>main.bicep</c>
/// or a module it uses would fail every deploy, so this reads the templates as text and fails the pull request
/// instead. It also checks that the AI-assistant access is on in test and off in prod (mcp-server.md §3.5). Nothing
/// here opens a connection or calls Azure.
/// </summary>
public sealed partial class InfraTemplateTests
{
    [Fact]
    public void Main_bicep_and_its_modules_declare_no_Microsoft_Authorization_resource()
    {
        var infra = Path.Combine(Repository.Root(), "infra");
        var main = Path.Combine(infra, "main.bicep");
        var modules = ModulePaths().Matches(File.ReadAllText(main))
            .Select(match => Path.Combine(infra, match.Groups["path"].Value))
            .ToList();

        Assert.NotEmpty(modules);
        Assert.Contains(modules, module => Path.GetFileName(module) == "alerts.bicep");

        var offenders = new[] { main }
            .Concat(modules)
            .Where(file => AuthorizationResource().IsMatch(File.ReadAllText(file)))
            .Select(file => Path.GetRelativePath(infra, file))
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Main_bicep_sets_Mcp_Enabled_from_a_parameter_that_is_off_unless_a_parameter_file_turns_it_on()
    {
        string Infra(string file) => File.ReadAllText(Path.Combine(Repository.Root(), "infra", file));

        Assert.Matches(McpEnabledParameter(), Infra("main.bicep"));
        Assert.Matches(McpEnabledSetting(), Infra("main.bicep"));
        Assert.Matches(McpEnabledTrue(), Infra("test.bicepparam"));
        Assert.DoesNotMatch(McpEnabledTrue(), Infra("prod.bicepparam"));
    }

    // A module declaration, such as: module alerts 'modules/alerts.bicep' = if (...) {
    [GeneratedRegex(@"^\s*module\s+\w+\s+'(?<path>modules/[^']+)'", RegexOptions.Multiline)]
    private static partial Regex ModulePaths();

    // A resource declaration only: the namespace in a comment or a description does not match.
    [GeneratedRegex(@"^\s*resource\s+\w+\s+'Microsoft\.Authorization/", RegexOptions.Multiline)]
    private static partial Regex AuthorizationResource();

    [GeneratedRegex(@"^param mcpEnabled bool = false\s*$", RegexOptions.Multiline)]
    private static partial Regex McpEnabledParameter();

    [GeneratedRegex(@"^\s*Mcp__Enabled:\s*string\(mcpEnabled\)\s*$", RegexOptions.Multiline)]
    private static partial Regex McpEnabledSetting();

    [GeneratedRegex(@"^param mcpEnabled\s*=\s*true\s*$", RegexOptions.Multiline)]
    private static partial Regex McpEnabledTrue();
}
