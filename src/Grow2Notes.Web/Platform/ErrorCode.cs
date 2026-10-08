using Microsoft.AspNetCore.Http.HttpResults;

namespace Grow2Notes.Web.Platform;

/// <summary>
/// A design.md §6.9 error code, with the HTTP status it is answered with, and the one way to answer it. Only the codes
/// the app answers are here, in §6.9's order, each added by the story that first answers it. A test reads §6.9's table
/// from design.md and fails unless each is a code there with the same status; the constructor is private, so the app
/// can answer no code that the test has not checked.
/// </summary>
internal sealed class ErrorCode
{
    /// <summary>The problem details extension member that holds the code (design.md §6.1).</summary>
    public const string Extension = "code";

    /// <summary>A stale <c>If-Match</c>.</summary>
    public static readonly ErrorCode PreconditionFailed =
        new(StatusCodes.Status412PreconditionFailed, "precondition.failed");

    /// <summary>Field errors, in the problem's <c>errors</c> (<see cref="ValidationProblems"/>).</summary>
    public static readonly ErrorCode ValidationFailed =
        new(StatusCodes.Status422UnprocessableEntity, "validation.failed");

    /// <summary>A required <c>If-Match</c> header is missing.</summary>
    public static readonly ErrorCode PreconditionRequired =
        new(StatusCodes.Status428PreconditionRequired, "precondition.required");

    private ErrorCode(int status, string code)
    {
        Status = status;
        Code = code;
    }

    public int Status { get; }

    public string Code { get; }

    /// <summary>Answers this code: problem details with its status, and the code in their <c>code</c> member.</summary>
    public ProblemHttpResult Problem() =>
        TypedResults.Problem(statusCode: Status, extensions: new Dictionary<string, object?> { [Extension] = Code });
}
