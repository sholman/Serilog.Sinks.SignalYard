using System;
using Serilog.Events;
using Serilog.Formatting;

namespace Serilog.Sinks.SignalYard;

/// <summary>
/// Configuration for the SignalYard Serilog sink.
/// </summary>
public class SignalYardSinkOptions
{
    /// <summary>
    /// The relative path of the SignalYard ingestion endpoint that accepts
    /// newline-delimited CLEF events. Appended to <see cref="ServerUrl"/>.
    /// </summary>
    public const string DefaultIngestPath = "api/events/raw";

    /// <summary>
    /// The base URL of the SignalYard server, e.g. <c>https://your-signalyard.azurewebsites.net</c>.
    /// The ingestion path (<see cref="IngestPath"/>) is appended automatically.
    /// </summary>
    public string ServerUrl { get; set; } = string.Empty;

    /// <summary>
    /// The per-application ingestion API key (starts with <c>sy_</c>), sent in the
    /// <c>X-Api-Key</c> header on every request.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// The ingestion path appended to <see cref="ServerUrl"/>. Defaults to
    /// <see cref="DefaultIngestPath"/> and rarely needs changing.
    /// </summary>
    public string IngestPath { get; set; } = DefaultIngestPath;

    /// <summary>
    /// The maximum number of events posted to SignalYard in a single batch. Default: 1000.
    /// </summary>
    public int BatchSizeLimit { get; set; } = 1000;

    /// <summary>
    /// The time to wait between checking for event batches. Default: 2 seconds.
    /// </summary>
    public TimeSpan Period { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// The maximum number of events to hold in the in-memory queue while waiting to be
    /// shipped. Once the limit is reached, additional events are dropped. Default: 100000.
    /// </summary>
    public int QueueLimit { get; set; } = 100_000;

    /// <summary>
    /// If <c>true</c>, batches are shipped eagerly as they fill, reducing latency at the cost
    /// of more frequent HTTP requests. Default: <c>false</c>.
    /// </summary>
    public bool EagerlyEmitFirstEvent { get; set; } = true;

    /// <summary>
    /// The minimum level for events passed through the sink. Default: <see cref="LogEventLevel.Verbose"/>.
    /// </summary>
    public LogEventLevel RestrictedToMinimumLevel { get; set; } = LevelAlias.Minimum;

    /// <summary>
    /// The HTTP request timeout for a single batch POST. Default: 30 seconds.
    /// </summary>
    public TimeSpan HttpTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// The formatter used to render events. Defaults to CLEF
    /// (<see cref="Serilog.Formatting.Compact.CompactJsonFormatter"/>), which SignalYard expects.
    /// Override only if you have customised the SignalYard ingestion pipeline.
    /// </summary>
    public ITextFormatter? Formatter { get; set; }
}
