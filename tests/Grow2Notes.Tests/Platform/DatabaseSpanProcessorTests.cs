using System.Diagnostics;
using Grow2Notes.Web.Platform;

namespace Grow2Notes.Tests.Platform;

/// <summary>
/// The processor on spans made here. TelemetryTests runs it on a span from the distro's own SqlClient instrumentation.
/// </summary>
public sealed class DatabaseSpanProcessorTests
{
    private static readonly string Canary = $"canary-{Guid.NewGuid():N}";

    [Fact]
    public void A_failed_database_span_keeps_its_error_status_and_loses_its_description()
    {
        using var span = FinishedSpan("db.system", "mssql",
            $"Violation of UNIQUE KEY constraint 'EmailIndex'. The duplicate key value is ({Canary}).");

        new DatabaseSpanProcessor().OnEnd(span);

        Assert.Equal(ActivityStatusCode.Error, span.Status);
        Assert.Null(span.StatusDescription);
    }

    [Fact]
    public void Other_spans_keep_their_description()
    {
        using var span = FinishedSpan("http.request.method", "GET", "Not a database failure.");

        new DatabaseSpanProcessor().OnEnd(span);

        Assert.Equal("Not a database failure.", span.StatusDescription);
    }

    private static Activity FinishedSpan(string tag, string value, string description)
    {
        var span = new Activity("Test").SetTag(tag, value).SetStatus(ActivityStatusCode.Error, description).Start();
        span.Stop();
        return span;
    }
}
