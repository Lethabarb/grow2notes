namespace Grow2Notes.Web.Platform.Email;

/// <summary>
/// Logs one warning when the host starts, naming the email settings that are missing, so that an owner sees why every
/// email answers Not sent. It is a hosted service because an operator command builds the app and never starts the
/// host, so the warning never reaches the standard output that holds a setup link alone. It names settings only,
/// never a value, and never reads Key Vault.
/// </summary>
/// <param name="missingSettings">The names of the missing settings, joined by a comma and a space.</param>
/// <param name="logger">The logger for this warning.</param>
internal sealed partial class EmailNotConfiguredWarning(
    string missingSettings, ILogger<EmailNotConfiguredWarning> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        LogNotConfigured(missingSettings);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Email is not configured: {Settings} not set, so every email answers Not sent")]
    private partial void LogNotConfigured(string settings);
}
