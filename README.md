# Serilog.Sinks.SignalYard

A [Serilog](https://serilog.net) sink that ships log events to a [SignalYard](https://github.com/signalyard) server.

Events are formatted as [CLEF](https://clef-json.org) (Compact Log Event Format), buffered with
durable periodic batching, and delivered over HTTP to the SignalYard ingestion endpoint using an
`X-Api-Key` header. Message templates, structured properties, log levels, and exceptions are all
preserved.

## Install

```bash
dotnet add package Serilog.Sinks.SignalYard
```

## Quick start

```csharp
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.SignalYard(
        serverUrl: "https://your-signalyard.azurewebsites.net",
        apiKey: "sy_your_api_key_here")
    .CreateLogger();

Log.Information("User {Username} logged in from {IpAddress}", "john.doe", "10.0.0.1");

Log.CloseAndFlush(); // flush buffered events on shutdown
```

`serverUrl` is the **base URL** of your SignalYard server — the sink appends the ingestion path
(`/api/events/raw`) for you. The `apiKey` is the per-application ingestion key (it starts with
`sy_`) shown when you create an application in SignalYard.

> Always call `Log.CloseAndFlush()` (or dispose the logger) at shutdown so batched events aren't lost.

## Configuration options

The convenience overload accepts the common knobs:

```csharp
.WriteTo.SignalYard(
    serverUrl: "https://your-signalyard.azurewebsites.net",
    apiKey: "sy_your_api_key_here",
    restrictedToMinimumLevel: LogEventLevel.Information,
    batchSizeLimit: 1000,
    period: TimeSpan.FromSeconds(2),
    queueLimit: 100_000)
```

For full control, pass a `SignalYardSinkOptions`:

```csharp
.WriteTo.SignalYard(new SignalYardSinkOptions
{
    ServerUrl = "https://your-signalyard.azurewebsites.net",
    ApiKey = "sy_your_api_key_here",
    BatchSizeLimit = 1000,
    Period = TimeSpan.FromSeconds(2),
    QueueLimit = 100_000,
    EagerlyEmitFirstEvent = true,
    HttpTimeout = TimeSpan.FromSeconds(30),
    RestrictedToMinimumLevel = LogEventLevel.Verbose,
})
```

| Option | Description | Default |
|--------|-------------|---------|
| `ServerUrl` | Base URL of the SignalYard server | *(required)* |
| `ApiKey` | Per-application ingestion key (`sy_...`) | *(required)* |
| `IngestPath` | Ingestion path appended to `ServerUrl` | `api/events/raw` |
| `BatchSizeLimit` | Max events per HTTP request | `1000` |
| `Period` | Time between batch flushes | `2s` |
| `QueueLimit` | Max in-memory events before dropping | `100000` |
| `EagerlyEmitFirstEvent` | Ship the first event without waiting for `Period` | `true` |
| `HttpTimeout` | Per-request HTTP timeout | `30s` |
| `RestrictedToMinimumLevel` | Minimum level passed to the sink | `Verbose` |
| `Formatter` | CLEF formatter override | `CompactJsonFormatter` |

## How it works

Each batch is rendered as newline-delimited CLEF (one JSON object per line) and POSTed to
`{ServerUrl}/api/events/raw` with a `Content-Type: application/json` body and an `X-Api-Key`
header. This is the format SignalYard expects for structured ingestion. Delivery failures are
reported through Serilog's [`SelfLog`](https://github.com/serilog/serilog/wiki/Debugging-and-Diagnostics):

```csharp
Serilog.Debugging.SelfLog.Enable(Console.Error);
```

## Supported frameworks

`netstandard2.0` and `net8.0` — usable from .NET Framework 4.6.1+, .NET Core, and modern .NET.

## Building from source

```bash
dotnet build -c Release
dotnet test
dotnet pack src/Serilog.Sinks.SignalYard/Serilog.Sinks.SignalYard.csproj -c Release
```

## License

[MIT](LICENSE)
