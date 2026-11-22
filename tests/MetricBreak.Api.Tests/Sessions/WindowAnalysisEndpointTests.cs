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
