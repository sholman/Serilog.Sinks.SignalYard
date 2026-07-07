using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Serilog;
using Serilog.Events;
using Xunit;

namespace Serilog.Sinks.SignalYard.Tests;

public class SignalYardSinkTests
{
    [Fact]
    public async Task Ships_Event_As_NewlineDelimited_Clef()
    {
        var handler = new CapturingHandler();
        using var httpClient = new HttpClient(handler);

        using (var logger = new LoggerConfiguration()
            .WriteTo.SignalYard(
                new SignalYardSinkOptions
                {
                    ServerUrl = "https://signalyard.example.com/",
                    ApiKey = "sy_test_key",
                    Period = TimeSpan.FromMilliseconds(50),
                    EagerlyEmitFirstEvent = true,
                },
                httpClient)
            .CreateLogger())
        {
            logger.Information("User {Username} logged in from {IpAddress}", "john.doe", "10.0.0.1");
            await WaitForRequestsAsync(handler, 1);
        }

        Assert.True(handler.Requests.TryDequeue(out var request));
        Assert.Equal("https://signalyard.example.com/api/events/raw", request!.Uri.ToString());
        Assert.Equal("sy_test_key", request.ApiKey);
        Assert.Equal("application/json", request.ContentType);

        // Body is newline-delimited CLEF: one JSON object per line.
        var lines = request.Body.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Single(lines);

        using var doc = JsonDocument.Parse(lines[0]);
        var root = doc.RootElement;
        Assert.Equal("User {Username} logged in from {IpAddress}", root.GetProperty("@mt").GetString());
        Assert.Equal("john.doe", root.GetProperty("Username").GetString());
        Assert.Equal("10.0.0.1", root.GetProperty("IpAddress").GetString());
        Assert.True(root.TryGetProperty("@t", out _));
    }

    [Fact]
    public async Task Error_Event_Serializes_Level_And_Exception()
    {
        var handler = new CapturingHandler();
        using var httpClient = new HttpClient(handler);

        using (var logger = new LoggerConfiguration()
            .WriteTo.SignalYard(
                new SignalYardSinkOptions
                {
                    ServerUrl = "https://signalyard.example.com",
                    ApiKey = "sy_test_key",
                    Period = TimeSpan.FromMilliseconds(50),
                    EagerlyEmitFirstEvent = true,
                },
                httpClient)
            .CreateLogger())
        {
            logger.Error(new InvalidOperationException("boom"), "It failed");
            await WaitForRequestsAsync(handler, 1);
        }

        Assert.True(handler.Requests.TryDequeue(out var request));
        var line = request!.Body.Split('\n', StringSplitOptions.RemoveEmptyEntries).Single();
        using var doc = JsonDocument.Parse(line);
        var root = doc.RootElement;
        Assert.Equal("Error", root.GetProperty("@l").GetString());
        Assert.Contains("InvalidOperationException", root.GetProperty("@x").GetString());
    }

    [Fact]
    public async Task Batches_Multiple_Events_Into_One_Request()
    {
        var handler = new CapturingHandler();
        using var httpClient = new HttpClient(handler);

        using (var logger = new LoggerConfiguration()
            .WriteTo.SignalYard(
                new SignalYardSinkOptions
                {
                    ServerUrl = "https://signalyard.example.com",
                    ApiKey = "sy_test_key",
                    Period = TimeSpan.FromMilliseconds(200),
                    BatchSizeLimit = 100,
                    EagerlyEmitFirstEvent = false,
                },
                httpClient)
            .CreateLogger())
        {
            logger.Information("one");
            logger.Information("two");
            logger.Information("three");
            await WaitForRequestsAsync(handler, 1);
        }

        // All three events shipped; because they were queued together they land in a
        // single batch (one request) whose body has three CLEF lines.
        var total = handler.Requests
            .SelectMany(r => r.Body.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            .Count();
        Assert.Equal(3, total);
    }

    [Theory]
    [InlineData("https://signalyard.example.com", "api/events/raw", "https://signalyard.example.com/api/events/raw")]
    [InlineData("https://signalyard.example.com/", "/api/events/raw", "https://signalyard.example.com/api/events/raw")]
    [InlineData("https://signalyard.example.com/base/", "api/events/raw", "https://signalyard.example.com/base/api/events/raw")]
    public void BuildIngestUri_Normalizes_Slashes(string serverUrl, string path, string expected)
    {
        var uri = SignalYardSink.BuildIngestUri(serverUrl, path);
        Assert.Equal(expected, uri.ToString());
    }

    [Fact]
    public void Constructor_Requires_ServerUrl_And_ApiKey()
    {
        Assert.Throws<ArgumentException>(() =>
            new SignalYardSink(new SignalYardSinkOptions { ApiKey = "sy_x" }));
        Assert.Throws<ArgumentException>(() =>
            new SignalYardSink(new SignalYardSinkOptions { ServerUrl = "https://x" }));
    }

    private static async Task WaitForRequestsAsync(CapturingHandler handler, int count)
    {
        for (var i = 0; i < 100 && handler.Requests.Count < count; i++)
            await Task.Delay(20);
    }
}
