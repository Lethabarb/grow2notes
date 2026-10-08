using System.Text.Json;

namespace Grow2Notes.Web.Platform;

/// <summary>
/// Turns the problem that minimal APIs' built-in validation answers with into design.md §6.1's: <c>422</c> with the
/// code <c>validation.failed</c>, and <c>errors</c> keyed by each field as the request's JSON names it.
/// </summary>
/// <remarks>
/// The validation filter answers <c>400</c> with an <see cref="HttpValidationProblemDetails"/>, which it writes
/// through the problem details service, so the service's customisation is where the app can change it. Nothing else
/// writes that type through the service. The filter keys each error by the C# path of the member that broke a rule,
/// such as <c>Part.Value</c>, where the API's JSON is camelCase (design.md §6.1). The service writes only for a request
/// whose <c>Accept</c> allows JSON, as the SPA's requests do; any other gets the filter's own <c>400</c>, unchanged.
/// </remarks>
internal static class ValidationProblems
{
    // ASP.NET Core's type for a 422, which ErrorCode.Problem gives any other 422 code. The service gave this problem
    // the type of a 400 before the customisation runs.
    private const string UnprocessableEntityType = "https://tools.ietf.org/html/rfc4918#section-11.2";

    /// <summary>
    /// For <see cref="ProblemDetailsOptions.CustomizeProblemDetails"/>. Leaves every other problem as it is.
    /// </summary>
    public static void Customize(ProblemDetailsContext context)
    {
        if (context.ProblemDetails is not HttpValidationProblemDetails problem)
        {
            return;
        }

        var failed = ErrorCode.ValidationFailed;
        context.HttpContext.Response.StatusCode = failed.Status;
        problem.Status = failed.Status;
        problem.Type = UnprocessableEntityType;
        problem.Extensions[ErrorCode.Extension] = failed.Code;
        problem.Errors = problem.Errors.ToDictionary(
            error => string.Join('.', error.Key.Split('.').Select(JsonNamingPolicy.CamelCase.ConvertName)),
            error => error.Value,
            StringComparer.Ordinal);
    }
}
