using System;
using Serilog;
using Serilog.Debugging;
using Serilog.Sinks.SignalYard;

// ---------------------------------------------------------------------------
// Minimal end-to-end harness for the SignalYard Serilog sink.
//
// Configure via environment variables or command-line args:
//   SIGNALYARD_URL     e.g. https://your-signalyard.azurewebsites.net  (or http://localhost:5000)
//   SIGNALYARD_APIKEY  the per-application ingestion key (starts with sy_)
//
// Run:
//   dotnet run --project samples/Serilog.Sinks.SignalYard.Sample -- <serverUrl> <apiKey>
//   -- or --
//   $env:SIGNALYARD_URL="..."; $env:SIGNALYARD_APIKEY="sy_..."; dotnet run --project samples/...
// ---------------------------------------------------------------------------

var serverUrl = args.ElementAtOrDefault(0) ?? Environment.GetEnvironmentVariable("SIGNALYARD_URL");
var apiKey = args.ElementAtOrDefault(1) ?? Environment.GetEnvironmentVariable("SIGNALYARD_APIKEY");

if (string.IsNullOrWhiteSpace(serverUrl) || string.IsNullOrWhiteSpace(apiKey))
{
    Console.Error.WriteLine("Usage: provide SignalYard server URL and API key.");
    Console.Error.WriteLine("  dotnet run -- <serverUrl> <apiKey>");
    Console.Error.WriteLine("  or set SIGNALYARD_URL and SIGNALYARD_APIKEY environment variables.");
    return 1;
}

// Surface any sink delivery failures (bad key, wrong URL, non-2xx) to the console.
SelfLog.Enable(Console.Error);

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .Enrich.WithProperty("Sample", "Serilog.Sinks.SignalYard")
    .WriteTo.Console()
    .WriteTo.SignalYard(serverUrl!, apiKey!)
    .CreateLogger();

try
{
    Log.Information("Sample harness started against {ServerUrl}", serverUrl);
    Log.Debug("A debug line with a value: {Answer}", 42);
    Log.Information("User {Username} logged in from {IpAddress}", "john.doe", "10.0.0.1");
    Log.Warning("Cache miss for key {CacheKey} (hit ratio {HitRatio:P1})", "user:42", 0.87);

    try
    {
        throw new InvalidOperationException("Simulated failure for the sink test");
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Order {OrderId} failed to process", 12345);
    }

    Log.Information("Sample harness finished; flushing...");
}
finally
{
    // CRITICAL: flush batched events before the process exits, or they are lost.
    Log.CloseAndFlush();
}

Console.WriteLine("Done. Check the SignalYard UI (or query via MCP) for the events above.");
return 0;
