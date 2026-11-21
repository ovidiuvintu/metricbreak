using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace MetricBreak.Api.Tests.Sessions;

public sealed class Stage1WorkspaceEndpointTests
{
    private const string _csvContent =
        "Timestamp,Pressure,Rate\n" +
        "2026-11-08T10:00:00Z,8500,40\n" +
        "2026-11-08T10:00:01Z,8700,42\n";

    [Fact]
    public async Task Import_RegistersRecording_AndReopenCreatesSession()
    {
        using var factory = new MetricBreakApiFactory();
        using var client = factory.CreateClient();

        var sessionId = await CreateSessionAsync(client);

        using (var listResponse = await client.GetAsync("/api/recordings"))
        {
            Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

            using var json = await ReadJsonAsync(listResponse);
            var recording = Assert.Single(
                json.RootElement.GetProperty("recordings").EnumerateArray());

            var recordingId = recording.GetProperty("recordingId").GetGuid();

            using var openResponse = await client.PostAsync(
                $"/api/recordings/{recordingId}/open",
                content: null);

            var openBody = await openResponse.Content.ReadAsStringAsync();
            Assert.True(
                openResponse.StatusCode == HttpStatusCode.Created,
                openBody);

            using var openJson = await ReadJsonAsync(openResponse);
            Assert.NotEqual(sessionId, openJson.RootElement.GetProperty("sessionId").GetGuid());
        }
    }

    [Fact]
    public async Task ReExport_PreservesBookmarks_AndUsesSameInvestigationId()
    {
        using var factory = new MetricBreakApiFactory();
        using var client = factory.CreateClient();

        var sessionId = await CreateSessionAsync(client);

        Guid investigationId;

        using (var exportResponse = await client.PostAsJsonAsync(
                   $"/api/sessions/{sessionId}/export",
                   new { name = "Checkout drop" }))
        {
            Assert.Equal(HttpStatusCode.Created, exportResponse.StatusCode);

            using var json = await ReadJsonAsync(exportResponse);
            investigationId = json.RootElement.GetProperty("investigationId").GetGuid();
        }

        using (var createBookmark = await client.PostAsJsonAsync(
                   $"/api/investigations/{investigationId}/bookmarks",
                   new
                   {
                       sampleSequence = 1,
                       timestampNanoseconds = 1L,
                       title = "Anchor",
                   }))
        {
            Assert.Equal(HttpStatusCode.Created, createBookmark.StatusCode);
        }

        using (var reExport = await client.PostAsJsonAsync(
                   $"/api/sessions/{sessionId}/export",
                   new
                   {
                       investigationId,
                       name = "Checkout drop",
                       uiState = new
                       {
                           selectedChannelIds = new[] { "Pressure" },
                           rightAxisChannelIds = Array.Empty<string>(),
                       },
                   }))
        {
            Assert.Equal(HttpStatusCode.Created, reExport.StatusCode);

            using var json = await ReadJsonAsync(reExport);
            Assert.Equal(investigationId, json.RootElement.GetProperty("investigationId").GetGuid());
        }

        using var detail = await client.GetAsync($"/api/investigations/{investigationId}");
        using var detailJson = await ReadJsonAsync(detail);

        Assert.Equal(2, detailJson.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("Checkout drop", detailJson.RootElement
            .GetProperty("metadata")
            .GetProperty("name")
            .GetString());

        var bookmark = Assert.Single(
            detailJson.RootElement.GetProperty("bookmarks").EnumerateArray());

        Assert.Equal("Anchor", bookmark.GetProperty("title").GetString());
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