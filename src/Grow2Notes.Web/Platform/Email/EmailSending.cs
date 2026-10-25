using Azure.Identity;
using Azure.Security.KeyVault.Secrets;

namespace Grow2Notes.Web.Platform.Email;

/// <summary>
/// Chooses the email sender: Resend (<see cref="ResendEmailSender"/>) where <c>Email:ApiKeySecretUri</c> is set, and
/// otherwise the console (<see cref="ConsoleEmailSender"/>), as in local development and CI's end-to-end run. The
/// console is not refused outside Development, since CI's end-to-end run is in Production. An app in Azure never
/// falls back to it: main.bicep always sets <c>Email__ApiKeySecretUri</c>, and app.bicep refuses an empty
/// <c>Email__SenderAddress</c>.
/// </summary>
internal static class EmailSending
{
    private const string ApiKeySecretUriKey = "Email:ApiKeySecretUri";

    private const string SenderAddressKey = "Email:SenderAddress";

    // One deadline for the read of the key and the send, so that a send fails well within the SPA's 10-second timeout,
    // with nothing committed, instead of holding the request and its transaction open. The person's next press is the
    // retry.
    private static readonly TimeSpan SendTimeout = TimeSpan.FromSeconds(5);

    public static IServiceCollection AddEmailWhenConfigured(
        this IServiceCollection services, IConfiguration configuration)
    {
        var secretUri = configuration[ApiKeySecretUriKey];
        if (string.IsNullOrEmpty(secretUri))
        {
            return services.AddSingleton<IEmailSender, ConsoleEmailSender>();
        }

        // Without the identity's client ID the credential would look for a system-assigned identity, which the app
        // does not have, and without the sender address there is nothing to send from, so either would let the app
        // start and fail on its first email, instead of here.
        var clientId = configuration["ManagedIdentity:ClientId"];
        if (string.IsNullOrEmpty(clientId))
        {
            throw new InvalidOperationException(
                $"{ApiKeySecretUriKey} is set but ManagedIdentity:ClientId is not. Set it to the client ID of the " +
                "app's user-assigned managed identity, which reads the API key from Key Vault.");
        }

        var senderAddress = configuration[SenderAddressKey];
        if (string.IsNullOrEmpty(senderAddress))
        {
            throw new InvalidOperationException(
                $"{ApiKeySecretUriKey} is set but {SenderAddressKey} is not. Set it to the address the email is " +
                "sent from, on the domain verified in Resend.");
        }

        // The vault answers only an Entra token from an identity with a role on it, as the app's user-assigned
        // identity has Key Vault Secrets User (design.md §9.4).
        var secret = new KeyVaultSecretIdentifier(new Uri(secretUri));
        var secrets = new SecretClient(
            secret.VaultUri, new ManagedIdentityCredential(ManagedIdentityId.FromUserAssignedClientId(clientId)));
        services.AddHttpClient(nameof(ResendEmailSender)).AddTypedClient<IEmailSender>(
            http => new ResendEmailSender(http, secrets, secret.Name, senderAddress, SendTimeout));
        return services;
    }
}
