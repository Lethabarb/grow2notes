using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Platform;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Grow2Notes.Tests.Platform;

/// <summary>
/// The processor in an OpenTelemetry logger made here, with a collector after it in the exporter's place.
/// TelemetryTests runs it as the app registers it.
/// </summary>
public sealed class DatabaseFailureLogProcessorTests
{
    private const string Template = "Check {Name} failed after {ElapsedMilliseconds}ms with message '{Description}'";

    private static readonly string Canary = $"canary-{Guid.NewGuid():N}";

    [Fact]
    public void A_database_failure_leaves_the_template_the_values_that_cannot_be_text_and_the_type_and_number()
    {
        var failure = new DbUpdateException("An error occurred while saving the entity changes.",
            TestSqlException.Create(2628, $"String or binary data would be truncated. Truncated value: '{Canary}'."));

        // The message as a value too, as the health check service logs a failed check's description.
        var log = Assert.Single(Export(logger =>
            logger.LogError(failure, Template, "database", 12.5, failure.InnerException!.Message)));

        Assert.Null(log.Exception);
        Assert.Null(log.FormattedMessage);
        Assert.Equal(Template, log.Body);
        Assert.Equal(
            [
                new("ElapsedMilliseconds", 12.5),
                new("{OriginalFormat}", Template),
                new("ExceptionType", "Microsoft.EntityFrameworkCore.DbUpdateException"),
                new("SqlErrorNumber", 2628),
            ],
            log.Attributes);
    }

    [Fact]
    public void Without_a_template_nothing_of_the_message_is_left()
    {
        var failure = TestSqlException.Create(2601, $"The duplicate key value is ({Canary}).");

        // State with no template, so OpenTelemetry makes the body from the formatted message.
        var log = Assert.Single(Export(logger =>
            logger.Log(LogLevel.Error, default, failure.Message, failure, (state, _) => state)));

        Assert.Null(log.Exception);
        Assert.Null(log.FormattedMessage);
        Assert.Null(log.Body);
        Assert.Equal(
            [new("ExceptionType", "Microsoft.Data.SqlClient.SqlException"), new("SqlErrorNumber", 2601)],
            log.Attributes);
    }

    [Fact]
    public void Other_exceptions_pass_through_unchanged()
    {
        var exception = new InvalidOperationException("Not a database failure.");

        var log = Assert.Single(Export(logger => logger.LogError(exception, "Something {Happened}", "else")));

        Assert.Same(exception, log.Exception);
        Assert.Equal("Something else", log.FormattedMessage);
        Assert.Contains(new("Happened", "else"), log.Attributes);
    }

    private static IReadOnlyCollection<ExportedLog> Export(Action<ILogger> log)
    {
        var exported = new LogCollector();
        using (var loggers = LoggerFactory.Create(logging => logging.AddOpenTelemetry(options =>
        {
            // As the distro sets it.
            options.IncludeFormattedMessage = true;
            options.AddProcessor(new DatabaseFailureLogProcessor()).AddProcessor(exported);
        })))
        {
            log(loggers.CreateLogger("Test"));
        }

        return exported.Logs;
    }
}
