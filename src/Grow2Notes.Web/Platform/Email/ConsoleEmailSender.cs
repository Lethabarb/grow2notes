namespace Grow2Notes.Web.Platform.Email;

/// <summary>
/// Writes each email to standard output in place of sending it, where no email service is configured: in
/// local development, which uses no Azure resource (design.md §10.3), and in CI's end-to-end run, so CI never sends
/// real email. It never writes through <see cref="ILogger"/>, so a setup link, which holds a token, and the address
/// it goes to never reach logs or telemetry (§9.5).
/// </summary>
internal sealed class ConsoleEmailSender : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        // In one write, which the console's writer keeps whole when two requests send at once.
        var written = $"""
            To: {message.To}
            Subject: {message.Subject}

            {message.Text}

            """;
        await Console.Out.WriteAsync(written.AsMemory(), cancellationToken);
    }
}
