using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Serilog.Sinks.SignalYard.Tests;

/// <summary>
/// A test <see cref="HttpMessageHandler"/> that records every request it receives and
/// returns a configurable response.
/// </summary>
internal sealed class CapturingHandler : HttpMessageHandler
{
    private readonly HttpStatusCode _statusCode;

    public CapturingHandler(HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        _statusCode = statusCode;
    }

    public ConcurrentQueue<CapturedRequest> Requests { get; } = new();

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null
            ? string.Empty
            : await request.Content.ReadAsStringAsync().ConfigureAwait(false);

        request.Headers.TryGetValues("X-Api-Key", out var apiKeys);
        var apiKey = apiKeys is null ? null : string.Join(",", apiKeys);

        Requests.Enqueue(new CapturedRequest(
            request.RequestUri!,
            body,
            apiKey,
            request.Content?.Headers.ContentType?.MediaType));

        return new HttpResponseMessage(_statusCode)
        {
            Content = new StringContent("{\"ingested\":1,\"failed\":0}"),
        };
    }
}

internal sealed record CapturedRequest(
    System.Uri Uri,
    string Body,
    string? ApiKey,
    string? ContentType);
