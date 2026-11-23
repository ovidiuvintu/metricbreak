using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace MetricBreak.Api.Tests.Sessions;

public sealed class WindowAnalysisEndpointTests
{
    [Fact]
    public async Task Analysis_SummarizesTheRequestedWindow()
    {
        using var factory = new MetricBreakApiFactory();
        using var client = factory.CreateClient();

        using var created = await client.PostAsync(
            "/api/sessions",
            Csv("Timestamp,Pressure,Rate\n" +
                "2026-11-08T10:00:00Z,8500,40\n" +
                "2026-11-08T10:00:01Z,8700,42\n" +
                "2026-11-08T10:00:02Z,9500,45\n"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var createdJson = await JsonDocument.ParseAsync(await created.Content.ReadAsStreamAsync());
        var sessionId = createdJson.RootElement.GetProperty("sessionId").GetGuid();

        using var full = await client.GetAsync($"/api/sessions/{sessionId}/analysis");
        Assert.Equal(HttpStatusCode.OK, full.StatusCode);
        using var fullJson = await JsonDocument.ParseAsync(await full.Content.ReadAsStreamAsync());
        Assert.Equal(3, fullJson.RootElement.GetProperty("sampleCount").GetInt64());

        var pressure = Channel(fullJson.RootElement, "Pressure");
        Assert.Equal(8500, pressure.GetProperty("minimum").GetDouble());
        Assert.Equal(0, pressure.GetProperty("minimumSequence").GetInt64());
        Assert.Equal(9500, pressure.GetProperty("maximum").GetDouble());
        Assert.Equal(2, pressure.GetProperty("maximumSequence").GetInt64());
        Assert.Equal(8900, pressure.GetProperty("mean").GetDouble());
        Assert.Equal(8700, pressure.GetProperty("median").GetDouble());

        var sampling = fullJson.RootElement.GetProperty("sampling");
        Assert.Equal(3, sampling.GetProperty("sampleCount").GetInt64());
        Assert.Equal("confirmed", sampling.GetProperty("cadenceSource").GetString());
        Assert.Equal(0, sampling.GetProperty("gapCount").GetInt64());
        Assert.Equal(0, sampling.GetProperty("missingTimestampCount").GetInt64());

        var comparison = Assert.Single(
            fullJson.RootElement.GetProperty("comparisons").EnumerateArray().ToArray());
        Assert.Equal(3, comparison.GetProperty("overlapCount").GetInt64());
        Assert.True(comparison.GetProperty("correlation").GetDouble() > 0);

        using var window = await client.GetAsync(
            $"/api/sessions/{sessionId}/analysis?start=0&end=1");
        using var windowJson = await JsonDocument.ParseAsync(await window.Content.ReadAsStreamAsync());
        var windowPressure = Channel(windowJson.RootElement, "Pressure");
        Assert.Equal(8700, windowPressure.GetProperty("maximum").GetDouble());
        Assert.Equal(1, windowPressure.GetProperty("maximumSequence").GetInt64());

        using var outside = await client.GetAsync(
            $"/api/sessions/{sessionId}/analysis?start=0&end=9");
        Assert.Equal(HttpStatusCode.BadRequest, outside.StatusCode);
    }

    [Fact]
    public async Task Analysis_PreservesDuplicateTimestampsAndCountsDeclaredGaps()
    {
        using var factory = new MetricBreakApiFactory();
        using var client = factory.CreateClient();

        using var created = await client.PostAsync(
            "/api/sessions",
            Csv("Timestamp,Pressure\n" +
                "2026-11-08T10:00:00Z,1\n" +
                "2026-11-08T10:00:00Z,2\n" +
                "2026-11-08T10:00:01Z,3\n" +
                "2026-11-08T10:00:05Z,4\n"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var createdJson = await JsonDocument.ParseAsync(await created.Content.ReadAsStreamAsync());
        var sessionId = createdJson.RootElement.GetProperty("sessionId").GetGuid();

        using var declared = await client.GetAsync(
            $"/api/sessions/{sessionId}/analysis?cadenceNanoseconds=1000000000");
        Assert.Equal(HttpStatusCode.OK, declared.StatusCode);
        using var json = await JsonDocument.ParseAsync(await declared.Content.ReadAsStreamAsync());
        var sampling = json.RootElement.GetProperty("sampling");

        Assert.Equal(4, sampling.GetProperty("sampleCount").GetInt64());
        Assert.Equal("declared", sampling.GetProperty("cadenceSource").GetString());
        Assert.Equal(1, sampling.GetProperty("duplicateGroupCount").GetInt64());
        Assert.Equal(2, sampling.GetProperty("duplicateSampleCount").GetInt64());
        var sequences = sampling.GetProperty("duplicates")[0].GetProperty("sequences");
        Assert.Equal(0, sequences[0].GetInt64());
        Assert.Equal(1, sequences[1].GetInt64());

        var gap = Assert.Single(sampling.GetProperty("gaps").EnumerateArray().ToArray());
        Assert.Equal(2, gap.GetProperty("beforeSequence").GetInt64());
        Assert.Equal(3, gap.GetProperty("afterSequence").GetInt64());
        Assert.Equal(3, gap.GetProperty("missingTimestamps").GetInt64());
    }

    private static JsonElement Channel(JsonElement analysis, string name)
    {
        return analysis.GetProperty("channels")
            .EnumerateArray()
            .Single(channel => channel.GetProperty("name").GetString() == name);
    }

    private static MultipartFormDataContent Csv(string csv)
    {
        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        content.Add(file, "file", "recording.csv");
        return content;
    }
}
