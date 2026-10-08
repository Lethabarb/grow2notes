using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests.Platform;

/// <summary>
/// The error codes against design.md §6.9 itself, as <c>AuditTypesTests</c> checks the audit types against §5.4: each
/// code the app answers must be a code there, with the same status. The app answers only some of §6.9's codes so far,
/// so a code in §6.9 with no constant is no failure; each story adds the constants of the codes it answers.
/// </summary>
public sealed partial class ErrorCodeTests
{
    [Fact]
    public void Every_error_code_is_a_code_of_design_md_6_9_with_the_same_status()
    {
        var design = CodesIn(DesignMd.Read());
        var codes = ErrorCodes();
        Assert.NotEmpty(codes);

        string[] differences =
        [
            .. from code in codes
               let status = design.GetValueOrDefault(code.Code)
               where status != code.Status
               select status == 0
                   ? $"- {code.Code}: a constant, but not a code in design.md"
                   : $"- {code.Code}: answered {code.Status}, where design.md has {status}",
            .. from code in codes
               group code by code.Code into same
               where same.Count() > 1
               select $"- {same.Key}: the code of {same.Count()} constants",
        ];

        if (differences.Length > 0)
        {
            Assert.Fail($"The error codes differ from design.md §6.9:\n{string.Join('\n', differences)}");
        }
    }

    [Fact]
    public async Task An_error_code_answers_problem_details_with_its_status_and_the_code()
    {
        await using var services = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        using var body = new MemoryStream();
        context.Response.Body = body;

        await ErrorCode.PreconditionRequired.Problem().ExecuteAsync(context);

        Assert.Equal(StatusCodes.Status428PreconditionRequired, context.Response.StatusCode);
        Assert.Equal("application/problem+json", context.Response.ContentType);
        using var problem = JsonDocument.Parse(body.ToArray());
        Assert.Equal(StatusCodes.Status428PreconditionRequired, problem.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("precondition.required", problem.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public void Reading_6_9_takes_each_code_with_its_status_and_its_table_alone()
    {
        const string design = """
            ### 6.9 Error codes

            | HTTP | `code` | When |
            |---|---|---|
            | 401 | (none) | Not signed in. |
            | 409 | `note.version_conflict` | `baseVersion` is stale. |
            | 428 | `precondition.required` | A required `If-Match` header is missing. |

            ---

            ## 7. Architecture

            | HTTP | `code` | When |
            |---|---|---|
            | 418 | `next.section` | Not in §6.9. |
            """;

        Assert.Equal(
            new Dictionary<string, int> { ["note.version_conflict"] = 409, ["precondition.required"] = 428 },
            CodesIn(design));
    }

    [Theory]
    [InlineData("| 4xx | `note.a` | A status that is not a number. |", "4xx")]
    [InlineData("| 409 | note.a | A code out of backticks. |", "note.a")]
    [InlineData("| 409 | `note.a`, `note.b` | Two codes. |", "`note.b`")]
    [InlineData("| 409 | `Note.A` | A code that is not a code's name. |", "`Note.A`")]
    [InlineData("| 409 | `note.a` | Once. |\n| 412 | `note.a` | Twice. |", "`note.a` twice")]
    public void Reading_6_9_refuses_what_it_cannot_read(string rows, string named)
    {
        var design = $"""
            ### 6.9 Error codes

            | HTTP | `code` | When |
            |---|---|---|
            {rows}
            """;

        var ex = Assert.Throws<InvalidOperationException>(() => CodesIn(design));

        Assert.Contains(named, ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The codes of §6.9's table, each with its HTTP status. A row whose code is "(none)", as 401's is, has no code.
    /// </summary>
    private static Dictionary<string, int> CodesIn(string design)
    {
        var codes = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var row in DesignMd.OnlyTable(DesignMd.Section(design, "### 6.9 "), "§6.9"))
        {
            var (http, code) = (row["HTTP"], row["`code`"]);
            if (code == "(none)")
            {
                continue;
            }

            if (!int.TryParse(http, NumberStyles.None, CultureInfo.InvariantCulture, out var status)
                || CodeCell().Match(code) is not { Success: true } match)
            {
                throw new InvalidOperationException(
                    $"design.md §6.9 has the row \"{http} | {code}\", which this test cannot read.");
            }

            if (!codes.TryAdd(match.Groups[1].Value, status))
            {
                throw new InvalidOperationException($"design.md §6.9 has {code} twice.");
            }
        }

        return codes;
    }

    private static List<ErrorCode> ErrorCodes() =>
    [
        .. from field in typeof(ErrorCode).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
           where field.FieldType == typeof(ErrorCode)
           select (ErrorCode)field.GetValue(null)!,
    ];

    // One code, backticked, as §6.9 writes each.
    [GeneratedRegex(@"^`([a-z_]+\.[a-z_]+)`$")]
    private static partial Regex CodeCell();
}
