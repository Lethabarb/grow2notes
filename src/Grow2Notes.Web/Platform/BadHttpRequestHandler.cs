using Microsoft.AspNetCore.Diagnostics;

namespace Grow2Notes.Web.Platform;

/// <summary>
/// Answers a request that the server could not read, such as a body over Kestrel's size limit, with the status its
/// <see cref="BadHttpRequestException"/> carries (<c>400</c>, or <c>413</c> for the size limit), not a <c>500</c>.
/// Like <see cref="DatabaseFailureHandler"/>, it writes no body, so the status code pages in Program.cs give the bare
/// status problem details with none of the exception's text.
/// </summary>
/// <remarks>
/// It logs nothing: the request was at fault, not the app. Handling the exception keeps it out of the exception
/// handler middleware's error log too, for the reasons in <see cref="DatabaseFailureHandler"/>'s remarks.
/// </remarks>
internal sealed class BadHttpRequestHandler : IExceptionHandler
{
    public ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not BadHttpRequestException badRequest)
        {
            return ValueTask.FromResult(false);
        }

        httpContext.Response.StatusCode = badRequest.StatusCode;
        return ValueTask.FromResult(true);
    }
}
