namespace Grow2Notes.Web.Platform.Email;

/// <summary>
/// Sends the app's email, which is only ever the setup-link email (design.md §8.1): through the email service in
/// Azure, and to the console where <c>Email:UseConsole</c> chooses it (<see cref="EmailSending.AddEmail"/>). The tests
/// put a fake in its place, so no test sends real email.
/// </summary>
internal interface IEmailSender
{
    /// <summary>
    /// Sends <paramref name="message"/>, returning once it has been handed on, without waiting for it to be delivered,
    /// and throwing when it could not be handed on.
    /// </summary>
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}
