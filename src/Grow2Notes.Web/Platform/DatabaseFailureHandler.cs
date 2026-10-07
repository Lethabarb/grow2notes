using Microsoft.AspNetCore.Diagnostics;

namespace Grow2Notes.Web.Platform;

/// <summary>
/// The one place that logs a database failure in a request (design.md §9.5), with only the exception's type and SQL
/// error number, and answers with an empty <c>500</c>, whatever the environment.
/// </summary>
/// <remarks>
/// Handling the exception also keeps it out of every other log. ASP.NET Core 10's exception handler middleware neither
/// logs an exception that an <see cref="IExceptionHandler"/> handled nor reports it to diagnostics, where the
/// OpenTelemetry instrumentation would see it: that is the default when
/// <see cref="ExceptionHandlerOptions.SuppressDiagnosticsCallback"/> is not set. Kestrel never sees it, and EF Core's
/// own failure events are off in appsettings.json.
/// </remarks>
internal sealed partial class DatabaseFailureHandler(ILogger<DatabaseFailureHandler> logger) : IExceptionHandler
{
    public ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (DatabaseFailure.Find(exception) is not { } failure)
        {
            return ValueTask.FromResult(false);
        }

        // Without the exception: every logger provider would write its message, and telemetry would send it.
        LogDatabaseFailure(failure.ExceptionType, failure.SqlErrorNumber);
        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        return ValueTask.FromResult(true);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Database failure: {ExceptionType}, SQL error {SqlErrorNumber}")]
    private partial void LogDatabaseFailure(string exceptionType, int? sqlErrorNumber);
}
