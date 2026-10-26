using System.Collections.Concurrent;

namespace Grow2Notes.Tests.Fixtures;

/// <summary>
/// Stands in for the network under an <see cref="HttpClient"/>: it records each request and answers it from the next of
/// the responses it was given, so nothing leaves the process. With none given it fails on any request, which is how a
/// test proves that a command makes none. A response is made when it is asked for, so that each request gets a message
/// of its own.
/// </summary>
internal sealed class RecordingHttpHandler(params Func<HttpResponseMessage>[] responses) : HttpMessageHandler
{
    private readonly ConcurrentQueue<Func<HttpResponseMessage>> pending = new(responses);
    private readonly ConcurrentQueue<Request> requests = new();

    /// <summary>Every request received so far, in order.</summary>
    public IReadOnlyCollection<Request> Requests => requests;

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        requests.Enqueue(new(request.Method, request.RequestUri!));
        return pending.TryDequeue(out var response)
            ? Task.FromResult(response())
            : throw new InvalidOperationException($"No request was expected: {request.Method} {request.RequestUri}.");
    }

    /// <summary>A request that the handler received.</summary>
    public sealed record Request(HttpMethod Method, Uri Url);
}
