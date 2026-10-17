using Azure;
using Azure.Communication.Email;

namespace Grow2Notes.Web.Platform.Email;

/// <summary>
/// Sends email through Azure Communication Services (design.md §10.1), from <paramref name="senderAddress"/>, with
/// a client that signs in as the app's user-assigned identity (<see cref="EmailSending.AddEmailWhenConfigured"/>).
/// </summary>
internal sealed class CommunicationServicesEmailSender(EmailClient client, string senderAddress) : IEmailSender
{
    /// <summary>
    /// Returns once Communication Services has accepted the message, and never waits for it to be delivered: the
    /// caller sends inside its transaction, which waiting would hold open for as long as delivery takes.
    /// </summary>
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var email = new Azure.Communication.Email.EmailMessage(
            senderAddress, message.To, new EmailContent(message.Subject) { PlainText = message.Text })
        {
            UserEngagementTrackingDisabled = true,
        };
        await client.SendAsync(WaitUntil.Started, email, cancellationToken);
    }
}
