using System.Text.Json;
using System.Text.Json.Nodes;

namespace Grow2Notes.Web.Features.Mcp.OAuth;

/// <summary>
/// Fetches an approved assistant's client metadata document, for <c>admin mcp-client add --metadata-url</c> and nothing
/// else (mcp-server.md §11.2). It follows no redirect, and refuses a body over <see cref="MaxBytes"/> or a fetch that
/// takes over <see cref="Timeout"/>, because the handler and client the app registers it with are set so (Program.cs).
/// It logs nothing, because the operator's output holds the command's result and nothing else.
/// </summary>
internal sealed class ClientMetadataDocuments(HttpClient http)
{
    /// <summary>The most bytes of a document that the client reads.</summary>
    public const int MaxBytes = 64 * 1024;

    /// <summary>How long the whole fetch may take.</summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets <paramref name="url"/> once.
    /// </summary>
    /// <returns>
    /// The document as a JSON object, or the reason that it could not be fetched, was not a success or was not a
    /// JSON object.
    /// </returns>
    public async Task<Fetched> FetchAsync(Uri url, CancellationToken cancellationToken)
    {
        try
        {
            // Read whole by SendAsync, as HttpClient's size limit applies to a body that it buffers.
            using var response = await http.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return Fetched.Refused($"The server answered {(int)response.StatusCode}, not a success.");
            }

            await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
            return await JsonNode.ParseAsync(body, cancellationToken: cancellationToken) is JsonObject document
                ? new(document, Refusal: null)
                : Fetched.Refused("The document is not a JSON object.");
        }
        catch (HttpRequestException problem)
        {
            return Fetched.Refused($"The document could not be fetched: {problem.Message}");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Fetched.Refused($"The document could not be fetched: it took over {Timeout.TotalSeconds} seconds.");
        }
        catch (JsonException)
        {
            return Fetched.Refused("The document is not JSON.");
        }
    }

    /// <summary>What <see cref="FetchAsync"/> found: a document, or why there is none.</summary>
    public sealed record Fetched(JsonObject? Document, string? Refusal)
    {
        public static Fetched Refused(string reason) => new(Document: null, reason);
    }
}
