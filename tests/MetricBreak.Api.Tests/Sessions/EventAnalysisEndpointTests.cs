using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace MetricBreak.Api.Tests.Sessions;

public sealed class EventAnalysisEndpointTests
{
    [Fact]
    public async Task Event_ReportsTheLeadUpAndHowLongTheConditionHolds()
    {
        using var factory = new MetricBreakApiFactory();
        using var client = factory.CreateClient();

        using var created = await client.PostAsync(
            "/api/sessions",
            Csv("Timestamp,Pressure,Rate\n" +
                "2026-11-08T10:00:00Z,100,1\n" +
                "2026-11-08T10:00:01Z,100,5\n" +
                "2026-11-08T10:00:02Z,200,5\n" +
                "2026-11-08T10:00:03Z,210,5\n" +
                "2026-11-08T10:00:04Z,50,5\n"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var createdJson = await JsonDocument.ParseAsync(await created.Content.ReadAsStreamAsync());
        var sessionId = createdJson.RootElement.GetProperty("sessionId").GetGuid();

        using var before = await client.GetAsync(
            $"/api/sessions/{sessionId}/analysis/event?sequence=2&lookback=2");
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        using var beforeJson = await JsonDocument.ParseAsync(await before.Content.ReadAsStreamAsync());
        var root = beforeJson.RootElement;

        var change = Assert.Single(root.GetProperty("immediateChanges").EnumerateArray().ToArray());
        Assert.Equal("Pressure", change.GetProperty("name").GetString());
        Assert.Equal(100, change.GetProperty("previous").GetProperty("numericValue").GetDouble());
        Assert.Equal(200, change.GetProperty("current").GetProperty("numericValue").GetDouble());

        var moves = root.GetProperty("firstMoves").EnumerateArray().ToArray();
        Assert.Equal("Rate", moves[0].GetProperty("name").GetString());
        Assert.Equal(1, moves[0].GetProperty("firstChangeSequence").GetInt64());
        Assert.Equal("Pressure", moves[1].GetProperty("name").GetString());
        Assert.Equal(2, moves[1].GetProperty("firstChangeSequence").GetInt64());

        var pressureId = root.GetProperty("states").EnumerateArray()
            .Single(state => state.GetProperty("name").GetString() == "Pressure")
            .GetProperty("channelId").GetGuid();

        using var breakpoint = await client.PostAsJsonAsync(
            $"/api/sessions/{sessionId}/breakpoints",
            new { channelId = pressureId, @operator = "GreaterThan", threshold = 150d });
        Assert.Equal(HttpStatusCode.Created, breakpoint.StatusCode);

        using var after = await client.GetAsync(
            $"/api/sessions/{sessionId}/analysis/event?sequence=2&lookback=2");
        using var afterJson = await JsonDocument.ParseAsync(await after.Content.ReadAsStreamAsync());
        var run = Assert.Single(afterJson.RootElement.GetProperty("conditionRuns").EnumerateArray().ToArray());
        Assert.True(run.GetProperty("matchesAtEvent").GetBoolean());
        Assert.Equal(2, run.GetProperty("startSequence").GetInt64());
        Assert.Equal(3, run.GetProperty("endSequence").GetInt64());
        Assert.Equal(2, run.GetProperty("sampleCount").GetInt64());
        Assert.Contains(">", run.GetProperty("label").GetString());
    }

    [Fact]
    public async Task Compare_FindsTheFirstDifferenceAgainstAKnownGoodRecording()
    {
        using var factory = new MetricBreakApiFactory();
        using var client = factory.CreateClient();

        using var subject = await client.PostAsync(
            "/api/sessions",
            Csv("Timestamp,Pressure\n" +
                "2026-11-08T10:00:00Z,1\n" +
                "2026-11-08T10:00:01Z,2\n" +
                "2026-11-08T10:00:02Z,3\n",
                "subject.csv"));
        using var reference = await client.PostAsync(
            "/api/sessions",
            Csv("Timestamp,Pressure,Stage\n" +
                "2026-11-08T10:00:00Z,1,Pad\n" +
                "2026-11-08T10:00:01Z,9,Pad\n" +
                "2026-11-08T10:00:02Z,3,Pad\n",
                "reference.csv"));
        Assert.Equal(HttpStatusCode.Created, subject.StatusCode);
        Assert.Equal(HttpStatusCode.Created, reference.StatusCode);

        using var subjectJson = await JsonDocument.ParseAsync(await subject.Content.ReadAsStreamAsync());
        using var referenceJson = await JsonDocument.ParseAsync(await reference.Content.ReadAsStreamAsync());
        var sessionId = subjectJson.RootElement.GetProperty("sessionId").GetGuid();
        var recordingId = referenceJson.RootElement.GetProperty("recordingId").GetGuid();

        using var compared = await client.GetAsync(
            $"/api/sessions/{sessionId}/analysis/compare?recordingId={recordingId}");
        Assert.Equal(HttpStatusCode.OK, compared.StatusCode);
        using var json = await JsonDocument.ParseAsync(await compared.Content.ReadAsStreamAsync());
        var root = json.RootElement;

        Assert.Equal(3, root.GetProperty("comparedSampleCount").GetInt64());
        Assert.Equal("Stage", Assert.Single(root.GetProperty("onlyInReference").EnumerateArray()).GetString());
        var channel = Assert.Single(root.GetProperty("channels").EnumerateArray().ToArray());
        Assert.Equal("Pressure", channel.GetProperty("name").GetString());
        Assert.Equal(1, channel.GetProperty("mismatchCount").GetInt64());
        Assert.Equal(1, channel.GetProperty("firstMismatchSequence").GetInt64());
        Assert.Equal(7, channel.GetProperty("maxAbsoluteDelta").GetDouble());
    }

    private static MultipartFormDataContent Csv(string csv, string fileName = "recording.csv")
    {
        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        content.Add(file, "file", fileName);
        return content;
    }
}
