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

    /// <summary>The email already belongs to an account, in any organisation and any letter case (A27).</summary>
    public static readonly ErrorCode EmailInUse = new(StatusCodes.Status409Conflict, "user.email_in_use");

    /// <summary>The setup link is expired, used or replaced.</summary>
    public static readonly ErrorCode SetupLinkInvalid = new(StatusCodes.Status410Gone, "auth.setup_link_invalid");

    /// <summary>A stale <c>If-Match</c>.</summary>
    public static readonly ErrorCode PreconditionFailed =
        new(StatusCodes.Status412PreconditionFailed, "precondition.failed");

    /// <summary>
    /// A reorder's IDs are not the list's active rows, each exactly once
    /// (<see cref="Configuration.ListOrder.Reorder"/>), or a row of the list changed while it was being reordered.
    /// </summary>
    public static readonly ErrorCode OrderMismatch =
        new(StatusCodes.Status422UnprocessableEntity, "config.order_mismatch");

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
