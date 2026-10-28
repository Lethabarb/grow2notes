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
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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

    private const string SenderAddressKey = "Email:SenderAddress";

    // Development is local development, which gets the switch from appsettings.Development.json; Production is what
    // App Service runs, with the switch set as CI's end-to-end run sets it. Either way no warning is registered.
    [Theory]
    [InlineData("Development", null)]
    [InlineData("Production", "true")]
    public async Task The_console_is_chosen_only_by_Email_UseConsole(string environment, string? useConsole)
    {
        await using var factory = new AppWithoutDatabase(environment);
        await using var app = useConsole is null ? factory : WithSettings(factory, [("Email:UseConsole", useConsole)]);

        Assert.IsType<ConsoleEmailSender>(app.Services.GetRequiredService<IEmailSender>());
        Assert.DoesNotContain(
            app.Services.GetServices<IHostedService>(), service => service is EmailNotConfiguredWarning);
    }

    // The other settings are present in every case, and Email:UseConsole is absent or false, so a missing setting is
    // all that differs: never the console, never a start that fails, and a send that fails before it calls out.
    [Theory]
    [InlineData(false, SenderAddress, null, "Email:ApiKeySecretUri")]
    [InlineData(true, null, null, SenderAddressKey)]
    [InlineData(true, "", null, SenderAddressKey)]
    [InlineData(true, "   ", null, SenderAddressKey)]
    [InlineData(false, null, null, "Email:ApiKeySecretUri, Email:SenderAddress")]
    [InlineData(true, null, "false", SenderAddressKey)]
    public async Task Without_Email_UseConsole_a_missing_setting_warns_once_and_every_send_fails(
        bool hasSecretUri, string? sender, string? useConsole, string missingSettings)
    {
        var logs = new RecordingLoggerProvider();
        (string Key, string Value)[] settings =
        [
            .. Settings.Where(setting => setting.Key == "ManagedIdentity:ClientId"),
            .. hasSecretUri ? [(SecretUriKey, SecretUri)] : Array.Empty<(string, string)>(),
            .. sender is null ? Array.Empty<(string, string)>() : [(SenderAddressKey, sender)],
            .. useConsole is null ? Array.Empty<(string, string)>() : [("Email:UseConsole", useConsole)],
        ];
        await using var factory = new AppWithoutDatabase("Production");
        await using var configured = WithSettings(factory, settings);
        await using var app = configured.WithWebHostBuilder(
            builder => builder.ConfigureLogging(logging => logging.ClearProviders().AddProvider(logs)));

        var emailSender = Assert.IsType<NotConfiguredEmailSender>(app.Services.GetRequiredService<IEmailSender>());

        // Values only: the settings' names, which the warning must hold, contain the words SecretUri and SenderAddress.
        string[] values = [VaultAddress, SecretUri, SenderAddress, ClientId];
        var warning = Assert.Single(
            logs.Entries, entry => entry.Category == typeof(EmailNotConfiguredWarning).FullName);
        Assert.Equal(LogLevel.Warning, warning.Level);
        Assert.All(missingSettings.Split(", "), name => Assert.True(warning.Holds(name)));
        Assert.All(values, value => Assert.False(warning.Holds(value)));

        var message = new EmailMessage("sam.taylor@example.org", "A subject", "A text");
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => emailSender.SendAsync(message, TestContext.Current.CancellationToken));
        Assert.Contains(missingSettings, exception.Message);
        Assert.All(values, value => Assert.DoesNotContain(value, exception.Message));
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
        Assert.DoesNotContain(
            app.Services.GetServices<IHostedService>(), service => service is EmailNotConfiguredWarning);
    }

    // A wrong value stops the start even when the sender is missing too.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_secret_uri_without_the_identity_client_id_stops_the_app_starting(bool hasSender)
    {
        await using var factory = new AppWithoutDatabase("Production");
        await using var app = WithSettings(
            factory,
            [.. Settings.Where(setting => setting.Key != "ManagedIdentity:ClientId"
                && (hasSender || setting.Key != SenderAddressKey))]);

        var exception = Assert.Throws<InvalidOperationException>(() => app.Services);
        Assert.Contains("Email:ApiKeySecretUri is set but ManagedIdentity:ClientId is not", exception.Message);
    }

    // An address with no secret in it is not where a key is, whether or not the sender is there to send from.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_setting_that_is_not_a_secret_uri_stops_the_app_starting(bool hasSender)
    {
        await using var factory = new AppWithoutDatabase("Production");
        await using var app = WithSettings(
            factory,
            [.. Settings.Where(
                setting => setting.Key != SecretUriKey && (hasSender || setting.Key != SenderAddressKey)),
                (SecretUriKey, VaultAddress)]);

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
