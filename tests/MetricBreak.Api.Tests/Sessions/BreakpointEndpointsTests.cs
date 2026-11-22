using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MetricBreak.Domain.Recordings;
using MetricBreak.Ingestion.Csv;

namespace MetricBreak.Api.Tests.Sessions;

public sealed class BreakpointEndpointsTests
{
    private const string _csvContent =
        "Timestamp,Pressure,Rate\n" +
        "2026-11-08T10:00:00Z,8500,40\n" +
        "2026-11-08T10:00:01Z,8700,42\n" +
        "2026-11-08T10:00:02Z,9500,45\n";

    [Fact]
    public async Task CreateBreakpoint_ValidRequest_Returns201()
    {
        using var factory = new MetricBreakApiFactory();
        using var client = factory.CreateClient();

        var (sessionId, pressureChannelId, _) = CreateSession(factory);

        using var response = await client.PostAsJsonAsync(
            $"/api/sessions/{sessionId}/breakpoints",
            new
            {
                channelId = pressureChannelId,
                @operator = "GreaterThan",
                threshold = 9000d
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var json = await ReadJsonAsync(response);

        var root = json.RootElement;

        Assert.NotEqual(
            Guid.Empty,
            root.GetProperty("breakpointId").GetGuid());

        Assert.Equal(
            pressureChannelId,
            root.GetProperty("channelId").GetGuid());

        Assert.Equal(
            "GreaterThan",
            root.GetProperty("operator").GetString());

        Assert.Equal(
            9000d,
            root.GetProperty("threshold").GetDouble());
    }

    [Fact]
    public async Task GetBreakpoints_AfterCreate_Returns200WithItems()
    {
        using var factory = new MetricBreakApiFactory();
        using var client = factory.CreateClient();

        var (sessionId, pressureChannelId, rateChannelId) =
            CreateSession(factory);

        using (var first = await client.PostAsJsonAsync(
                   $"/api/sessions/{sessionId}/breakpoints",
                   new
                   {
                       channelId = pressureChannelId,
                       @operator = "GreaterThan",
                       threshold = 9000d
                   }))
        {
            Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        }

        using (var second = await client.PostAsJsonAsync(
                   $"/api/sessions/{sessionId}/breakpoints",
                   new
                   {
                       channelId = rateChannelId,
                       @operator = "LessThan",
                       threshold = 50d
                   }))
        {
            Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        }

        using var response = await client.GetAsync(
            $"/api/sessions/{sessionId}/breakpoints");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = await ReadJsonAsync(response);

        var breakpoints = json.RootElement
            .GetProperty("breakpoints")
            .EnumerateArray()
            .ToArray();

        Assert.Equal(2, breakpoints.Length);
    }

    [Fact]
    public async Task CreateBreakpoint_UnknownSession_Returns404()
    {
        using var factory = new MetricBreakApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            $"/api/sessions/{Guid.NewGuid()}/breakpoints",
            new
            {
                channelId = Guid.NewGuid(),
                @operator = "GreaterThan",
                threshold = 9000d
            });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateBreakpoint_UnknownChannel_Returns400()
    {
        using var factory = new MetricBreakApiFactory();
        using var client = factory.CreateClient();

        var (sessionId, _, _) = CreateSession(factory);

        using var response = await client.PostAsJsonAsync(
            $"/api/sessions/{sessionId}/breakpoints",
            new
            {
                channelId = Guid.NewGuid(),
                @operator = "GreaterThan",
                threshold = 9000d
            });

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    [Fact]
    public async Task CreateBreakpoint_InvalidOperator_Returns400()
    {
        using var factory = new MetricBreakApiFactory();
        using var client = factory.CreateClient();

        var (sessionId, pressureChannelId, _) = CreateSession(factory);

        using var response = await client.PostAsJsonAsync(
            $"/api/sessions/{sessionId}/breakpoints",
            new
            {
                channelId = pressureChannelId,
                @operator = "Nope",
                threshold = 9000d
            });

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    [Fact]
    public async Task DeleteBreakpoint_Existing_Returns204AndRemoves()
    {
        using var factory = new MetricBreakApiFactory();
        using var client = factory.CreateClient();

        var (sessionId, pressureChannelId, _) = CreateSession(factory);

        Guid breakpointId;

        using (var createResponse = await client.PostAsJsonAsync(
                   $"/api/sessions/{sessionId}/breakpoints",
                   new
                   {
                       channelId = pressureChannelId,
                       @operator = "GreaterThan",
                       threshold = 9000d
                   }))
        {
            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

            using var json = await ReadJsonAsync(createResponse);
            breakpointId = json.RootElement
                .GetProperty("breakpointId")
                .GetGuid();
        }

        using (var deleteResponse = await client.DeleteAsync(
                   $"/api/sessions/{sessionId}/breakpoints/{breakpointId}"))
        {
            Assert.Equal(
                HttpStatusCode.NoContent,
                deleteResponse.StatusCode);
        }

        using var listResponse = await client.GetAsync(
            $"/api/sessions/{sessionId}/breakpoints");

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        using var listJson = await ReadJsonAsync(listResponse);

        var breakpoints = listJson.RootElement
            .GetProperty("breakpoints")
            .EnumerateArray()
            .ToArray();

        Assert.Empty(breakpoints);
    }


    [Fact]
    public async Task PatchBreakpoint_DisableAndEnable_UpdatesEnabledStateInGet()
    {
        using var factory = new MetricBreakApiFactory();
        using var client = factory.CreateClient();

        var (sessionId, pressureChannelId, _) = CreateSession(factory);

        Guid breakpointId;

        using (var createResponse = await client.PostAsJsonAsync(
                   $"/api/sessions/{sessionId}/breakpoints",
                   new
                   {
                       channelId = pressureChannelId,
                       @operator = "GreaterThan",
                       threshold = 9000d
                   }))
        {
            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

            using var json = await ReadJsonAsync(createResponse);

            breakpointId = json.RootElement
                .GetProperty("breakpointId")
                .GetGuid();

            Assert.True(json.RootElement
                .GetProperty("isEnabled")
                .GetBoolean());
        }

        using (var disableResponse = await client.PatchAsJsonAsync(
                   $"/api/sessions/{sessionId}/breakpoints/{breakpointId}",
                   new { isEnabled = false }))
        {
            Assert.Equal(HttpStatusCode.NoContent, disableResponse.StatusCode);
        }

        using (var listResponse = await client.GetAsync(
                   $"/api/sessions/{sessionId}/breakpoints"))
        {
            Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

            using var listJson = await ReadJsonAsync(listResponse);

            var breakpoints = listJson.RootElement
                .GetProperty("breakpoints")
                .EnumerateArray()
                .ToArray();

            // Disabled breakpoints remain visible.
            var breakpoint = Assert.Single(breakpoints);

            Assert.Equal(
                breakpointId,
                breakpoint.GetProperty("breakpointId").GetGuid());

            Assert.False(
                breakpoint.GetProperty("isEnabled").GetBoolean());
        }

        using (var enableResponse = await client.PatchAsJsonAsync(
                   $"/api/sessions/{sessionId}/breakpoints/{breakpointId}",
                   new { isEnabled = true }))
        {
            Assert.Equal(HttpStatusCode.NoContent, enableResponse.StatusCode);
        }

        using var afterEnableResponse = await client.GetAsync(
            $"/api/sessions/{sessionId}/breakpoints");

        Assert.Equal(HttpStatusCode.OK, afterEnableResponse.StatusCode);

        using var afterEnableJson = await ReadJsonAsync(afterEnableResponse);

        var afterEnableBreakpoints = afterEnableJson.RootElement
            .GetProperty("breakpoints")
            .EnumerateArray()
            .ToArray();

        var enabledBreakpoint = Assert.Single(afterEnableBreakpoints);

        Assert.Equal(
            breakpointId,
            enabledBreakpoint.GetProperty("breakpointId").GetGuid());

        Assert.True(
            enabledBreakpoint.GetProperty("isEnabled").GetBoolean());
    }


    [Fact]
    public async Task PatchBreakpoint_UnknownSession_Returns404()
    {
        using var factory = new MetricBreakApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.PatchAsJsonAsync(
            $"/api/sessions/{Guid.NewGuid()}/breakpoints/{Guid.NewGuid()}",
            new { isEnabled = false });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PatchBreakpoint_UnknownBreakpoint_Returns404()
    {
        using var factory = new MetricBreakApiFactory();
        using var client = factory.CreateClient();

        var (sessionId, _, _) = CreateSession(factory);

        using var response = await client.PatchAsJsonAsync(
            $"/api/sessions/{sessionId}/breakpoints/{Guid.NewGuid()}",
            new { isEnabled = false });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateMissingDataBreakpoint_ThenContinue_StopsOnTimeGap()
    {
        const string csv =
            "Timestamp,Pressure\n" +
            "2026-11-08T10:00:00Z,1\n" +
            "2026-11-08T10:00:01Z,1\n" +
            "2026-11-08T10:00:07Z,1\n" +
            "2026-11-08T10:00:08Z,1\n";

        using var factory = new MetricBreakApiFactory();
        using var client = factory.CreateClient();
        var sessionId = CreateSession(factory, csv, "gap.csv");

        using (var created = await client.PostAsJsonAsync(
                   $"/api/sessions/{sessionId}/breakpoints/missing-data",
                   new { minimumSeconds = 5d }))
        {
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            using var createdJson = await ReadJsonAsync(created);
            Assert.Equal("MissingLongerThan", createdJson.RootElement.GetProperty("operator").GetString());
            Assert.Equal(5d, createdJson.RootElement.GetProperty("threshold").GetDouble());
            Assert.Equal(Guid.Empty, createdJson.RootElement.GetProperty("channelId").GetGuid());
        }

        using var list = await client.GetAsync($"/api/sessions/{sessionId}/breakpoints");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        using var listJson = await ReadJsonAsync(list);
        var listed = Assert.Single(listJson.RootElement.GetProperty("breakpoints").EnumerateArray());
        Assert.Equal("MissingLongerThan", listed.GetProperty("operator").GetString());

        using var continued = await client.PostAsync($"/api/sessions/{sessionId}/continue", content: null);
        Assert.Equal(HttpStatusCode.OK, continued.StatusCode);
        using var continuedJson = await ReadJsonAsync(continued);
        Assert.True(continuedJson.RootElement.GetProperty("hitBreakpoint").GetBoolean());
        Assert.Equal(2, continuedJson.RootElement.GetProperty("stop").GetProperty("sequence").GetInt64());
        Assert.Equal(
            "MissingLongerThan",
            continuedJson.RootElement.GetProperty("stop").GetProperty("hits")[0].GetProperty("operator").GetString());

        using var after = await client.PostAsync($"/api/sessions/{sessionId}/continue", content: null);
        Assert.Equal(HttpStatusCode.OK, after.StatusCode);
        using var afterJson = await ReadJsonAsync(after);
        Assert.False(afterJson.RootElement.GetProperty("hitBreakpoint").GetBoolean());
        Assert.True(afterJson.RootElement.GetProperty("reachedEndOfRecording").GetBoolean());
    }

    [Fact]
    public async Task CreateMissingDataBreakpoint_ZeroDuration_Returns400()
    {
        using var factory = new MetricBreakApiFactory();
        using var client = factory.CreateClient();
        var (sessionId, _, _) = CreateSession(factory);

        using var response = await client.PostAsJsonAsync(
            $"/api/sessions/{sessionId}/breakpoints/missing-data",
            new { minimumSeconds = 0d });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
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
            "Breakpoint HTTP Test",
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

    private static Guid CreateSession(
        MetricBreakApiFactory factory,
        string csv,
        string sourceName)
    {
        var parser = new CsvTelemetryParser();
        using var reader = new StringReader(csv);
        var parsed = parser.Parse(reader);
        var recordingId = Guid.NewGuid();
        var recording = new Recording(
            recordingId,
            sourceName,
            new RecordingSource(sourceName, "gap-hash", csv.Length),
            DateTimeOffset.UtcNow);
        var imported = new ImportedCsvRecording(
            recording,
            parsed.Channels,
            new CsvTelemetryReader(recordingId, parsed));
        return factory.SessionManager.Create(imported);
    }
}
