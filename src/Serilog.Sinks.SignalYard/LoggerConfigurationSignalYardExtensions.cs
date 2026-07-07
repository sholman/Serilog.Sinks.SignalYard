using System;
using System.Net.Http;
using Serilog.Configuration;
using Serilog.Events;
using Serilog.Sinks.PeriodicBatching;

namespace Serilog.Sinks.SignalYard;

/// <summary>
/// Adds the <c>WriteTo.SignalYard(...)</c> configuration methods to Serilog.
/// </summary>
public static class LoggerConfigurationSignalYardExtensions
{
    /// <summary>
    /// Writes log events to a SignalYard server as batched, newline-delimited CLEF over HTTP.
    /// </summary>
    /// <param name="loggerSinkConfiguration">The <c>WriteTo</c> configuration object.</param>
    /// <param name="serverUrl">
    /// The base URL of the SignalYard server, e.g. <c>https://your-signalyard.azurewebsites.net</c>.
    /// </param>
    /// <param name="apiKey">The per-application ingestion API key (starts with <c>sy_</c>).</param>
    /// <param name="restrictedToMinimumLevel">The minimum level for events passed to the sink.</param>
    /// <param name="batchSizeLimit">Maximum number of events shipped in a single batch.</param>
    /// <param name="period">Time to wait between checking for event batches.</param>
    /// <param name="queueLimit">Maximum number of events queued in memory before dropping.</param>
    /// <returns>The logger configuration, to allow chaining.</returns>
    public static LoggerConfiguration SignalYard(
        this LoggerSinkConfiguration loggerSinkConfiguration,
        string serverUrl,
        string apiKey,
        LogEventLevel restrictedToMinimumLevel = LevelAlias.Minimum,
        int batchSizeLimit = 1000,
        TimeSpan? period = null,
        int queueLimit = 100_000)
    {
        var options = new SignalYardSinkOptions
        {
            ServerUrl = serverUrl,
            ApiKey = apiKey,
            RestrictedToMinimumLevel = restrictedToMinimumLevel,
            BatchSizeLimit = batchSizeLimit,
            Period = period ?? TimeSpan.FromSeconds(2),
            QueueLimit = queueLimit,
        };

        return loggerSinkConfiguration.SignalYard(options);
    }

    /// <summary>
    /// Writes log events to a SignalYard server using a fully-configured
    /// <see cref="SignalYardSinkOptions"/> instance.
    /// </summary>
    /// <param name="loggerSinkConfiguration">The <c>WriteTo</c> configuration object.</param>
    /// <param name="options">The sink configuration.</param>
    /// <param name="httpClient">
    /// An optional caller-managed <see cref="HttpClient"/>. When supplied, the caller retains
    /// ownership and is responsible for its disposal. When <c>null</c>, the sink creates and
    /// owns one. Intended for testing and custom <see cref="HttpMessageHandler"/> pipelines.
    /// </param>
    /// <returns>The logger configuration, to allow chaining.</returns>
    public static LoggerConfiguration SignalYard(
        this LoggerSinkConfiguration loggerSinkConfiguration,
        SignalYardSinkOptions options,
        HttpClient? httpClient = null)
    {
        if (loggerSinkConfiguration is null) throw new ArgumentNullException(nameof(loggerSinkConfiguration));
        if (options is null) throw new ArgumentNullException(nameof(options));

        var signalYardSink = new SignalYardSink(options, httpClient);

        var batchingOptions = new PeriodicBatchingSinkOptions
        {
            BatchSizeLimit = options.BatchSizeLimit,
            Period = options.Period,
            QueueLimit = options.QueueLimit,
            EagerlyEmitFirstEvent = options.EagerlyEmitFirstEvent,
        };

        // PeriodicBatchingSink owns the SignalYardSink and disposes it (releasing an owned
        // HttpClient) when the logger is disposed.
        var batchingSink = new PeriodicBatchingSink(signalYardSink, batchingOptions);

        return loggerSinkConfiguration.Sink(batchingSink, options.RestrictedToMinimumLevel);
    }
}
