using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace MetricBreak.Api.Tests.Sessions;

public sealed class InvestigationBookmarkEndpointTests
{
    private const string _csvContent =
        "Timestamp,Pressure,Rate\n" +
        "2026-11-08T10:00:00Z,8500,40\n" +
        "2026-11-08T10:00:00Z,8700,42\n" +
        "2026-11-08T10:00:01Z,9500,45\n";

    [Fact]
    public async Task CreateListUpdateDelete_PersistsThroughRevision()
    {
        using var factory = new MetricBreakApiFactory();
        using var client = factory.CreateClient();

        var rootPath = CreateRootPath();

        try
        {
        var (projectId, investigationId) =
            await ExportInvestigationAsync(client, rootPath, _csvContent);

        using (var listResponse = await client.GetAsync(
                   $"/api/investigations/{investigationId}/bookmarks?projectId={projectId}"))
        {
            Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

            using var json = await ReadJsonAsync(listResponse);
            Assert.Empty(
                json.RootElement.GetProperty("bookmarks").EnumerateArray().ToArray());
        }

        Guid bookmarkId;

        using (var createResponse = await client.PostAsJsonAsync(
                   $"/api/investigations/{investigationId}/bookmarks?projectId={projectId}",
                   new
                   {
                       sampleSequence = 1,
                       timestampNanoseconds = 42L,
                       title = "Duplicate timestamp row",
                       annotation = "Second sample shares clock value.",
                   }))
        {
            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

            using var json = await ReadJsonAsync(createResponse);
            bookmarkId = json.RootElement.GetProperty("bookmarkId").GetGuid();
            Assert.Equal(1, json.RootElement.GetProperty("sampleSequence").GetInt64());
        }

        using (var updateResponse = await client.PutAsJsonAsync(
                   $"/api/investigations/{investigationId}/bookmarks/{bookmarkId}?projectId={projectId}",
                   new
                   {
                       title = "Updated title",
                       annotation = "Edited annotation",
                   }))
        {
            Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        }

        using (var listAfterUpdate = await client.GetAsync(
                   $"/api/investigations/{investigationId}/bookmarks?projectId={projectId}"))
        {
            using var json = await ReadJsonAsync(listAfterUpdate);

            var bookmark = Assert.Single(
                json.RootElement.GetProperty("bookmarks").EnumerateArray());

            Assert.Equal("Updated title", bookmark.GetProperty("title").GetString());
            Assert.Equal(
                "Edited annotation",
                bookmark.GetProperty("annotation").GetString());
        }

        using (var deleteResponse = await client.DeleteAsync(
                   $"/api/investigations/{investigationId}/bookmarks/{bookmarkId}?projectId={projectId}"))
        {
            Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        }

        using var listAfterDelete = await client.GetAsync(
            $"/api/investigations/{investigationId}/bookmarks?projectId={projectId}");

        using (var emptyJson = await ReadJsonAsync(listAfterDelete))
        {
            Assert.Empty(
                emptyJson.RootElement.GetProperty("bookmarks").EnumerateArray().ToArray());
        }
        }
        finally
        {
            DeleteRoot(rootPath);
        }
    }

    [Fact]
    public async Task MultipleBookmarksAtSameSequence_AreSortedDeterministically()
    {
        using var factory = new MetricBreakApiFactory();
        using var client = factory.CreateClient();

        var rootPath = CreateRootPath();

        try
        {
        var (projectId, investigationId) =
            await ExportInvestigationAsync(client, rootPath, _csvContent);

        await client.PostAsJsonAsync(
            $"/api/investigations/{investigationId}/bookmarks?projectId={projectId}",
            new
            {
                sampleSequence = 0,
                timestampNanoseconds = 1L,
                title = "First",
            });

        await client.PostAsJsonAsync(
            $"/api/investigations/{investigationId}/bookmarks?projectId={projectId}",
            new
            {
                sampleSequence = 0,
                timestampNanoseconds = 1L,
                title = "Second",
            });

        using var listResponse = await client.GetAsync(
            $"/api/investigations/{investigationId}/bookmarks?projectId={projectId}");

        using var json = await ReadJsonAsync(listResponse);

        var titles = json.RootElement
            .GetProperty("bookmarks")
            .EnumerateArray()
            .Select(item => item.GetProperty("title").GetString())
            .ToArray();

        Assert.Equal(new[] { "First", "Second" }, titles);
        }
        finally
        {
            DeleteRoot(rootPath);
        }
    }

