using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using MetricBreak.Api.Sessions;
using MetricBreak.Domain.Breakpoints;
using MetricBreak.Ingestion.Csv;

namespace MetricBreak.Api.Tests.Sessions;

public sealed class TimelineBreakpointEndpointTests
{
    private const string Csv =
        "Timestamp,Pressure\n" +
        "2026-11-08T10:00:00Z,1\n" +
        "2026-11-08T10:00:01Z,1\n" +
        "2026-11-08T10:00:02Z,1\n" +
        "2026-11-08T10:00:03Z,1\n" +
        "2026-11-08T10:00:04Z,1\n" +
        "\n" +
        "Timestamp,Type,Name,Description\n" +
        "2026-11-08T10:00:01Z,Stage,Pad,\n" +
        "2026-11-08T10:00:02Z,Event,Breakdown,pressure\n" +
        "2026-11-08T10:00:03Z,Stage,Flush,\n" +
        "2026-11-08T10:00:04Z,Event,Breakdown,again\n";

    [Fact]
    public async Task NamedEvent_StopsOnEachMatchingSample_AndUnknownNameIsRejected()
    {
        using var factory = new MetricBreakApiFactory();
        using var client = factory.CreateClient();
        var sessionId = await CreateSession(client);

        using (var unknown = await client.PostAsJsonAsync(
                   $"/api/sessions/{sessionId}/breakpoints/timeline",
                   new { kind = "Event", name = "Does Not Exist" }))
        {
            var body = await unknown.Content.ReadAsStringAsync();
            Assert.True(unknown.StatusCode == HttpStatusCode.BadRequest, body);
            Assert.Contains("Does Not Exist", body, StringComparison.Ordinal);
        }

        Guid breakpointId;
        using (var created = await client.PostAsJsonAsync(
                   $"/api/sessions/{sessionId}/breakpoints/timeline",
                   new { kind = "Event", name = "breakdown" }))
        {
            var body = await created.Content.ReadAsStringAsync();
            Assert.True(created.StatusCode == HttpStatusCode.Created, body);
            using var json = JsonDocument.Parse(body);
            Assert.Equal(TimelineBreakpoint.EventOperatorName, json.RootElement.GetProperty("operator").GetString());
            Assert.Equal("Breakdown", json.RootElement.GetProperty("value").GetString());
            Assert.Equal(Guid.Empty, json.RootElement.GetProperty("channelId").GetGuid());
            breakpointId = json.RootElement.GetProperty("breakpointId").GetGuid();
        }

        var first = await Continue(client, sessionId);
        Assert.Equal(2, first.Sequence);
        Assert.Equal("Breakdown", first.MarkerName);

        var second = await Continue(client, sessionId);
        Assert.Equal(4, second.Sequence);
        Assert.Equal("Breakdown", second.MarkerName);

        using var listed = await client.GetAsync($"/api/sessions/{sessionId}/breakpoints");
        Assert.Equal(HttpStatusCode.OK, listed.StatusCode);
        using var listJson = await ReadJsonAsync(listed);
        var item = Assert.Single(listJson.RootElement.GetProperty("breakpoints").EnumerateArray());
        Assert.Equal(breakpointId, item.GetProperty("breakpointId").GetGuid());
        Assert.Equal(TimelineBreakpoint.EventOperatorName, item.GetProperty("operator").GetString());
    }

    [Fact]
    public async Task AnyEvent_SkipsStageMarkers()
    {
        using var factory = new MetricBreakApiFactory();
        using var client = factory.CreateClient();
        var sessionId = await CreateSession(client);

        using var created = await client.PostAsJsonAsync(
            $"/api/sessions/{sessionId}/breakpoints/timeline",
            new { kind = "Event" });
        var body = await created.Content.ReadAsStringAsync();
        Assert.True(created.StatusCode == HttpStatusCode.Created, body);

        var stop = await Continue(client, sessionId);
        Assert.Equal(2, stop.Sequence);
        Assert.Equal("Breakdown", stop.MarkerName);
    }

