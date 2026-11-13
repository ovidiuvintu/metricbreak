
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MetricBreak.Api.Tests.Sessions;

namespace MetricBreak.Api.Tests;

public class ReplayBreakpointIntegrationTests
{
    [Fact]
    public async Task Continue_MultipleMatchingBreakpoints_ReportsAllHits()
    {
        using var factory = new MetricBreakApiFactory();
        using var client = factory.CreateClient();
        var (sessionId, channelId) = await CreateTestSessionAsync(client);

        var firstId = await AddBreakpointAsync(
            client, sessionId, channelId, "GreaterThan", 25);

        var secondId = await AddBreakpointAsync(
            client, sessionId, channelId, "GreaterThan", 25.5);

        using var response = await client.PostAsync(
            $"/api/sessions/{sessionId}/continue", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var result = await ReadJsonAsync(response);
        var root = result.RootElement;

        Assert.True(root.GetProperty("hitBreakpoint").GetBoolean());

        var stop = root.GetProperty("stop");
        Assert.Equal(3, stop.GetProperty("sequence").GetInt64());

        var hits = stop.GetProperty("hits")
            .EnumerateArray()
            .Select(hit => hit.GetProperty("breakpointId").GetGuid())
            .ToHashSet();

        Assert.Equal(2, hits.Count);
        Assert.Contains(firstId, hits);
        Assert.Contains(secondId, hits);

        // Continue must advance beyond the previous stop.
        using var nextResponse = await client.PostAsync(
            $"/api/sessions/{sessionId}/continue", null);

        using var nextResult = await ReadJsonAsync(nextResponse);
        Assert.Equal(
            4,
            nextResult.RootElement
                .GetProperty("stop")
                .GetProperty("sequence")
                .GetInt64());
    }

    [Fact]
    public async Task Continue_DisabledBreakpoint_IsIgnored()
    {
        using var factory = new MetricBreakApiFactory();
        using var client = factory.CreateClient();
        var (sessionId, channelId) = await CreateTestSessionAsync(client);

        var disabledId = await AddBreakpointAsync(
            client, sessionId, channelId, "GreaterThan", 25);

        var enabledId = await AddBreakpointAsync(
            client, sessionId, channelId, "GreaterThan", 35);

        using var patchResponse = await client.PatchAsJsonAsync(
            $"/api/sessions/{sessionId}/breakpoints/{disabledId}",
            new { isEnabled = false });

        Assert.Equal(HttpStatusCode.NoContent, patchResponse.StatusCode);

        using var response = await client.PostAsync(
            $"/api/sessions/{sessionId}/continue", null);

        using var result = await ReadJsonAsync(response);
        var root = result.RootElement;

        Assert.True(root.GetProperty("hitBreakpoint").GetBoolean());

        var stop = root.GetProperty("stop");
        Assert.Equal(6, stop.GetProperty("sequence").GetInt64());

        var hits = stop.GetProperty("hits").EnumerateArray().ToArray();

        Assert.Single(hits);
        Assert.Equal(
            enabledId,
            hits[0].GetProperty("breakpointId").GetGuid());
    }

    [Fact]
    public async Task Continue_DeletedBreakpoint_IsIgnored()
    {
        using var factory = new MetricBreakApiFactory();
        using var client = factory.CreateClient();
        var (sessionId, channelId) = await CreateTestSessionAsync(client);

        var deletedId = await AddBreakpointAsync(
            client, sessionId, channelId, "GreaterThan", 25);

        var retainedId = await AddBreakpointAsync(
            client, sessionId, channelId, "GreaterThan", 35);

        using var deleteResponse = await client.DeleteAsync(
            $"/api/sessions/{sessionId}/breakpoints/{deletedId}");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        using var response = await client.PostAsync(
            $"/api/sessions/{sessionId}/continue", null);

        using var result = await ReadJsonAsync(response);
        var root = result.RootElement;

        Assert.True(root.GetProperty("hitBreakpoint").GetBoolean());

        var stop = root.GetProperty("stop");
        Assert.Equal(6, stop.GetProperty("sequence").GetInt64());

        var hits = stop.GetProperty("hits").EnumerateArray().ToArray();

        Assert.Single(hits);
        Assert.Equal(
            retainedId,
            hits[0].GetProperty("breakpointId").GetGuid());
    }

    // Helpers

    private static async Task<Guid> AddBreakpointAsync(
        HttpClient client,
        Guid sessionId,
        Guid channelId,
        string comparisonOperator,
        double threshold)
    {
        using var response = await client.PostAsJsonAsync(
            $"/api/sessions/{sessionId}/breakpoints",
            new
            {
                channelId,
                @operator = comparisonOperator,
                threshold
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var json = await ReadJsonAsync(response);

        return json.RootElement
            .GetProperty("breakpointId")
            .GetGuid();
    }

    private static async Task<JsonDocument> ReadJsonAsync(
        HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();

        var stream = await response.Content.ReadAsStreamAsync();
        return await JsonDocument.ParseAsync(stream);
    }

    private static async Task<(Guid SessionId, Guid ChannelId)>
    CreateTestSessionAsync(HttpClient client)
    {
        const string csv = """
        Timestamp,value
        2026-01-01T00:00:00Z,5
        2026-01-01T00:00:01Z,10
        2026-01-01T00:00:02Z,25
        2026-01-01T00:00:03Z,26
        2026-01-01T00:00:04Z,30
        2026-01-01T00:00:05Z,25
        2026-01-01T00:00:06Z,40
        2026-01-01T00:00:07Z,10
        """;

        using var content = new MultipartFormDataContent();

        var fileContent = new ByteArrayContent(
            System.Text.Encoding.UTF8.GetBytes(csv));

        content.Add(fileContent, "file", "breakpoint-test.csv");

        // Create the replay session.
        using var response = await client.PostAsync(
            "/api/sessions",
            content);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var sessionJson = await ReadJsonAsync(response);

        var sessionId = sessionJson.RootElement
            .GetProperty("sessionId")
            .GetGuid();

        // Retrieve the channel associated with the imported CSV.
        using var channelsResponse = await client.GetAsync(
            $"/api/sessions/{sessionId}/channels");

        Assert.Equal(HttpStatusCode.OK, channelsResponse.StatusCode);

        using var channelsJson = await ReadJsonAsync(channelsResponse);

        var channelId = channelsJson.RootElement[0]
            .GetProperty("channelId")
            .GetGuid();

        return (sessionId, channelId);
    }
}
