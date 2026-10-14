using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Platform.Email;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Grow2Notes.Tests.Platform.Email;

/// <summary>
/// The console email sender of local development (design.md §10.3), as the app registers it. The test hosts the app
/// without a database, so it runs without Docker.
/// </summary>
[Collection<StandardOutputCollection>]
public sealed class ConsoleEmailSenderTests
{
    [Fact]
    public async Task Writes_each_email_to_standard_output_and_nothing_to_any_logger()
    {
        var logs = new RecordingLoggerProvider();
        await using var factory = new AppWithoutDatabase("Development");

        // The app's only logger provider, so the console logger writes nothing to standard output either.
        await using var app = factory.WithWebHostBuilder(
            builder => builder.ConfigureLogging(logging => logging.ClearProviders().AddProvider(logs)));
        var sender = app.Services.GetRequiredService<IEmailSender>();
        var message = new EmailMessage(
            $"sam.taylor.{Guid.NewGuid():N}@example.org", "A subject", $"A first line\n\nA link {Guid.NewGuid():N}");

        var standardOutput = Console.Out;
        using var written = new StringWriter();
        Console.SetOut(written);
        try
        {
            await sender.SendAsync(message, TestContext.Current.CancellationToken);
        }
        finally
        {
            Console.SetOut(standardOutput);
        }

        Assert.Equal($"To: {message.To}\nSubject: {message.Subject}\n\n{message.Text}\n", written.ToString());
        Assert.DoesNotContain(
            logs.Entries.SelectMany(entry => entry.Values.Select(value => value.Value?.ToString())
                .Append(entry.Message)
                .Append(entry.Exception?.ToString())),
            text => text is not null && (text.Contains(message.To) || text.Contains(message.Text)));
    }
}
