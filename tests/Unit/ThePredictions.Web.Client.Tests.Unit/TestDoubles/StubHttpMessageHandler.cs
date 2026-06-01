using System.Net;
using System.Net.Http.Json;

namespace ThePredictions.Web.Client.Tests.Unit.TestDoubles;

/// <summary>
/// Records outgoing requests and returns scripted responses in order, falling
/// back to <see cref="FallbackStatus"/> once the queue is empty.
/// </summary>
public sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<HttpResponseMessage> _responses = new();

    public List<RecordedRequest> Requests { get; } = [];
    public int SendCount => Requests.Count;
    public HttpStatusCode FallbackStatus { get; set; } = HttpStatusCode.OK;

    /// <summary>Optional delay so concurrent callers overlap (for coalescing tests).</summary>
    public int DelayMs { get; set; }

    public StubHttpMessageHandler EnqueueStatus(HttpStatusCode status)
    {
        _responses.Enqueue(new HttpResponseMessage(status));
        return this;
    }

    public StubHttpMessageHandler EnqueueJson(HttpStatusCode status, object body)
    {
        _responses.Enqueue(new HttpResponseMessage(status) { Content = JsonContent.Create(body) });
        return this;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(new RecordedRequest(
            request.Method,
            request.RequestUri,
            request.Headers.Authorization?.Parameter,
            request.Content is not null));

        if (DelayMs > 0)
            await Task.Delay(DelayMs, cancellationToken);

        return _responses.Count > 0
            ? _responses.Dequeue()
            : new HttpResponseMessage(FallbackStatus);
    }
}

public sealed record RecordedRequest(HttpMethod Method, Uri? Uri, string? BearerToken, bool HasContent);
