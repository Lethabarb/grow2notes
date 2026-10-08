using System.Net;
using System.Text.Json;

namespace Grow2Notes.Tests.Fixtures;

public static class ProblemResponse
{
    /// <summary>
    /// Asserts that <paramref name="response"/> is RFC 9457 problem details (design.md §6.1) whose status, in the
    /// response and in its body, is <paramref name="status"/>, and returns the body for the test to check further.
    /// </summary>
    public static async Task<string> ReadProblemAsync(this HttpResponseMessage response, HttpStatusCode status)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var problem = JsonDocument.Parse(body);
        Assert.Equal((int)status, problem.RootElement.GetProperty("status").GetInt32());
        return body;
    }
}