    [Fact]
    public async Task BookmarkChanges_IsolateInvestigations()
    {
        using var factory = new MetricBreakApiFactory();
        using var client = factory.CreateClient();

        var rootPath = CreateRootPath();

        try
        {
        var (projectId, firstInvestigationId) =
            await ExportInvestigationAsync(client, rootPath, _csvContent);

        Guid secondInvestigationId;

        using (var secondExport = await client.PostAsync(
                   $"/api/sessions/{await CreateSessionAsync(client)}/export?projectId={projectId}",
                   content: null))
        {
            using var exportJson = await ReadJsonAsync(secondExport);
            secondInvestigationId = exportJson.RootElement.GetProperty("investigationId").GetGuid();
        }

        await client.PostAsJsonAsync(
            $"/api/investigations/{firstInvestigationId}/bookmarks?projectId={projectId}",
            new
            {
                sampleSequence = 0,
                timestampNanoseconds = 0L,
                title = "Only on first",
            });

        using var secondList = await client.GetAsync(
            $"/api/investigations/{secondInvestigationId}/bookmarks?projectId={projectId}");

        using var secondListJson = await ReadJsonAsync(secondList);
        Assert.Empty(
            secondListJson.RootElement.GetProperty("bookmarks").EnumerateArray().ToArray());
        }
        finally
        {
            DeleteRoot(rootPath);
        }
    }

