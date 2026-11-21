using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace MetricBreak.Api.Tests.Sessions;

public sealed class WorkspaceInvestigationEndpointTests
{
    private const string _csvContent =
        "Timestamp,Pressure,Rate\n" +
        "2026-11-08T10:00:00Z,8500,40\n" +
        "2026-11-08T10:00:00Z,8700,42\n" +
        "2026-11-08T10:00:01Z,9500,45\n";

    [Fact]
    public async Task ExportWithoutProject_ListsAndBookmarksPersist()
    {
        using var factory = new MetricBreakApiFactory();
        using var client = factory.CreateClient();

        var sessionId = await CreateSessionAsync(client);

        Guid investigationId;

        using (var exportResponse = await client.PostAsync(
                   $"/api/sessions/{sessionId}/export",
                   content: null))
        {
            Assert.Equal(HttpStatusCode.Created, exportResponse.StatusCode);

            using var json = await ReadJsonAsync(exportResponse);
            investigationId = json.RootElement.GetProperty("investigationId").GetGuid();
        }

        using (var listResponse = await client.GetAsync("/api/investigations"))
        {
            Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

            using var json = await ReadJsonAsync(listResponse);
            var items = json.RootElement
                .GetProperty("investigations")
                .EnumerateArray()
                .ToArray();

            var match = Assert.Single(items);
            Assert.Equal(investigationId, match.GetProperty("investigationId").GetGuid());
            Assert.Equal("recording.csv", match.GetProperty("recordingName").GetString());
        }

        using (var createBookmark = await client.PostAsJsonAsync(
                   $"/api/investigations/{investigationId}/bookmarks",
                   new
                   {
                       sampleSequence = 1,
                       timestampNanoseconds = 42L,
                       title = "Second duplicate-timestamp row",
                   }))
        {
            Assert.Equal(HttpStatusCode.Created, createBookmark.StatusCode);
        }

        using var listBookmarks = await client.GetAsync(
            $"/api/investigations/{investigationId}/bookmarks");

        using (var bookmarkJson = await ReadJsonAsync(listBookmarks))
        {
            var bookmark = Assert.Single(
                bookmarkJson.RootElement.GetProperty("bookmarks").EnumerateArray());

            Assert.Equal(1, bookmark.GetProperty("sampleSequence").GetInt64());
            Assert.Equal(
                "Second duplicate-timestamp row",
                bookmark.GetProperty("title").GetString());
        }
    }

    [Fact]
    public async Task ListWorkspaceInvestigations_FiltersByRecordingName()
    {
        using var factory = new MetricBreakApiFactory();
        using var client = factory.CreateClient();

        var sessionA = await CreateSessionAsync(client, _csvContent, "alpha.csv");
        var sessionB = await CreateSessionAsync(client, _csvContent, "beta.csv");

        using (var exportA = await client.PostAsync(
                   $"/api/sessions/{sessionA}/export",
                   content: null))
        {
            Assert.Equal(HttpStatusCode.Created, exportA.StatusCode);
        }

        using (var exportB = await client.PostAsync(
                   $"/api/sessions/{sessionB}/export",
                   content: null))
        {
            Assert.Equal(HttpStatusCode.Created, exportB.StatusCode);
        }

        using var filtered = await client.GetAsync(
            "/api/investigations?recordingName=beta.csv");

        using var json = await ReadJsonAsync(filtered);
        var items = json.RootElement
            .GetProperty("investigations")
            .EnumerateArray()
            .ToArray();

        var match = Assert.Single(items);
        Assert.Equal("beta.csv", match.GetProperty("recordingName").GetString());
    }

    private static async Task<Guid> CreateSessionAsync(
        HttpClient client,
        string csvContent = _csvContent,
        string fileName = "recording.csv")
    {
        using var createSessionResponse = await client.PostAsync(
            "/api/sessions",
            CreateCsvUploadContent(csvContent, fileName));

        using var json = await ReadJsonAsync(createSessionResponse);
        Assert.Equal(HttpStatusCode.Created, createSessionResponse.StatusCode);
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