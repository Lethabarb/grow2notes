using System.Text.RegularExpressions;
using Grow2Notes.Tests.Fixtures;

namespace Grow2Notes.Tests.Platform;

/// <summary>
/// The build check for the rule that the pipeline's template holds no lock or role (design.md §10.5, S00.02.05): the
/// deployment identity's Contributor role cannot write Microsoft.Authorization resources, so they live in
/// <c>bootstrap.bicep</c> and <c>locks.bicep</c>, which the operator runs. A role or lock added to <c>main.bicep</c>
/// or a module it uses would fail every deploy, so this reads the templates as text and fails the pull request
/// instead. It also checks that the AI-assistant access is on in test and off in prod (mcp-server.md §3.5), that the
/// Resend API key is a secure parameter that only the Key Vault secret receives, and that no deploy chooses the
/// console sender, which would write setup links to the app's stdout (design.md §10.4, T40). Nothing here opens a
/// connection or calls Azure.
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

    [Fact]
    public void The_Resend_key_is_a_secure_parameter_that_only_the_vault_secret_receives()
    {
        var infra = Path.Combine(Repository.Root(), "infra");
        var filesNamingIt = InfraFiles()
            .Where(file => File.ReadAllText(file).Contains(KeyParameter, StringComparison.Ordinal))
            .Select(file => Path.GetRelativePath(infra, file).Replace(Path.DirectorySeparatorChar, '/'))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(["main.bicep", "modules/keyvault.bicep"], filesNamingIt);

        foreach (var file in filesNamingIt)
        {
            var lines = File.ReadAllLines(Path.Combine(infra, file)).Select(line => line.Trim()).ToList();

            for (var index = 0; index < lines.Count; index++)
            {
                if (lines[index].StartsWith("param " + KeyParameter, StringComparison.Ordinal))
                {
                    Assert.True(index > 0 && lines[index - 1] == "@secure()", $"{file}: the key is not secure.");
                }
            }

            Assert.DoesNotContain(
                lines,
                line => line.StartsWith("output ", StringComparison.Ordinal) && line.Contains(KeyParameter));
        }

        Assert.Equal(
            ["param resendApiKey string = ''", "resendApiKey: resendApiKey"],
            NonCommentLinesNamingTheKey(Path.Combine(infra, "main.bicep")));

        var vault = Path.Combine(infra, "modules", "keyvault.bicep");
        var vaultLines = NonCommentLinesNamingTheKey(vault);

        Assert.Equal(3, vaultLines.Count);
        Assert.Equal("param resendApiKey string", vaultLines[0]);
        Assert.Matches(VaultSecretResource(), vaultLines[1]);
        Assert.Equal("value: resendApiKey", vaultLines[2]);
        Assert.Matches(VaultSecretDeclaration(), File.ReadAllText(vault));

        var deploy = File.ReadAllLines(Path.Combine(Repository.Root(), ".github", "workflows", "deploy.yml"));
        var deployLinesNamingIt = deploy.Where(line => line.Contains(KeyParameter)).ToList();

        Assert.Single(deployLinesNamingIt);
        Assert.Contains("resendApiKey=\"$RESEND_API_KEY\"", deployLinesNamingIt[0]);
        Assert.DoesNotContain(deploy, line => line.Contains("--mode"));
    }

    [Fact]
    public void No_deploy_chooses_the_console_sender()
    {
        var files = InfraFiles().Append(Path.Combine(Repository.Root(), ".github", "workflows", "deploy.yml"));
        var offenders = files
            .SelectMany(file => ConsoleSenderChoices
                .Where(choice => File.ReadAllText(file).Contains(choice, StringComparison.Ordinal))
                .Select(choice => $"{Path.GetFileName(file)}: {choice}"))
            .ToList();

        Assert.Empty(offenders);
    }

    private const string KeyParameter = "resendApiKey";

    // The switch that makes the app write its emails to the console, and the environment names that would load the
    // development settings which turn it on. Azure sets none of them.
    private static readonly string[] ConsoleSenderChoices =
        ["UseConsole", "ASPNETCORE_ENVIRONMENT", "DOTNET_ENVIRONMENT"];

    private static IEnumerable<string> InfraFiles() =>
        Directory.EnumerateFiles(Path.Combine(Repository.Root(), "infra"), "*", SearchOption.AllDirectories)
            .Where(file => Path.GetExtension(file) is ".bicep" or ".bicepparam");

    private static List<string> NonCommentLinesNamingTheKey(string file) =>
        File.ReadAllLines(file)
            .Select(line => line.Trim())
            .Where(line => !line.StartsWith("//", StringComparison.Ordinal) && line.Contains(KeyParameter))
            .ToList();

    // A module declaration, such as: module alerts 'modules/alerts.bicep' = if (...) {
    [GeneratedRegex(@"^\s*module\s+\w+\s+'(?<path>modules/[^']+)'", RegexOptions.Multiline)]
    private static partial Regex ModulePaths();

    // A resource declaration only: the namespace in a comment or a description does not match.
    [GeneratedRegex(@"^\s*resource\s+\w+\s+'Microsoft\.Authorization/", RegexOptions.Multiline)]
    private static partial Regex AuthorizationResource();

    // The secret's declaration line: it exists only if the key is not empty.
    [GeneratedRegex(
        @"^resource resendApiKeySecret 'Microsoft\.KeyVault/vaults/secrets@[\d-]+' = "
        + @"if \(!empty\(resendApiKey\)\) \{$")]
    private static partial Regex VaultSecretResource();

    // The whole declaration: a child of the vault, named resend-api-key, which sets only its value.
    [GeneratedRegex(
        @"^resource resendApiKeySecret .*\{\s+parent: vault\s+name: 'resend-api-key'\s+"
        + @"properties: \{\s+value: resendApiKey\s+\}",
        RegexOptions.Multiline)]
    private static partial Regex VaultSecretDeclaration();

    [GeneratedRegex(@"^param mcpEnabled bool = false\s*$", RegexOptions.Multiline)]
    private static partial Regex McpEnabledParameter();

    [GeneratedRegex(@"^\s*Mcp__Enabled:\s*string\(mcpEnabled\)\s*$", RegexOptions.Multiline)]
    private static partial Regex McpEnabledSetting();

    [GeneratedRegex(@"^param mcpEnabled\s*=\s*true\s*$", RegexOptions.Multiline)]
    private static partial Regex McpEnabledTrue();
}
