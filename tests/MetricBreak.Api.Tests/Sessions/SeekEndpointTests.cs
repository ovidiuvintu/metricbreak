
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MetricBreak.Domain.Recordings;
using MetricBreak.Ingestion.Csv;

namespace MetricBreak.Api.Tests.Sessions;

public sealed class SeekEndpointTests
{
    private const string CsvContent =
        "Timestamp,Pressure,Rate\n" +
        "2026-11-08T10:00:00Z,8500,40\n" +
        "2026-11-08T10:00:01Z,8700,42\n" +
        "2026-11-08T10:00:02Z,9500,45\n";

    [Fact]
    public async Task Seek_ValidSequence_Returns200()
    {
        using var factory = new MetricBreakApiFactory();
        using var client = factory.CreateClient();

        var sessionId = CreateSession(factory);

        using var response = await SeekAsync(client, sessionId, 2);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = await ReadJsonAsync(response);

        var session = GetSession(json);

        Assert.Equal(
            sessionId.ToString(),
            session.GetProperty("sessionId").GetString());

        Assert.Equal(
            3,
            session.GetProperty("sampleCount").GetInt64());

        Assert.Equal(
            "Paused",
            session.GetProperty("executionState").GetString());

        Assert.Equal(
            "User",
            session.GetProperty("pauseReason").GetString());

        Assert.Equal(2, GetSequence(session));
    }

    [Fact]
    public async Task Seek_SequenceZero_Returns200()
    {
        using var factory = new MetricBreakApiFactory();
        using var client = factory.CreateClient();

        var sessionId = CreateSession(factory);

        using (var first = await SeekAsync(client, sessionId, 2))
        {
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        }

        using var response = await SeekAsync(client, sessionId, 0);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = await ReadJsonAsync(response);

        var session = GetSession(json);

        Assert.Equal(0, GetSequence(session));

        Assert.Equal(
            "Paused",
            session.GetProperty("executionState").GetString());
    }

    [Fact]
    public async Task Seek_NegativeSequence_Returns400()
    {
        using var factory = new MetricBreakApiFactory();
        using var client = factory.CreateClient();

        var sessionId = CreateSession(factory);

        using var response = await SeekAsync(client, sessionId, -1);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);

        await AssertPositionAsync(
            client,
            sessionId,
            expectedSequence: 0);
    }

    [Fact]
    public async Task Seek_SequenceBeyondRecording_Returns400()
    {
        using var factory = new MetricBreakApiFactory();
        using var client = factory.CreateClient();

        var sessionId = CreateSession(factory);

        using (var first = await SeekAsync(client, sessionId, 1))
        {
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        }

        using var response = await SeekAsync(client, sessionId, 3);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);

        await AssertPositionAsync(
            client,
            sessionId,
            expectedSequence: 1);
    }

    [Fact]
    public async Task Seek_UnknownSession_Returns404()
    {
        using var factory = new MetricBreakApiFactory();
        using var client = factory.CreateClient();

        using var response = await SeekAsync(
            client,
            Guid.NewGuid(),
            1);

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }

    [Fact]
    public async Task Seek_RemovedSession_Returns404()
    {
        using var factory = new MetricBreakApiFactory();
        using var client = factory.CreateClient();

        var sessionId = CreateSession(factory);

        Assert.True(factory.SessionManager.Remove(sessionId));

        using var response = await SeekAsync(
            client,
            sessionId,
            1);

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }

    [Fact]
    public async Task Seek_ReturnsCorrectTelemetrySample()
    {
        using var factory = new MetricBreakApiFactory();
        using var client = factory.CreateClient();

        var sessionId = CreateSession(factory);

        using var response = await SeekAsync(client, sessionId, 2);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = await ReadJsonAsync(response);

        var session = GetSession(json);

        var sample = session.GetProperty("currentSample");

        Assert.Equal(
            2,
            sample.GetProperty("sequence").GetInt64());

        Assert.Equal(
            1794132002000000000L,
            sample.GetProperty("timestampNanoseconds").GetInt64());

        var channels = sample.GetProperty("channels");

        var values = channels.EnumerateObject()
            .Select(property =>
                property.Value.GetDouble())
            .ToArray();

        Assert.Equal(2, values.Length);
        Assert.Contains(9500.0, values);
        Assert.Contains(45.0, values);
    }

    [Fact]
    public async Task Seek_InvalidPosition_PreservesExecutionState()
    {
        using var factory = new MetricBreakApiFactory();
        using var client = factory.CreateClient();

        var sessionId = CreateSession(factory);

        using var response = await SeekAsync(client, sessionId, 100);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);

        using var getResponse = await client.GetAsync(
            $"/api/sessions/{sessionId}");

        Assert.Equal(
            HttpStatusCode.OK,
            getResponse.StatusCode);

        using var json = await ReadJsonAsync(getResponse);

        var session = json.RootElement;

        Assert.Equal(0, GetSequence(session));

        Assert.Equal(
            "Ready",
            session.GetProperty("executionState").GetString());

        Assert.Equal(
            "None",
            session.GetProperty("pauseReason").GetString());
    }

    private static async Task<HttpResponseMessage> SeekAsync(
        HttpClient client,
        Guid sessionId,
        long sequenceNumber)
    {
        return await client.PostAsJsonAsync(
            $"/api/sessions/{sessionId}/seek",
            new { sequenceNumber });
    }

    private static async Task<JsonDocument> ReadJsonAsync(
        HttpResponseMessage response)
    {
        await using var stream =
            await response.Content.ReadAsStreamAsync();

        return await JsonDocument.ParseAsync(stream);
    }

    private static JsonElement GetSession(JsonDocument json)
    {
        return json.RootElement.GetProperty("session");
    }

    private static long GetSequence(JsonElement session)
    {
        return session
            .GetProperty("currentSample")
            .GetProperty("sequence")
            .GetInt64();
    }

    private static async Task AssertPositionAsync(
        HttpClient client,
        Guid sessionId,
        long expectedSequence)
    {
        using var response = await client.GetAsync(
            $"/api/sessions/{sessionId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = await ReadJsonAsync(response);

        Assert.Equal(
            expectedSequence,
            GetSequence(json.RootElement));
    }

    private static Guid CreateSession(
        MetricBreakApiFactory factory)
    {
        var parser = new CsvTelemetryParser();

        using var reader = new StringReader(CsvContent);

        var parsed = parser.Parse(reader);

        var recordingId = Guid.NewGuid();

        var recording = new Recording(
            recordingId,
            "Seek HTTP Test",
            new RecordingSource(
                "test.csv",
                "test-hash",
                CsvContent.Length),
            DateTimeOffset.UtcNow);

        var telemetryReader = new CsvTelemetryReader(
            recordingId,
            parsed);

        var imported = new ImportedCsvRecording(
            recording,
            parsed.Channels,
            telemetryReader);

        return factory.SessionManager.Create(imported);
    }
}

