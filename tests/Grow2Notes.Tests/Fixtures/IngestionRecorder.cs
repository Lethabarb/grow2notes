using System.Collections.Concurrent;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace Grow2Notes.Tests.Fixtures;

/// <summary>
/// Stands in for Application Insights' ingestion endpoint as the Azure Monitor exporter's HTTP transport: it keeps the
/// body of every request the exporter sends, decompressed, and accepts every item, so nothing leaves the process.
/// </summary>
public sealed class IngestionRecorder : HttpMessageHandler
{
    private readonly ConcurrentQueue<string> payloads = new();

    /// <summary>Each request's body as sent: one JSON telemetry item per line.</summary>
    public IReadOnlyCollection<string> Payloads => payloads;

    /// <summary>Every telemetry item sent.</summary>
    public IEnumerable<JsonNode> Items => payloads.SelectMany(Lines).Select(line => JsonNode.Parse(line)!);

    // The exporter sends synchronously from its export threads, which HttpMessageHandler does not support by default.
    protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var content = request.Content!;
        var body = content.ReadAsStream(cancellationToken);
        if (content.Headers.ContentEncoding.Contains("gzip"))
        {
            body = new GZipStream(body, CompressionMode.Decompress);
        }

        using var reader = new StreamReader(body);
        var payload = reader.ReadToEnd();
        payloads.Enqueue(payload);

        var items = Lines(payload).Length;
        var accepted = new { itemsReceived = items, itemsAccepted = items, errors = Array.Empty<object>() };
        return new(HttpStatusCode.OK) { Content = JsonContent.Create(accepted) };
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(Send(request, cancellationToken));

    private static string[] Lines(string payload) => payload.Split('\n', StringSplitOptions.RemoveEmptyEntries);
}
