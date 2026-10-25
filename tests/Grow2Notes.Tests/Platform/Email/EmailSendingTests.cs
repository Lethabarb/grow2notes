using System.Reflection;
using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Platform.Email;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests.Platform.Email;

/// <summary>
/// Which email sender the app has (<see cref="EmailSending"/>), read from the app's own services. The tests host the
/// app without a database, so they run without Docker, and read nothing from a vault and send nothing to Resend:
/// building the vault's client and its credential calls nothing.
/// </summary>
public sealed class EmailSendingTests
{
    // No such resource exists, and nothing here reads from it.
    private const string VaultAddress = "https://kv-grow2notes-example.vault.azure.net/";

    private const string SecretUriKey = "Email:ApiKeySecretUri";

    private const string SecretUri = VaultAddress + "secrets/resend-api-key";

    private const string SenderAddress = "Grow2Notes <setup@example.org>";

    private const string ClientId = "00000000-0000-0000-0000-000000000001";

    private static readonly (string Key, string Value)[] Settings =
    [
        (SecretUriKey, SecretUri),
        ("Email:SenderAddress", SenderAddress),
        ("ManagedIdentity:ClientId", ClientId),
    ];

    // Development is local development; Production is what App Service runs, and what CI's end-to-end run runs.
    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task Without_an_api_key_secret_the_email_goes_to_the_console(string environment)
    {
        await using var app = new AppWithoutDatabase(environment);

        Assert.IsType<ConsoleEmailSender>(app.Services.GetRequiredService<IEmailSender>());
    }

    [Fact]
    public async Task With_the_settings_the_key_is_read_from_Key_Vault_as_the_app_identity_and_no_key_is_held()
    {
        await using var factory = new AppWithoutDatabase("Production");
        await using var app = WithSettings(factory, Settings);

        var sender = Assert.IsType<ResendEmailSender>(app.Services.GetRequiredService<IEmailSender>());

        var reachable = Reachable(sender);
        var secrets = Assert.Single(reachable.OfType<SecretClient>());
        Assert.Equal(new Uri(VaultAddress), secrets.VaultUri);

        // A client made from a key holds an AzureKeyCredential instead.
        var credential = Assert.IsType<ManagedIdentityCredential>(Assert.Single(
            Reachable(secrets), item => item is TokenCredential or AzureKeyCredential or AzureNamedKeyCredential));
        Assert.Equal($"ClientId {ClientId}", ManagedIdentities.IdentityOf(credential));

        // The sender holds where the key is, never the key.
        Assert.Contains("resend-api-key", reachable.OfType<string>());
        Assert.Contains(SenderAddress, reachable.OfType<string>());
        Assert.Contains(TimeSpan.FromSeconds(5), reachable.OfType<TimeSpan>());
    }

    [Theory]
    [InlineData("ManagedIdentity:ClientId")]
    [InlineData("Email:SenderAddress")]
    public async Task A_secret_uri_without_the_other_setting_stops_the_app_starting(string missing)
    {
        await using var factory = new AppWithoutDatabase("Production");
        await using var app = WithSettings(factory, [.. Settings.Where(setting => setting.Key != missing)]);

        var exception = Assert.Throws<InvalidOperationException>(() => app.Services);
        Assert.Contains($"Email:ApiKeySecretUri is set but {missing} is not", exception.Message);
    }

    // An address with no secret in it is not where a key is.
    [Fact]
    public async Task A_setting_that_is_not_a_secret_uri_stops_the_app_starting()
    {
        await using var factory = new AppWithoutDatabase("Production");
        await using var app = WithSettings(
            factory,
            [.. Settings.Where(setting => setting.Key != SecretUriKey), (SecretUriKey, VaultAddress)]);

        Assert.ThrowsAny<ArgumentException>(() => app.Services);
    }

    // Host settings rather than app configuration: Program.cs reads these while it registers services, and
    // WebApplicationFactory hands host settings to the app before Program.cs runs, but adds app configuration only
    // when the host is built.
    private static WebApplicationFactory<Program> WithSettings(
        WebApplicationFactory<Program> factory, (string Key, string Value)[] settings) =>
        factory.WithWebHostBuilder(builder =>
        {
            foreach (var (key, value) in settings)
            {
                builder.UseSetting(key, value);
            }
        });

    // Everything reachable from root through the fields of the Azure SDK's own types, of arrays and of structs, which
    // is how a client holds its pipeline and the pipeline its policies, the one that signs requests among them. It
    // stops at a credential and at .NET's own classes, such as HttpClient, which hold nothing of the SDK's.
    private static List<object> Reachable(object root)
    {
        var reachable = new List<object>();
        var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var pending = new Stack<object>([root]);
        while (pending.TryPop(out var item))
        {
            var type = item.GetType();
            if (!type.IsValueType && !seen.Add(item))
            {
                continue;
            }

            reachable.Add(item);
            if (item is Array array)
            {
                foreach (var element in array)
                {
                    if (element is not null)
                    {
                        pending.Push(element);
                    }
                }

                continue;
            }

            var holdsSdkParts = item == root
                || (type.IsValueType && !type.IsPrimitive && !type.IsEnum)
                || type.Namespace?.StartsWith("Azure", StringComparison.Ordinal) == true;
            if (!holdsSdkParts || item is TokenCredential)
            {
                continue;
            }

            for (var declaring = type; declaring is not null; declaring = declaring.BaseType)
            {
                foreach (var field in declaring.GetFields(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (field.GetValue(item) is { } value)
                    {
                        pending.Push(value);
                    }
                }
            }
        }

        return reachable;
    }
}
