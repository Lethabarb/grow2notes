using Grow2Notes.Tests.Fixtures;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Grow2Notes.Tests.Platform;

[Collection<SqlServerCollection>]
public sealed class KeyRingWrappingTests(Grow2NotesFactory factory) : IClassFixture<Grow2NotesFactory>
{
    // No such vault exists. Only wrapping a new key or unwrapping a stored one would call it, and no test here does.
    private const string KeyUri = "https://kv-example.vault.azure.net/keys/data-protection";

    private const string ClientId = "00000000-0000-0000-0000-000000000001";

    [Fact]
    public void Without_a_key_URI_the_keys_are_stored_unwrapped()
    {
        Assert.Null(KeyManagement(factory).XmlEncryptor);
    }

    [Fact]
    public async Task With_a_key_URI_and_a_client_ID_new_keys_are_wrapped_by_the_Key_Vault_key()
    {
        // Data Protection loads the key ring as the app starts, and makes a key if there is none. Starting the
        // unwrapped app first leaves a current key in the shared database, so the wrapped one has no key to wrap.
        _ = factory.Services;
        await using var wrapped = WithSettings(
            ("DataProtection:KeyVaultKeyUri", KeyUri),
            ("ManagedIdentity:ClientId", ClientId));

        Assert.Equal(
            "Azure.Extensions.AspNetCore.DataProtection.Keys.AzureKeyVaultXmlEncryptor",
            KeyManagement(wrapped).XmlEncryptor?.GetType().FullName);
    }

    [Fact]
    public async Task A_key_URI_without_a_client_ID_stops_the_app_starting()
    {
        await using var app = WithSettings(("DataProtection:KeyVaultKeyUri", KeyUri));

        var exception = Assert.Throws<InvalidOperationException>(() => app.Services);
        Assert.Contains("ManagedIdentity:ClientId is not", exception.Message);
    }

    // Host settings rather than app configuration: Program.cs reads these while it registers services, and
    // WebApplicationFactory hands host settings to the app before Program.cs runs, but adds app configuration only
    // when the host is built.
    private WebApplicationFactory<Program> WithSettings(params (string Key, string Value)[] settings) =>
        factory.WithWebHostBuilder(builder =>
        {
            foreach (var (key, value) in settings)
            {
                builder.UseSetting(key, value);
            }
        });

    private static KeyManagementOptions KeyManagement(WebApplicationFactory<Program> app) =>
        app.Services.GetRequiredService<IOptions<KeyManagementOptions>>().Value;
}
