using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MetricBreak.Domain.Recordings;
using MetricBreak.Ingestion.Csv;

namespace MetricBreak.Api.Tests.Sessions;

public sealed class ContinueEndpointTests
{
    private const string _csvContent =
        "Timestamp,Pressure,Rate\n" +
        "2026-11-08T10:00:00Z,8500,40\n" +
        "2026-11-08T10:00:01Z,8700,42\n" +
        "2026-11-08T10:00:02Z,9500,45\n";

    [Fact]
    public async Task Continue_UnknownSession_Returns404()
    {
        using var factory = new MetricBreakApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsync(
            $"/api/sessions/{Guid.NewGuid()}/continue",
            content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Continue_RemovedSession_Returns404()
    {
        using var factory = new MetricBreakApiFactory();
        using var client = factory.CreateClient();

        var (sessionId, _, _) = CreateSession(factory);

        Assert.True(factory.SessionManager.Remove(sessionId));

        using var response = await client.PostAsync(
            $"/api/sessions/{sessionId}/continue",
            content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Continue_NoActiveBreakpoints_ReturnsExecutedFalse()
    {
        using var factory = new MetricBreakApiFactory();
        using var client = factory.CreateClient();

        var (sessionId, _, _) = CreateSession(factory);

        using var response = await client.PostAsync(
            $"/api/sessions/{sessionId}/continue",
            content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = await ReadJsonAsync(response);
        var root = json.RootElement;

        Assert.False(root.GetProperty("executed").GetBoolean());
        Assert.False(root.GetProperty("hitBreakpoint").GetBoolean());
        Assert.False(root.GetProperty("reachedEndOfRecording").GetBoolean());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("stop").ValueKind);

        var session = root.GetProperty("session");

        Assert.Equal("Ready", session.GetProperty("executionState").GetString());
        Assert.Equal("None", session.GetProperty("pauseReason").GetString());
        Assert.Equal(
            0,
            session.GetProperty("currentSample")
                .GetProperty("sequence")
                .GetInt64());
    }

    [Fact]
    public async Task Continue_BreakpointHit_ReturnsStopDetails()
    {
        using var factory = new MetricBreakApiFactory();
        using var client = factory.CreateClient();

        var (sessionId, pressureChannelId, _) = CreateSession(factory);

        using (var createBreakpointResponse = await client.PostAsJsonAsync(
                   $"/api/sessions/{sessionId}/breakpoints",
                   new
                   {
                       channelId = pressureChannelId,
                       @operator = "GreaterThan",
                       threshold = 9000d
                   }))
        {
            Assert.Equal(HttpStatusCode.Created, createBreakpointResponse.StatusCode);
        }

        using var response = await client.PostAsync(
            $"/api/sessions/{sessionId}/continue",
            content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = await ReadJsonAsync(response);
        var root = json.RootElement;

        Assert.True(root.GetProperty("executed").GetBoolean());
        Assert.True(root.GetProperty("hitBreakpoint").GetBoolean());
        Assert.False(root.GetProperty("reachedEndOfRecording").GetBoolean());

        var stop = root.GetProperty("stop");

        Assert.Equal(2, stop.GetProperty("sequence").GetInt64());

        var hits = stop.GetProperty("hits")
            .EnumerateArray()
            .ToArray();

        Assert.Single(hits);

        var hit = hits[0];

        Assert.Equal(pressureChannelId, hit.GetProperty("channelId").GetGuid());
        Assert.Equal("GreaterThan", hit.GetProperty("operator").GetString());
        Assert.Equal(9500d, hit.GetProperty("observedValue").GetDouble());
        Assert.Equal(9000d, hit.GetProperty("threshold").GetDouble());

        var session = root.GetProperty("session");

        Assert.Equal("Paused", session.GetProperty("executionState").GetString());
        Assert.Equal("Breakpoint", session.GetProperty("pauseReason").GetString());
        Assert.Equal(
            2,
            session.GetProperty("currentSample")
                .GetProperty("sequence")
                .GetInt64());
    }

    [Fact]
    public async Task Continue_NoHit_ReachesEndOfRecording()
    {
        using var factory = new MetricBreakApiFactory();
        using var client = factory.CreateClient();

        var (sessionId, pressureChannelId, _) = CreateSession(factory);

        using (var createBreakpointResponse = await client.PostAsJsonAsync(
                   $"/api/sessions/{sessionId}/breakpoints",
                   new
                   {
                       channelId = pressureChannelId,
                       @operator = "GreaterThan",
                       threshold = 99999d
                   }))
        {
            Assert.Equal(HttpStatusCode.Created, createBreakpointResponse.StatusCode);
        }

        using var response = await client.PostAsync(
            $"/api/sessions/{sessionId}/continue",
            content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = await ReadJsonAsync(response);
        var root = json.RootElement;

        Assert.True(root.GetProperty("executed").GetBoolean());
        Assert.False(root.GetProperty("hitBreakpoint").GetBoolean());
        Assert.True(root.GetProperty("reachedEndOfRecording").GetBoolean());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("stop").ValueKind);

        var session = root.GetProperty("session");

        Assert.Equal("Completed", session.GetProperty("executionState").GetString());
        Assert.Equal("EndOfRecording", session.GetProperty("pauseReason").GetString());
        Assert.Equal(
            2,
            session.GetProperty("currentSample")
                .GetProperty("sequence")
                .GetInt64());
    }

    [Fact]
    public async Task ContinueBackward_UnknownSession_Returns404()
    {
        using var factory = new MetricBreakApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsync(
            $"/api/sessions/{Guid.NewGuid()}/continue-backward",
            content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ContinueBackward_NoActiveBreakpoints_ReturnsExecutedFalse()
    {
        using var factory = new MetricBreakApiFactory();
        using var client = factory.CreateClient();

        var (sessionId, _, _) = CreateSession(factory);

        using (var seekResponse = await client.PostAsJsonAsync(
                   $"/api/sessions/{sessionId}/seek",
                   new { sequenceNumber = 2L }))
        {
            Assert.Equal(HttpStatusCode.OK, seekResponse.StatusCode);
        }

        using var response = await client.PostAsync(
            $"/api/sessions/{sessionId}/continue-backward",
            content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = await ReadJsonAsync(response);
        var root = json.RootElement;

        Assert.False(root.GetProperty("executed").GetBoolean());
        Assert.False(root.GetProperty("hitBreakpoint").GetBoolean());
        Assert.False(root.GetProperty("reachedEndOfRecording").GetBoolean());
        Assert.False(root.GetProperty("reachedStartOfRecording").GetBoolean());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("stop").ValueKind);
    }

    [Fact]
    public async Task ContinueBackward_BreakpointHit_ReturnsStopDetails()
    {
        using var factory = new MetricBreakApiFactory();
        using var client = factory.CreateClient();

        var (sessionId, pressureChannelId, _) = CreateSession(factory);

        using (var seekResponse = await client.PostAsJsonAsync(
                   $"/api/sessions/{sessionId}/seek",
                   new { sequenceNumber = 2L }))
        {
            Assert.Equal(HttpStatusCode.OK, seekResponse.StatusCode);
        }

        using (var createBreakpointResponse = await client.PostAsJsonAsync(
                   $"/api/sessions/{sessionId}/breakpoints",
                   new
                   {
                       channelId = pressureChannelId,
                       @operator = "LessThan",
                       threshold = 8600d
                   }))
        {
            Assert.Equal(HttpStatusCode.Created, createBreakpointResponse.StatusCode);
        }

        using var response = await client.PostAsync(
            $"/api/sessions/{sessionId}/continue-backward",
            content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = await ReadJsonAsync(response);
        var root = json.RootElement;

        Assert.True(root.GetProperty("executed").GetBoolean());
        Assert.True(root.GetProperty("hitBreakpoint").GetBoolean());
        Assert.False(root.GetProperty("reachedStartOfRecording").GetBoolean());

        var stop = root.GetProperty("stop");

        Assert.Equal(0, stop.GetProperty("sequence").GetInt64());

        var session = root.GetProperty("session");

        Assert.Equal("Paused", session.GetProperty("executionState").GetString());
        Assert.Equal("Breakpoint", session.GetProperty("pauseReason").GetString());
    }

    [Fact]
    public async Task ContinueBackward_NoHit_ReachesStartOfRecording()
    {
        using var factory = new MetricBreakApiFactory();
        using var client = factory.CreateClient();

        var (sessionId, pressureChannelId, _) = CreateSession(factory);

        using (var seekResponse = await client.PostAsJsonAsync(
                   $"/api/sessions/{sessionId}/seek",
                   new { sequenceNumber = 2L }))
        {
            Assert.Equal(HttpStatusCode.OK, seekResponse.StatusCode);
        }

        using (var createBreakpointResponse = await client.PostAsJsonAsync(
                   $"/api/sessions/{sessionId}/breakpoints",
                   new
                   {
                       channelId = pressureChannelId,
                       @operator = "GreaterThan",
                       threshold = 99999d
                   }))
        {
            Assert.Equal(HttpStatusCode.Created, createBreakpointResponse.StatusCode);
        }

        using var response = await client.PostAsync(
            $"/api/sessions/{sessionId}/continue-backward",
            content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = await ReadJsonAsync(response);
        var root = json.RootElement;

        Assert.True(root.GetProperty("executed").GetBoolean());
        Assert.False(root.GetProperty("hitBreakpoint").GetBoolean());
        Assert.True(root.GetProperty("reachedStartOfRecording").GetBoolean());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("stop").ValueKind);

        var session = root.GetProperty("session");

        Assert.Equal("Completed", session.GetProperty("executionState").GetString());
        Assert.Equal("StartOfRecording", session.GetProperty("pauseReason").GetString());
        Assert.Equal(
            0,
            session.GetProperty("currentSample")
                .GetProperty("sequence")
                .GetInt64());
    }

    private static async Task<JsonDocument> ReadJsonAsync(
        HttpResponseMessage response)
    {
        await using var stream =
            await response.Content.ReadAsStreamAsync();

        return await JsonDocument.ParseAsync(stream);
    }

    private static (Guid SessionId, Guid PressureChannelId, Guid RateChannelId)
        CreateSession(MetricBreakApiFactory factory)
    {
        var parser = new CsvTelemetryParser();

        using var reader = new StringReader(_csvContent);

        var parsed = parser.Parse(reader);

        var recordingId = Guid.NewGuid();

        var recording = new Recording(
            recordingId,
            "Continue HTTP Test",
            new RecordingSource(
                "test.csv",
                "test-hash",
                _csvContent.Length),
            DateTimeOffset.UtcNow);

        var telemetryReader = new CsvTelemetryReader(
            recordingId,
            parsed);

        var imported = new ImportedCsvRecording(
            recording,
            parsed.Channels,
            telemetryReader);

        var sessionId = factory.SessionManager.Create(imported);

        return (
            sessionId,
            parsed.Channels[0].Id,
            parsed.Channels[1].Id);
    }
}
