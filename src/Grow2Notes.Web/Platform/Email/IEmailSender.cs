namespace Grow2Notes.Web.Platform.Email;

/// <summary>
/// Sends the app's email, which is only ever the setup-link email (A23): through Communication Services in Azure, and
/// to the console elsewhere (<see cref="EmailSending.AddEmailWhenConfigured"/>). The tests put a fake in its place,
/// so no test sends real email.
/// </summary>
internal interface IEmailSender
{
    /// <summary>
    /// Sends <paramref name="message"/>, returning once it has been handed on, without waiting for it to be delivered,
    /// and throwing when it could not be handed on.
    /// </summary>
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}
