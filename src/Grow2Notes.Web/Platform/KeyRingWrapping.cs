using Azure.Identity;
using Microsoft.AspNetCore.DataProtection;

namespace Grow2Notes.Web.Platform;

/// <summary>
/// Wraps each Data Protection key with the Key Vault key before it is stored in SQL (design.md §9.3), when
/// <c>DataProtection:KeyVaultKeyUri</c> is set, as it is in Azure. Local development and the tests have no Key Vault,
/// so their keys are stored unwrapped.
/// </summary>
internal static class KeyRingWrapping
{
    public static IDataProtectionBuilder ProtectKeysWithKeyVaultWhenConfigured(
        this IDataProtectionBuilder dataProtection, IConfiguration configuration)
    {
        var keyUri = configuration["DataProtection:KeyVaultKeyUri"];
        if (string.IsNullOrEmpty(keyUri))
        {
            return dataProtection;
        }

        // Only the app's user-assigned identity may wrap and unwrap with the key (design.md §9.4). Without its client
        // ID the credential would look for a system-assigned identity, which the app does not have, so the app would
        // start and fail later, on its first use of a key, instead of here.
        var clientId = configuration["ManagedIdentity:ClientId"];
        if (string.IsNullOrEmpty(clientId))
        {
            throw new InvalidOperationException(
                "DataProtection:KeyVaultKeyUri is set but ManagedIdentity:ClientId is not. Set it to the client ID " +
                "of the app's user-assigned managed identity, which wraps and unwraps the Data Protection keys.");
        }

        // The key URI has no version, so each new Data Protection key is wrapped with the Key Vault key's current
        // version, and rotating the key in Key Vault needs no change here.
        return dataProtection.ProtectKeysWithAzureKeyVault(
            new Uri(keyUri),
            new ManagedIdentityCredential(ManagedIdentityId.FromUserAssignedClientId(clientId)));
    }
}
