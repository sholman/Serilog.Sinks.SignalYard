using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Serilog.Debugging;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Formatting.Compact;
using Serilog.Sinks.PeriodicBatching;

namespace Serilog.Sinks.SignalYard;

/// <summary>
/// A batched Serilog sink that ships events to a SignalYard server as newline-delimited
/// CLEF (Compact Log Event Format), authenticated with an <c>X-Api-Key</c> header.
/// </summary>
public sealed class SignalYardSink : IBatchedLogEventSink, IDisposable
{
    private const string ApiKeyHeaderName = "X-Api-Key";

    private readonly ITextFormatter _formatter;
    private readonly Uri _ingestUri;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    /// <summary>
    /// Creates a sink from the supplied options, owning an internally-created
    /// <see cref="HttpClient"/> that is disposed with the sink.
    /// </summary>
    public SignalYardSink(SignalYardSinkOptions options)
        : this(options, httpClient: null)
    {
    }

    /// <summary>
    /// Creates a sink, optionally reusing a caller-supplied <see cref="HttpClient"/>
    /// (which the caller is responsible for disposing). Intended for testing and advanced
    /// scenarios such as custom <see cref="HttpMessageHandler"/> pipelines.
    /// </summary>
    public SignalYardSink(SignalYardSinkOptions options, HttpClient? httpClient)
    {
        if (options is null) throw new ArgumentNullException(nameof(options));
        if (string.IsNullOrWhiteSpace(options.ServerUrl))
            throw new ArgumentException("A SignalYard ServerUrl must be provided.", nameof(options));
        if (string.IsNullOrWhiteSpace(options.ApiKey))
            throw new ArgumentException("A SignalYard ApiKey must be provided.", nameof(options));

        _formatter = options.Formatter ?? new CompactJsonFormatter();
        _ingestUri = BuildIngestUri(options.ServerUrl, options.IngestPath);

        if (httpClient is null)
        {
            _httpClient = new HttpClient { Timeout = options.HttpTimeout };
            _ownsHttpClient = true;
        }
        else
        {
            _httpClient = httpClient;
            _ownsHttpClient = false;
        }

        // Set the API key once as a default header so it is applied to every request,
        // regardless of whether the HttpClient is owned or supplied.
        _httpClient.DefaultRequestHeaders.Remove(ApiKeyHeaderName);
        _httpClient.DefaultRequestHeaders.Add(ApiKeyHeaderName, options.ApiKey);
    }

    internal static Uri BuildIngestUri(string serverUrl, string ingestPath)
    {
        var baseUrl = serverUrl.TrimEnd('/');
        var path = (ingestPath ?? SignalYardSinkOptions.DefaultIngestPath).TrimStart('/');
        return new Uri($"{baseUrl}/{path}", UriKind.Absolute);
    }

    /// <inheritdoc />
    public async Task EmitBatchAsync(IEnumerable<LogEvent> batch)
    {
        if (batch is null)
            return;

        var count = 0;
        var payload = new StringWriter();
        foreach (var logEvent in batch)
        {
            // CompactJsonFormatter writes a single JSON object followed by a newline,
            // producing the newline-delimited CLEF that /api/events/raw expects.
            _formatter.Format(logEvent, payload);
            count++;
        }

        if (count == 0)
            return;

        using var content = new StringContent(payload.ToString(), Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync(_ingestUri, content).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var body = await ReadBodySafeAsync(response).ConfigureAwait(false);
            var message =
                $"SignalYard sink failed to ship {count} event(s) to {_ingestUri}: " +
                $"{(int)response.StatusCode} {response.ReasonPhrase}. {body}";
            SelfLog.WriteLine(message);
            throw new LoggingFailedException(message);
        }
    }

    /// <inheritdoc />
    public Task OnEmptyBatchAsync() =>
#if NET8_0_OR_GREATER
        Task.CompletedTask;
#else
        Task.FromResult(0);
#endif

    private static async Task<string> ReadBodySafeAsync(HttpResponseMessage response)
    {
        try
        {
            return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_ownsHttpClient)
            _httpClient.Dispose();
    }
}

/// <summary>
/// Thrown when the SignalYard sink cannot deliver a batch of events to the server.
/// </summary>
public sealed class LoggingFailedException : Exception
{
    /// <summary>Creates the exception with a descriptive message.</summary>
    public LoggingFailedException(string message) : base(message) { }
}
