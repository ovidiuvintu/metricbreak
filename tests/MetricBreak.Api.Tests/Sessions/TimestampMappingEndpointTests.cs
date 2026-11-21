using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace MetricBreak.Api.Tests.Sessions;

public sealed class TimestampMappingEndpointTests
{
    [Fact]
    public async Task ClassicCsv_StillImportsWithoutAMapping()
    {
        using var factory = new MetricBreakApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsync(
            "/api/sessions",
            Csv("Timestamp,Pressure\n2026-11-08T10:00:00Z,10\n"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var created = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var sessionId = created.RootElement.GetProperty("sessionId").GetString();

        using var state = await client.GetAsync($"/api/sessions/{sessionId}");
        using var json = await JsonDocument.ParseAsync(await state.Content.ReadAsStreamAsync());
        Assert.Equal("Unix", json.RootElement.GetProperty("timestampOrigin").GetString());
    }

    [Fact]
    public async Task Preview_SuggestsEpochMilliseconds_AndImportHonorsTheOverride()
    {
        using var factory = new MetricBreakApiFactory();
        using var client = factory.CreateClient();
        const string csv = "seen_ms,Pressure\n1700000000000,10\n1700000001000,11\n";

        using var previewResponse = await client.PostAsync(
            "/api/sessions/timestamp-preview",
            Csv(csv));
        Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);
        using var preview = await JsonDocument.ParseAsync(
            await previewResponse.Content.ReadAsStreamAsync());
        Assert.Equal(0, preview.RootElement.GetProperty("suggestedColumnIndex").GetInt32());
        Assert.Equal(
            "UnixEpochMilliseconds",
            preview.RootElement.GetProperty("suggestedFormat").GetString());
        Assert.False(preview.RootElement.GetProperty("ambiguous").GetBoolean());
        Assert.Equal(
            "2023-11-14T22:13:20Z",
            preview.RootElement.GetProperty("preview")[0].GetProperty("normalized").GetString());

        using var content = Csv(csv);
        content.Add(new StringContent("0"), "timestampColumn");
        content.Add(new StringContent("UnixEpochSeconds"), "timestampFormat");
        using var import = await client.PostAsync("/api/sessions", content);
        var body = await import.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.BadRequest, import.StatusCode);
        Assert.Contains("out of range", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AmbiguousFile_RequiresAnExplicitMapping()
    {
        using var factory = new MetricBreakApiFactory();
        using var client = factory.CreateClient();
        const string csv = "time,Pressure\n0,10\n1,11\n";

        using var rejected = await client.PostAsync("/api/sessions", Csv(csv));
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);

        using var content = Csv(csv);
        content.Add(new StringContent("0"), "timestampColumn");
        content.Add(new StringContent("RelativeElapsedSeconds"), "timestampFormat");
        using var imported = await client.PostAsync("/api/sessions", content);
        Assert.Equal(HttpStatusCode.Created, imported.StatusCode);

        using var created = await JsonDocument.ParseAsync(await imported.Content.ReadAsStreamAsync());
        var sessionId = created.RootElement.GetProperty("sessionId").GetString();
        using var state = await client.GetAsync($"/api/sessions/{sessionId}");
        using var json = await JsonDocument.ParseAsync(await state.Content.ReadAsStreamAsync());
        Assert.Equal("Elapsed", json.RootElement.GetProperty("timestampOrigin").GetString());
        Assert.Equal(0, json.RootElement.GetProperty("currentSample").GetProperty("timestampNanoseconds").GetInt64());
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
