using Grow2Notes.Web.Platform.Email;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Grow2Notes.Tests.Fixtures;

/// <summary>
/// Stands in for the app's email sender in a test of an email that cannot be sent: it sends nothing and throws, as
/// an email service can, an exception whose message holds the address and the whole text, setup link and all, so
/// that a test shows that neither reaches the logs (design.md §9.5).
/// </summary>
internal sealed class FailingEmailSender : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken) =>
        throw new InvalidOperationException($"Could not send to {message.To}: {message.Text}");

    /// <summary>
    /// Puts a failing sender in place of the app's, or of the recording one that the factory puts there.
    /// </summary>
    public static void ReplaceAppSender(IServiceCollection services)
    {
        services.RemoveAll<IEmailSender>();
        services.AddSingleton<IEmailSender, FailingEmailSender>();
    }
}