    [Fact]
    public async Task NamedStage_StopsOnTheStageStart()
    {
        using var factory = new MetricBreakApiFactory();
        using var client = factory.CreateClient();
        var sessionId = await CreateSession(client);

        using var created = await client.PostAsJsonAsync(
            $"/api/sessions/{sessionId}/breakpoints/timeline",
            new { kind = "Stage", name = "Flush" });
        var body = await created.Content.ReadAsStringAsync();
        Assert.True(created.StatusCode == HttpStatusCode.Created, body);
        using var json = JsonDocument.Parse(body);
        Assert.Equal(TimelineBreakpoint.StageOperatorName, json.RootElement.GetProperty("operator").GetString());
        Assert.Equal("Flush", json.RootElement.GetProperty("value").GetString());

        var stop = await Continue(client, sessionId);
        Assert.Equal(3, stop.Sequence);
        Assert.Equal("Flush", stop.MarkerName);
    }

    [Fact]
    public void ExportedInvestigation_RestoresTheTimelineBreakpoint()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            "metricbreak-timeline-breakpoint",
            Guid.NewGuid().ToString("N") + ".csv");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        try
        {
            File.WriteAllText(path, Csv);
            var importer = new CsvRecordingImporter();
            var manager = new ReplaySessionManager();
            var sessionId = manager.Create(importer.Import(path));

            Assert.True(manager.TryAddTimelineBreakpoint(
                sessionId,
                "Event",
                "Breakdown",
                out var created));
            Assert.NotNull(created);

            Assert.True(manager.TryExportInvestigation(sessionId, null, out var exported));
            Assert.NotNull(exported);
            Assert.True(manager.Remove(sessionId));

            Assert.True(manager.TryImportInvestigation(
                importer.Import(path),
                exported,
                out var resumed,
                out var error), error);

            Assert.True(manager.TryGetBreakpointResponses(resumed, out var breakpoints));
            var restored = Assert.Single(breakpoints!);
            Assert.Equal(created.Id, restored.BreakpointId);
            Assert.Equal(TimelineBreakpoint.EventOperatorName, restored.Operator);
            Assert.Equal("Breakdown", restored.Value);

            Assert.True(manager.TryContinueWithSnapshot(
                resumed,
                out var hadBreakpoints,
                out var stop,
                out _));
            Assert.True(hadBreakpoints);
            Assert.NotNull(stop);
            Assert.Equal(2, stop.Position.SequenceNumber);
            Assert.Equal("Breakdown", Assert.Single(stop.Hits).MarkerName);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    private static async Task<Guid> CreateSession(HttpClient client)
    {
        using var created = await client.PostAsync("/api/sessions", CsvUpload(Csv, "timeline.csv"));
        var body = await created.Content.ReadAsStringAsync();
        Assert.True(created.StatusCode == HttpStatusCode.Created, body);
        using var json = JsonDocument.Parse(body);
        return json.RootElement.GetProperty("sessionId").GetGuid();
    }

    private static async Task<(long Sequence, string? MarkerName)> Continue(
        HttpClient client,
        Guid sessionId)
    {
        using var continued = await client.PostAsync(
            $"/api/sessions/{sessionId}/continue",
            content: null);
        var body = await continued.Content.ReadAsStringAsync();
        Assert.True(continued.StatusCode == HttpStatusCode.OK, body);
        using var json = JsonDocument.Parse(body);
        Assert.True(json.RootElement.GetProperty("hitBreakpoint").GetBoolean(), body);
        var stop = json.RootElement.GetProperty("stop");
        var hit = stop.GetProperty("hits")[0];
        return (stop.GetProperty("sequence").GetInt64(), hit.GetProperty("observedText").GetString());
    }

    private static MultipartFormDataContent CsvUpload(string csv, string fileName)
    {
        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        content.Add(file, "file", fileName);
        return content;
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        await using var stream = await response.Content.ReadAsStreamAsync();
        return await JsonDocument.ParseAsync(stream);
    }
}
