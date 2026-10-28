namespace Grow2Notes.Web.Platform.Email;

/// <summary>
/// Stands in for the email service where <see cref="EmailSending"/> finds a setting missing, and fails every send
/// before any call to Key Vault or Resend, so the caller answers Not sent with nothing committed. It holds the names
/// of the missing settings and never a value.
/// </summary>
/// <param name="missingSettings">The names of the missing settings, joined by a comma and a space.</param>
internal sealed class NotConfiguredEmailSender(string missingSettings) : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken) =>
        Task.FromException(new InvalidOperationException($"Email is not configured: {missingSettings} not set."));
}
