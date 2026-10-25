using System.Text.RegularExpressions;
using Grow2Notes.Tests.Fixtures;

namespace Grow2Notes.Tests.Platform;

/// <summary>
/// The build check for the rule that the pipeline's template holds no lock or role (design.md §10.5, S00.02.05): the
/// deployment identity's Contributor role cannot write Microsoft.Authorization resources, so they live in
/// <c>bootstrap.bicep</c> and <c>locks.bicep</c>, which the operator runs. A role or lock added to <c>main.bicep</c>
/// or a module it uses would fail every deploy, so this reads the templates as text and fails the pull request
/// instead. Nothing here opens a connection or calls Azure.
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

    // A module declaration, such as: module alerts 'modules/alerts.bicep' = if (...) {
    [GeneratedRegex(@"^\s*module\s+\w+\s+'(?<path>modules/[^']+)'", RegexOptions.Multiline)]
    private static partial Regex ModulePaths();

    // A resource declaration only: the namespace in a comment or a description does not match.
    [GeneratedRegex(@"^\s*resource\s+\w+\s+'Microsoft\.Authorization/", RegexOptions.Multiline)]
    private static partial Regex AuthorizationResource();
}
