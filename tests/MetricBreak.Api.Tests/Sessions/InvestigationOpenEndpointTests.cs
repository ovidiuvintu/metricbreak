using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace MetricBreak.Api.Tests.Sessions;

public sealed class InvestigationOpenEndpointTests
{
    private const string _csvContent =
        "Timestamp,Pressure,Rate\n" +
        "2026-11-08T10:00:00Z,8500,40\n" +
        "2026-11-08T10:00:01Z,8700,42\n" +
        "2026-11-08T10:00:02Z,9500,45\n";

    [Fact]
    public async Task OpenInvestigation_RestoresSequenceFromCatalog()
    {
        using var factory = new MetricBreakApiFactory();
        using var client = factory.CreateClient();

        var sessionId = await CreateSessionAsync(client);

        using (var seekResponse = await client.PostAsJsonAsync(
                   $"/api/sessions/{sessionId}/seek",
                   new { sequenceNumber = 2L }))
        {
            Assert.Equal(HttpStatusCode.OK, seekResponse.StatusCode);
        }

        Guid investigationId;

        using (var exportResponse = await client.PostAsJsonAsync(
                   $"/api/sessions/{sessionId}/export",
                   new { name = "Pressure sweep" }))
        {
            Assert.Equal(HttpStatusCode.Created, exportResponse.StatusCode);

            using var json = await ReadJsonAsync(exportResponse);
            investigationId = json.RootElement.GetProperty("investigationId").GetGuid();
        }

        using var openResponse = await client.PostAsync(
            $"/api/investigations/{investigationId}/open?closeSessionId={sessionId}",
            content: null);

        var openBody = await openResponse.Content.ReadAsStringAsync();
        Assert.True(
            openResponse.StatusCode == HttpStatusCode.Created,
            openBody);

        using var openJson = await ReadJsonAsync(openResponse);
        Assert.Equal(2, openJson.RootElement.GetProperty("sequenceNumber").GetInt64());

        var newSessionId = openJson.RootElement.GetProperty("sessionId").GetGuid();

        using var sessionState = await client.GetAsync($"/api/sessions/{newSessionId}");
        using var stateJson = await ReadJsonAsync(sessionState);
        Assert.Equal(
            2,
            stateJson.RootElement
                .GetProperty("currentSample")
                .GetProperty("sequence")
                .GetInt64());
    }

    [Fact]
    public async Task OpenInvestigation_UpdatesLastOpenedInWorkspaceList()
    {
        using var factory = new MetricBreakApiFactory();
        using var client = factory.CreateClient();

        var sessionId = await CreateSessionAsync(client);

        Guid investigationId;

        using (var exportResponse = await client.PostAsJsonAsync(
                   $"/api/sessions/{sessionId}/export",
                   new { name = "Last-opened probe" }))
        {
            Assert.Equal(HttpStatusCode.Created, exportResponse.StatusCode);
            using var json = await ReadJsonAsync(exportResponse);
            investigationId = json.RootElement.GetProperty("investigationId").GetGuid();
        }

        using (var listBeforeOpen = await client.GetAsync("/api/investigations"))
        {
            using var json = await ReadJsonAsync(listBeforeOpen);
            var item = json.RootElement.GetProperty("investigations")
                .EnumerateArray()
                .Single(element =>
                    element.GetProperty("investigationId").GetGuid() == investigationId);

            var exportedAt = item.GetProperty("exportedAtUtc").GetDateTimeOffset();
            var lastOpened = item.GetProperty("lastOpenedAtUtc").GetDateTimeOffset();
            Assert.Equal(exportedAt, lastOpened);
        }

        using var openResponse = await client.PostAsync(
            $"/api/investigations/{investigationId}/open?closeSessionId={sessionId}",
            content: null);
        Assert.Equal(HttpStatusCode.Created, openResponse.StatusCode);

        using var listAfterOpen = await client.GetAsync("/api/investigations");
        using var afterJson = await ReadJsonAsync(listAfterOpen);
        var afterItem = afterJson.RootElement.GetProperty("investigations")
            .EnumerateArray()
            .Single(element =>
                element.GetProperty("investigationId").GetGuid() == investigationId);

        Assert.True(
            afterItem.GetProperty("lastOpenedAtUtc").GetDateTimeOffset()
            >= afterItem.GetProperty("exportedAtUtc").GetDateTimeOffset());
    }

    [Fact]
    public async Task OpenInvestigation_RestoresBreakpointsOnNewSession()
    {
        using var factory = new MetricBreakApiFactory();
        using var client = factory.CreateClient();

        var sessionId = await CreateSessionAsync(client);

        using (var channelResponse = await client.GetAsync(
                   $"/api/sessions/{sessionId}/channels"))
        {
            using var channels = await ReadJsonAsync(channelResponse);
            var channelId = channels.RootElement
                .GetProperty("channels")
                .EnumerateArray()
                .First()
                .GetProperty("channelId")
                .GetString();

            using var breakpointResponse = await client.PostAsJsonAsync(
                $"/api/sessions/{sessionId}/breakpoints",
                new
                {
                    channelId,
                    @operator = "GreaterThan",
                    threshold = 8600,
                });
            Assert.Equal(HttpStatusCode.Created, breakpointResponse.StatusCode);
        }

        Guid investigationId;

        using (var exportResponse = await client.PostAsJsonAsync(
                   $"/api/sessions/{sessionId}/export",
                   new { name = "Breakpoint carryover" }))
        {
            Assert.Equal(HttpStatusCode.Created, exportResponse.StatusCode);
            using var json = await ReadJsonAsync(exportResponse);
            investigationId = json.RootElement.GetProperty("investigationId").GetGuid();
        }

        using var openResponse = await client.PostAsync(
            $"/api/investigations/{investigationId}/open?closeSessionId={sessionId}",
            content: null);
        Assert.Equal(HttpStatusCode.Created, openResponse.StatusCode);

        using var openJson = await ReadJsonAsync(openResponse);
        var newSessionId = openJson.RootElement.GetProperty("sessionId").GetGuid();

        using var breakpointsResponse = await client.GetAsync(
            $"/api/sessions/{newSessionId}/breakpoints");
        using var breakpointsJson = await ReadJsonAsync(breakpointsResponse);

        Assert.Equal(
            1,
            breakpointsJson.RootElement
                .GetProperty("breakpoints")
                .GetArrayLength());
    }

    private static async Task<Guid> CreateSessionAsync(HttpClient client)
    {
        using var createSessionResponse = await client.PostAsync(
            "/api/sessions",
            CreateCsvUploadContent(_csvContent, "recording.csv"));

        Assert.Equal(HttpStatusCode.Created, createSessionResponse.StatusCode);

        using var json = await ReadJsonAsync(createSessionResponse);
        return json.RootElement.GetProperty("sessionId").GetGuid();
    }

    private static MultipartFormDataContent CreateCsvUploadContent(
        string csv,
        string fileName)
    {
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        content.Add(fileContent, "file", fileName);
        return content;
    }

    private static async Task<JsonDocument> ReadJsonAsync(
        HttpResponseMessage response)
    {
        await using var stream = await response.Content.ReadAsStreamAsync();
        return await JsonDocument.ParseAsync(stream);
    }
}