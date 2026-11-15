using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace MetricBreak.Api.Tests.Sessions;

/// <summary>
/// Exercises actual ASP.NET Core HTTP endpoints with typed CSV uploads.
/// Reuses the existing MetricBreakApiFactory / WebApplicationFactory setup.
/// </summary>
public sealed class TypedTelemetryHttpIntegrationTests
{
    private const string Csv =
        "Timestamp,Pressure,PumpRunning,ValveState\n" +
        "2026-11-15T10:00:00Z,4500,true,OPEN\n" +
        "2026-11-15T10:00:01Z,4700,false,CLOSED\n" +
        "2026-11-15T10:00:02Z,,true,OPEN\n";

    [Fact]
    public async Task Upload_MixedTypeCsv_CreatesSession()
    {
        using var factory = new MetricBreakApiFactory();
        using var client = factory.CreateClient();
        var sessionId = await UploadAsync(client);

        using var response = await client.GetAsync($"/api/sessions/{sessionId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        Assert.Equal(sessionId.ToString(), json.RootElement.GetProperty("sessionId").GetString());
        Assert.Equal(3, json.RootElement.GetProperty("sampleCount").GetInt64());
    }

    [Fact]
    public async Task Channels_ExposeInferredValueTypes()
    {
        using var factory = new MetricBreakApiFactory();
        using var client = factory.CreateClient();
        var sessionId = await UploadAsync(client);

        using var response = await client.GetAsync($"/api/sessions/{sessionId}/channels");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        var channels = json.RootElement;
        Assert.Equal(JsonValueKind.Array, channels.ValueKind);
        Assert.Equal(3, channels.GetArrayLength());
        Assert.Equal("Numeric", FindChannel(channels, "Pressure").GetProperty("valueType").GetString());
        Assert.Equal("Boolean", FindChannel(channels, "PumpRunning").GetProperty("valueType").GetString());
        Assert.Equal("Categorical", FindChannel(channels, "ValveState").GetProperty("valueType").GetString());
    }

    [Fact]
    public async Task CurrentSample_PreservesTypedAndLegacyNumericValues()
    {
        using var factory = new MetricBreakApiFactory();
        using var client = factory.CreateClient();
        var sessionId = await UploadAsync(client);
        var ids = await GetChannelIdsAsync(client, sessionId);

        using var response = await client.GetAsync($"/api/sessions/{sessionId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        var sample = json.RootElement.GetProperty("currentSample");
        Assert.Equal(0, sample.GetProperty("sequence").GetInt64());
        Assert.Equal(4500d, sample.GetProperty("channels").GetProperty(ids["Pressure"]).GetDouble());
        Assert.False(sample.GetProperty("channels").TryGetProperty(ids["ValveState"], out _));
        Assert.False(sample.GetProperty("channels").TryGetProperty(ids["PumpRunning"], out _));

        var typed = sample.GetProperty("typedChannels");
        Assert.Equal("Numeric", typed.GetProperty(ids["Pressure"]).GetProperty("valueType").GetString());
        Assert.Equal(4500d, typed.GetProperty(ids["Pressure"]).GetProperty("value").GetDouble());
        Assert.Equal("Boolean", typed.GetProperty(ids["PumpRunning"]).GetProperty("valueType").GetString());
        Assert.True(typed.GetProperty(ids["PumpRunning"]).GetProperty("value").GetBoolean());
        Assert.Equal("Categorical", typed.GetProperty(ids["ValveState"]).GetProperty("valueType").GetString());
        Assert.Equal("OPEN", typed.GetProperty(ids["ValveState"]).GetProperty("value").GetString());
    }

    [Fact]
    public async Task SampleRange_PreservesValuesAndJsonNullForMissingPressure()
    {
        using var factory = new MetricBreakApiFactory();
        using var client = factory.CreateClient();
        var sessionId = await UploadAsync(client);
        var ids = await GetChannelIdsAsync(client, sessionId);

        using var response = await client.GetAsync($"/api/sessions/{sessionId}/samples?start=0&count=3");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        var root = json.RootElement;
        Assert.Equal(3, root.GetProperty("count").GetInt32());
        Assert.Equal(3, root.GetProperty("totalSamples").GetInt64());
        var samples = root.GetProperty("samples");
        Assert.Equal(3, samples.GetArrayLength());
        Assert.Equal("CLOSED", samples[1].GetProperty("typedChannels")
            .GetProperty(ids["ValveState"]).GetProperty("value").GetString());
        Assert.False(samples[1].GetProperty("typedChannels")
            .GetProperty(ids["PumpRunning"]).GetProperty("value").GetBoolean());
        Assert.False(samples[2].GetProperty("channels").TryGetProperty(ids["Pressure"], out _));
        var missing = samples[2].GetProperty("typedChannels").GetProperty(ids["Pressure"]);
        Assert.Equal("Missing", missing.GetProperty("valueType").GetString());
        Assert.Equal(JsonValueKind.Null, missing.GetProperty("value").ValueKind);
        Assert.True(samples[2].GetProperty("typedChannels")
            .GetProperty(ids["PumpRunning"]).GetProperty("value").GetBoolean());
    }

    [Fact]
    public async Task Seek_UpdatesCurrentSample_WithoutMutatingRecordedRange()
    {
        using var factory = new MetricBreakApiFactory();
        using var client = factory.CreateClient();
        var sessionId = await UploadAsync(client);
        var ids = await GetChannelIdsAsync(client, sessionId);

        using (var seek = await client.PostAsJsonAsync(
            $"/api/sessions/{sessionId}/seek", new { sequenceNumber = 1L }))
        {
            Assert.Equal(HttpStatusCode.OK, seek.StatusCode);
        }

        using (var state = await client.GetAsync($"/api/sessions/{sessionId}"))
        {
            Assert.Equal(HttpStatusCode.OK, state.StatusCode);
            using var json = await ReadJsonAsync(state);
            var sample = json.RootElement.GetProperty("currentSample");
            Assert.Equal(1, sample.GetProperty("sequence").GetInt64());
            Assert.Equal("CLOSED", sample.GetProperty("typedChannels")
                .GetProperty(ids["ValveState"]).GetProperty("value").GetString());
        }

        using var range = await client.GetAsync($"/api/sessions/{sessionId}/samples?start=0&count=1");
        Assert.Equal(HttpStatusCode.OK, range.StatusCode);
        using var rangeJson = await ReadJsonAsync(range);
        Assert.Equal("OPEN", rangeJson.RootElement.GetProperty("samples")[0]
            .GetProperty("typedChannels").GetProperty(ids["ValveState"])
            .GetProperty("value").GetString());
    }

    private static async Task<Guid> UploadAsync(HttpClient client)
    {
        using var form = new MultipartFormDataContent();
        using var file = new ByteArrayContent(Encoding.UTF8.GetBytes(Csv));
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
        form.Add(file, "file", "mixed-telemetry.csv");
        using var response = await client.PostAsync("/api/sessions", form);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        var sessionId = json.RootElement.GetProperty("sessionId").GetGuid();
        Assert.NotEqual(Guid.Empty, sessionId);
        return sessionId;
    }

    private static async Task<Dictionary<string, string>> GetChannelIdsAsync(HttpClient client, Guid sessionId)
    {
        using var response = await client.GetAsync($"/api/sessions/{sessionId}/channels");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        return json.RootElement.EnumerateArray().ToDictionary(
            c => c.GetProperty("name").GetString()!,
            c => c.GetProperty("channelId").GetGuid().ToString(),
            StringComparer.Ordinal);
    }

    private static JsonElement FindChannel(JsonElement channels, string name) =>
        channels.EnumerateArray().Single(c => c.GetProperty("name").GetString() == name);

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        await using var stream = await response.Content.ReadAsStreamAsync();
        return await JsonDocument.ParseAsync(stream);
    }
}