    [Fact]
    public async Task InvalidSampleSequence_ReturnsBadRequest()
    {
        using var factory = new MetricBreakApiFactory();
        using var client = factory.CreateClient();

        var rootPath = CreateRootPath();

        try
        {
        var (projectId, investigationId) =
            await ExportInvestigationAsync(client, rootPath, _csvContent);

        using var response = await client.PostAsJsonAsync(
            $"/api/investigations/{investigationId}/bookmarks?projectId={projectId}",
            new
            {
                sampleSequence = 99,
                timestampNanoseconds = 0L,
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        finally
        {
            DeleteRoot(rootPath);
        }
    }

    [Fact]
    public async Task ImportInvestigation_PreservesBookmarks()
    {
        using var factory = new MetricBreakApiFactory();
        using var client = factory.CreateClient();

        var rootPath = CreateRootPath();

        try
        {
        var (projectId, investigationId) =
            await ExportInvestigationAsync(client, rootPath, _csvContent);

        await client.PostAsJsonAsync(
            $"/api/investigations/{investigationId}/bookmarks?projectId={projectId}",
            new
            {
                sampleSequence = 2,
                timestampNanoseconds = 5L,
                title = "End of run",
            });

        using var importResponse = await client.PostAsync(
            "/api/sessions/import",
            CreateImportContent(projectId, investigationId, _csvContent, "recording.csv"));

        Assert.Equal(HttpStatusCode.Created, importResponse.StatusCode);

        using var listResponse = await client.GetAsync(
            $"/api/investigations/{investigationId}/bookmarks?projectId={projectId}");

        using var json = await ReadJsonAsync(listResponse);

        var bookmark = Assert.Single(
            json.RootElement.GetProperty("bookmarks").EnumerateArray());

        Assert.Equal("End of run", bookmark.GetProperty("title").GetString());
        Assert.Equal(2, bookmark.GetProperty("sampleSequence").GetInt64());
        }
        finally
        {
            DeleteRoot(rootPath);
        }
    }

    [Fact]
    public async Task LegacyInvestigationWithoutBookmarks_LoadsEmptyList()
    {
        using var factory = new MetricBreakApiFactory();
        using var client = factory.CreateClient();

        var rootPath = Path.Combine(
            Path.GetTempPath(),
            "metricbreak-bookmark-tests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(rootPath);

        try
        {
            Guid projectId;
            Guid investigationId = Guid.NewGuid();

            using (var createProjectResponse = await client.PostAsJsonAsync(
                       "/api/projects",
                       new
                       {
                           projectName = "Legacy Bookmarks",
                           rootPath,
                       }))
            {
                using var json = await ReadJsonAsync(createProjectResponse);
                projectId = json.RootElement.GetProperty("projectId").GetGuid();
            }

            var investigationDirectory = Path.Combine(
                rootPath,
                ".metricbreak",
                "investigations");

            Directory.CreateDirectory(investigationDirectory);

            var revisionId = Guid.NewGuid();
            var relativePath = $"investigations/{investigationId:N}-{revisionId:N}.json";
            var revisionPath = Path.Combine(
                rootPath,
                ".metricbreak",
                relativePath.Replace('/', Path.DirectorySeparatorChar));

            await File.WriteAllTextAsync(
                revisionPath,
                $$"""
                {
                  "SchemaVersion": 1,
                  "InvestigationId": "{{investigationId}}",
                  "ExportedAtUtc": "2026-01-01T00:00:00+00:00",
                  "Recording": {
                    "SourceName": "recording.csv",
                    "SourceContentHash": "legacy-hash",
                    "SourceSizeInBytes": 64,
                    "RecordingName": "recording.csv",
                    "SampleCount": 3
                  },
                  "Session": {
                    "SequenceNumber": 0,
                    "ExecutionState": "Paused",
                    "PauseReason": "None"
                  },
                  "Breakpoints": []
                }
                """);

            var manifestPath = Path.Combine(
                rootPath,
                ".metricbreak",
                "project.mbproj.json");

            await File.WriteAllTextAsync(
                manifestPath,
                $$"""
                {
                  "SchemaVersion": 1,
                  "ProjectId": "{{projectId}}",
                  "ProjectName": "Legacy Bookmarks",
                  "CreatedAtUtc": "2026-01-01T00:00:00+00:00",
                  "UpdatedAtUtc": "2026-01-01T00:00:00+00:00",
                  "Investigations": [
                    {
                      "InvestigationId": "{{investigationId}}",
                      "RelativePath": "{{relativePath.Replace("\\", "/")}}",
                      "RecordingName": "recording.csv",
                      "SourceContentHash": "legacy-hash",
                      "SourceSizeInBytes": 64,
                      "ExportedAtUtc": "2026-01-01T00:00:00+00:00"
                    }
                  ]
                }
                """);

            using (var openResponse = await client.PostAsJsonAsync(
                       "/api/projects/open",
                       new { rootPath }))
            {
                Assert.Equal(HttpStatusCode.OK, openResponse.StatusCode);
            }

            using var listResponse = await client.GetAsync(
                $"/api/investigations/{investigationId}/bookmarks?projectId={projectId}");

            Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

            using var listJson = await ReadJsonAsync(listResponse);
            Assert.Empty(
                listJson.RootElement.GetProperty("bookmarks").EnumerateArray().ToArray());
        }
        finally
        {
            if (Directory.Exists(rootPath))
            {
                Directory.Delete(rootPath, recursive: true);
            }
        }
    }

    private static string CreateRootPath()
    {
        var rootPath = Path.Combine(
            Path.GetTempPath(),
            "metricbreak-bookmark-tests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(rootPath);
        return rootPath;
    }

    private static void DeleteRoot(string rootPath)
    {
        if (Directory.Exists(rootPath))
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }

    private static async Task<(Guid ProjectId, Guid InvestigationId)> ExportInvestigationAsync(
        HttpClient client,
        string rootPath,
        string csvContent)
    {
        Guid projectId;

        using (var createProjectResponse = await client.PostAsJsonAsync(
                   "/api/projects",
                   new
                   {
                       projectName = "Bookmark Test",
                       rootPath,
                   }))
        {
            using var json = await ReadJsonAsync(createProjectResponse);
            projectId = json.RootElement.GetProperty("projectId").GetGuid();
        }

        var sessionId = await CreateSessionAsync(client, csvContent);

        using var exportResponse = await client.PostAsync(
            $"/api/sessions/{sessionId}/export?projectId={projectId}",
            content: null);

        using var exportJson = await ReadJsonAsync(exportResponse);

        return (
            projectId,
            exportJson.RootElement.GetProperty("investigationId").GetGuid());
    }

    private static async Task<Guid> CreateSessionAsync(
        HttpClient client,
        string csvContent = _csvContent)
    {
        using var createSessionResponse = await client.PostAsync(
            "/api/sessions",
            CreateCsvUploadContent(csvContent, "recording.csv"));

        using var json = await ReadJsonAsync(createSessionResponse);
        return json.RootElement.GetProperty("sessionId").GetGuid();
    }

    private static MultipartFormDataContent CreateImportContent(
        Guid projectId,
        Guid investigationId,
        string csv,
        string fileName)
    {
        var content = new MultipartFormDataContent();
        content.Add(new StringContent(projectId.ToString()), "projectId");
        content.Add(new StringContent(investigationId.ToString()), "investigationId");

        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        content.Add(fileContent, "file", fileName);

        return content;
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