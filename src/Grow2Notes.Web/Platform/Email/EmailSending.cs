using Azure.Communication.Email;
using Azure.Identity;

namespace Grow2Notes.Web.Platform.Email;

/// <summary>
/// Chooses the email sender: Communication Services where <c>Email:Endpoint</c> is set, as main.bicep sets it in
/// Azure with <c>Email:SenderAddress</c>, and otherwise the console (<see cref="ConsoleEmailSender"/>), as in local
/// development and CI's end-to-end run. The console is not refused outside Development, since CI's end-to-end run is
/// in Production: as for Key Vault and telemetry, the after-deploy check that the web app's settings name both is
/// what keeps an app in Azure from writing setup links to its console.
/// </summary>
internal static class EmailSending
{
    private const string EndpointKey = "Email:Endpoint";

    private const string SenderAddressKey = "Email:SenderAddress";

    public static IServiceCollection AddEmailWhenConfigured(
        this IServiceCollection services, IConfiguration configuration)
    {
        var endpoint = configuration[EndpointKey];
        if (string.IsNullOrEmpty(endpoint))
        {
            return services.AddSingleton<IEmailSender, ConsoleEmailSender>();
        }

        // Access keys are off on Communication Services, so it accepts only an Entra token from an identity with a
        // role on it, as the app's user-assigned identity has Communication and Email Service Owner (design.md §9.4).
        // Without the identity's client ID the credential would look for a system-assigned identity, which the app
        // does not have, and without the sender address there is nothing to send from, so either would let the app
        // start and fail on its first email, instead of here.
        var clientId = configuration["ManagedIdentity:ClientId"];
        if (string.IsNullOrEmpty(clientId))
        {
            throw new InvalidOperationException(
                $"{EndpointKey} is set but ManagedIdentity:ClientId is not. Set it to the client ID of the app's " +
                "user-assigned managed identity, which sends the email.");
        }

        var senderAddress = configuration[SenderAddressKey];
        if (string.IsNullOrEmpty(senderAddress))
        {
            throw new InvalidOperationException(
                $"{EndpointKey} is set but {SenderAddressKey} is not. Set it to the address the email is sent " +
                "from, the Email service domain's DoNotReply address.");
        }

        // One try, which waits at most 5 seconds for its answer, so that a send fails well within the SPA's 10-second
        // timeout, with nothing committed, instead of holding the request and its transaction open. The SDK's default
        // is 3 retries, each after the Retry-After of a 429, which is how Communication Services refuses a send over
        // its limits, and the screen would then say "no connection" for an invite that could still commit (T01's
        // Notes). The person's next press is the retry.
        var options = new EmailClientOptions { Retry = { MaxRetries = 0, NetworkTimeout = TimeSpan.FromSeconds(5) } };
        var client = new EmailClient(
            new Uri(endpoint),
            new ManagedIdentityCredential(ManagedIdentityId.FromUserAssignedClientId(clientId)),
            options);
        return services.AddSingleton<IEmailSender>(new CommunicationServicesEmailSender(client, senderAddress));
    }
}
