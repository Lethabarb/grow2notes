using System.Collections.Concurrent;
using Grow2Notes.Web.Platform.Email;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Grow2Notes.Tests.Fixtures;

/// <summary>
/// Stands in for the app's email sender in every app that <see cref="Grow2NotesFactory"/> hosts, so no test sends real
/// email (design.md A58): it records each message instead. Each host has one of its own, so an app made with
/// <c>WithWebHostBuilder</c> starts with none recorded, and a class fixture's is shared by the class's tests.
/// </summary>
internal sealed class RecordingEmailSender : IEmailSender
{
    private readonly ConcurrentQueue<EmailMessage> sent = new();

    /// <summary>Every message sent so far, in the order sent.</summary>
    public IReadOnlyCollection<EmailMessage> Sent => sent;

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        sent.Enqueue(message);
        return Task.CompletedTask;
    }

    /// <summary>Puts a recording sender of the host's own in place of the app's.</summary>
    public static void ReplaceAppSender(IServiceCollection services)
    {
        services.RemoveAll<IEmailSender>();
        services.AddSingleton<RecordingEmailSender>();
        services.AddSingleton<IEmailSender>(provider => provider.GetRequiredService<RecordingEmailSender>());
    }
}
