using Microsoft.AspNetCore.Diagnostics;

namespace Grow2Notes.Web.Platform;

/// <summary>
/// The one place that logs a database failure in a request (design.md §9.5), with only the exception's type and SQL
/// error number, and answers <c>500</c>, whatever the environment; unless the client has gone, when it logs nothing
/// and answers <c>499</c>. It writes no body: the status code pages in Program.cs give the bare status the same problem
/// details as any other error, with none of the exception's text.
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

        // The client has gone, which cancels the request's SQL command. SqlClient reports a command stopped that way as
        // SQL error 0, not as a cancellation, so the exception handler middleware does not take it for an aborted
        // request. It is no failure of the database, and nobody reads the answer: 499, as the middleware gives an
        // aborted request.
        if (httpContext.RequestAborted.IsCancellationRequested)
        {
            httpContext.Response.StatusCode = StatusCodes.Status499ClientClosedRequest;
            return ValueTask.FromResult(true);
        }

        // Without the exception: every logger provider would write its message, and telemetry would send it.
        LogDatabaseFailure(failure.ExceptionType, failure.SqlErrorNumber);
        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        return ValueTask.FromResult(true);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Database failure: {ExceptionType}, SQL error {SqlErrorNumber}")]
    private partial void LogDatabaseFailure(string exceptionType, int? sqlErrorNumber);
}
