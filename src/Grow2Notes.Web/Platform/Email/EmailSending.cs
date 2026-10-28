using Azure.Identity;
using Azure.Security.KeyVault.Secrets;

namespace Grow2Notes.Web.Platform.Email;

/// <summary>
/// Chooses the email sender. A wrong value stops the start and an absent value warns. In order: the console
/// (<see cref="ConsoleEmailSender"/>) where <c>Email:UseConsole</c> is true, which only local development and CI's
/// end-to-end run set; then a stop for a set <c>Email:ApiKeySecretUri</c> that cannot work; then, where that or
/// <c>Email:SenderAddress</c> is blank, a sender that answers every email with Not sent
/// (<see cref="NotConfiguredEmailSender"/>) and one startup warning (<see cref="EmailNotConfiguredWarning"/>); and
/// otherwise Resend (<see cref="ResendEmailSender"/>). An app in Azure never falls back to the console, so a setup
/// link never reaches its standard output (design.md §10.3 and §10.4).
/// </summary>
internal static class EmailSending
{
    private const string UseConsoleKey = "Email:UseConsole";

    private const string ApiKeySecretUriKey = "Email:ApiKeySecretUri";

    private const string SenderAddressKey = "Email:SenderAddress";

    // One deadline for the read of the key and the send, so that a send fails well within the SPA's 10-second timeout,
    // with nothing committed, instead of holding the request and its transaction open. The person's next press is the
    // retry.
    private static readonly TimeSpan SendTimeout = TimeSpan.FromSeconds(5);

    public static IServiceCollection AddEmail(this IServiceCollection services, IConfiguration configuration)
    {
        if (configuration.GetValue<bool>(UseConsoleKey))
        {
            return services.AddSingleton<IEmailSender, ConsoleEmailSender>();
        }

        var secretUri = configuration[ApiKeySecretUriKey];
        var senderAddress = configuration[SenderAddressKey];
        (SecretClient Client, string Name)? key = null;
        if (!string.IsNullOrWhiteSpace(secretUri))
        {
            // Without the identity's client ID the credential would look for a system-assigned identity, which the
            // app does not have, so it would start and fail on its first email, instead of here.
            var clientId = configuration["ManagedIdentity:ClientId"];
            if (string.IsNullOrEmpty(clientId))
            {
                throw new InvalidOperationException(
                    $"{ApiKeySecretUriKey} is set but ManagedIdentity:ClientId is not. Set it to the client ID of " +
                    "the app's user-assigned managed identity, which reads the API key from Key Vault.");
            }

            // The vault answers only an Entra token from an identity with a role on it, as the app's user-assigned
            // identity has Key Vault Secrets User (design.md §9.4).
            var secret = new KeyVaultSecretIdentifier(new Uri(secretUri));
            key = (new SecretClient(
                secret.VaultUri, new ManagedIdentityCredential(ManagedIdentityId.FromUserAssignedClientId(clientId))),
                secret.Name);
        }

        if (key is { } vault && !string.IsNullOrWhiteSpace(senderAddress))
        {
            return services.AddHttpClient(nameof(ResendEmailSender)).AddTypedClient<IEmailSender>(
                http => new ResendEmailSender(http, vault.Client, vault.Name, senderAddress, SendTimeout)).Services;
        }

        var missing = new List<string>();
        if (key is null)
        {
            missing.Add(ApiKeySecretUriKey);
        }

        if (string.IsNullOrWhiteSpace(senderAddress))
        {
            missing.Add(SenderAddressKey);
        }

        var names = string.Join(", ", missing);
        services.AddSingleton<IEmailSender>(new NotConfiguredEmailSender(names));
        return services.AddHostedService(provider => new EmailNotConfiguredWarning(
            names, provider.GetRequiredService<ILogger<EmailNotConfiguredWarning>>()));
    }
}
