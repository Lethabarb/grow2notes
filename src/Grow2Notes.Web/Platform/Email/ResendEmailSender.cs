using System.Net.Http.Headers;
using System.Net.Http.Json;
using Azure.Security.KeyVault.Secrets;

namespace Grow2Notes.Web.Platform.Email;

/// <summary>
/// Sends email through Resend's HTTP API (design.md §9.4), from <paramref name="senderAddress"/>, with the API key
/// that each deploy writes to Key Vault as <paramref name="apiKeySecretName"/> from the environment's RESEND_API_KEY
/// secret, read as the app's user-assigned identity (<see cref="EmailSending.AddEmail"/>).
/// </summary>
/// <param name="http">A client with no base address and no default headers: each request carries its own.</param>
/// <param name="secrets">The client of the vault that holds the API key.</param>
/// <param name="apiKeySecretName">The name of the vault's secret that holds the API key.</param>
/// <param name="senderAddress">The address to send from: a bare address, or a display name and an address.</param>
/// <param name="timeout">How long the read of the key and the send together may take.</param>
internal sealed class ResendEmailSender(
    HttpClient http, SecretClient secrets, string apiKeySecretName, string senderAddress, TimeSpan timeout)
    : IEmailSender
{
    public static readonly Uri ApiAddress = new("https://api.resend.com/emails");

    /// <summary>
    /// Returns once Resend has accepted the message, and never waits for it to be delivered: the caller sends inside
    /// its transaction, which waiting would hold open for as long as delivery takes. The key is read at each send and
    /// held in a local only, so a rotated key works at the next send and a missing one fails only the send. The one
    /// deadline covers the read and the send, and the send is made once: the person's next press is the retry. A
    /// refusal throws with its status alone, as the answer's body can hold the address or the link.
    /// </summary>
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);

        var key = await secrets.GetSecretAsync(apiKeySecretName, cancellationToken: deadline.Token);

        using var request = new HttpRequestMessage(HttpMethod.Post, ApiAddress)
        {
            Content = JsonContent.Create(new ResendEmail(senderAddress, [message.To], message.Subject, message.Text)),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key.Value.Value);

        // Resend rejects a request that has no User-Agent.
        request.Headers.UserAgent.ParseAdd("Grow2Notes");

        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
        response.EnsureSuccessStatusCode();
    }

    private sealed record ResendEmail(string From, string[] To, string Subject, string Text);
}
