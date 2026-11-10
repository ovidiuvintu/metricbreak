using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace MetricBreak.Api.Tests.Sessions;

public sealed class ProjectWorkspaceEndpointTests
{
    private const string _csvContent =
        "Timestamp,Pressure,Rate\n" +
        "2026-11-08T10:00:00Z,8500,40\n" +
        "2026-11-08T10:00:01Z,8700,42\n" +
        "2026-11-08T10:00:02Z,9500,45\n";

    [Fact]
    public async Task CreateOpenExportAndImport_ThroughProjectWorkspace_Works()
    {
        using var factory = new MetricBreakApiFactory();
        using var client = factory.CreateClient();

        var rootPath = Path.Combine(
            Path.GetTempPath(),
            "metricbreak-project-tests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(rootPath);

        try
        {
            Guid projectId;

            using (var createProjectResponse = await client.PostAsJsonAsync(
                       "/api/projects",
                       new
                       {
                           projectName = "Workspace Test",
                           rootPath
                       }))
            {
                Assert.Equal(HttpStatusCode.Created, createProjectResponse.StatusCode);

                using var json = await ReadJsonAsync(createProjectResponse);
                projectId = json.RootElement.GetProperty("projectId").GetGuid();
            }

            using (var openResponse = await client.PostAsJsonAsync(
                       "/api/projects/open",
                       new { rootPath }))
            {
                Assert.Equal(HttpStatusCode.OK, openResponse.StatusCode);
            }

            Guid sessionId;

            using (var createSessionResponse = await client.PostAsync(
                       "/api/sessions",
                       CreateCsvUploadContent(_csvContent, "recording.csv")))
            {
                Assert.Equal(HttpStatusCode.Created, createSessionResponse.StatusCode);

                using var json = await ReadJsonAsync(createSessionResponse);
                sessionId = json.RootElement.GetProperty("sessionId").GetGuid();
            }

            Guid investigationId;

            using (var exportResponse = await client.PostAsync(
                       $"/api/sessions/{sessionId}/export?projectId={projectId}",
                       content: null))
            {
                Assert.Equal(HttpStatusCode.Created, exportResponse.StatusCode);

                using var json = await ReadJsonAsync(exportResponse);
                investigationId = json.RootElement.GetProperty("investigationId").GetGuid();
            }

            using (var listResponse = await client.GetAsync(
                       $"/api/projects/{projectId}/investigations"))
            {
                Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

                using var json = await ReadJsonAsync(listResponse);

                var investigations = json.RootElement
                    .GetProperty("investigations")
                    .EnumerateArray()
                    .ToArray();

                Assert.Single(investigations);
                Assert.Equal(
                    investigationId,
                    investigations[0].GetProperty("investigationId").GetGuid());
            }

            using var importResponse = await client.PostAsync(
                "/api/sessions/import",
                CreateImportContent(
                    projectId,
                    investigationId,
                    _csvContent,
                    "recording.csv"));

            Assert.Equal(HttpStatusCode.Created, importResponse.StatusCode);
        }
        finally
        {
            if (Directory.Exists(rootPath))
            {
                Directory.Delete(rootPath, recursive: true);
            }
        }
    }

    [Fact]
    public async Task OpenProjects_CloseProject_UpdatesOpenProjectList()
    {
        using var factory = new MetricBreakApiFactory();
        using var client = factory.CreateClient();

        var rootPath = Path.Combine(
            Path.GetTempPath(),
            "metricbreak-project-tests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(rootPath);

        try
        {
            Guid projectId;

            using (var createProjectResponse = await client.PostAsJsonAsync(
                       "/api/projects",
                       new
                       {
                           projectName = "Open List Test",
                           rootPath
                       }))
            {
                Assert.Equal(HttpStatusCode.Created, createProjectResponse.StatusCode);

                using var json = await ReadJsonAsync(createProjectResponse);
                projectId = json.RootElement.GetProperty("projectId").GetGuid();
            }

            using (var openListResponse = await client.GetAsync("/api/projects/open"))
            {
                Assert.Equal(HttpStatusCode.OK, openListResponse.StatusCode);

                using var json = await ReadJsonAsync(openListResponse);

                var projectIds = json.RootElement
                    .EnumerateArray()
                    .Select(item => item.GetProperty("projectId").GetGuid())
                    .ToArray();

                Assert.Contains(projectId, projectIds);
            }

            using (var closeResponse = await client.PostAsync(
                       $"/api/projects/{projectId}/close",
                       content: null))
            {
                Assert.Equal(HttpStatusCode.NoContent, closeResponse.StatusCode);
            }

            using var afterCloseResponse = await client.GetAsync("/api/projects/open");

            Assert.Equal(HttpStatusCode.OK, afterCloseResponse.StatusCode);

            using var afterCloseJson = await ReadJsonAsync(afterCloseResponse);

            var remainingProjectIds = afterCloseJson.RootElement
                .EnumerateArray()
                .Select(item => item.GetProperty("projectId").GetGuid())
                .ToArray();

            Assert.DoesNotContain(projectId, remainingProjectIds);
        }
        finally
        {
            if (Directory.Exists(rootPath))
            {
                Directory.Delete(rootPath, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Startup_RestoresLastOpenedProject()
    {
        var rootPath = Path.Combine(
            Path.GetTempPath(),
            "metricbreak-project-tests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(rootPath);

        Guid projectId;

        try
        {
            using (var firstFactory = new MetricBreakApiFactory())
            using (var firstClient = firstFactory.CreateClient())
            {
                using var createProjectResponse = await firstClient.PostAsJsonAsync(
                    "/api/projects",
                    new
                    {
                        projectName = "Restore Startup Test",
                        rootPath
                    });

                Assert.Equal(HttpStatusCode.Created, createProjectResponse.StatusCode);

                using var json = await ReadJsonAsync(createProjectResponse);
                projectId = json.RootElement.GetProperty("projectId").GetGuid();
            }

            using var secondFactory = new MetricBreakApiFactory();
            using var secondClient = secondFactory.CreateClient();

            using var openListResponse = await secondClient.GetAsync("/api/projects/open");

            Assert.Equal(HttpStatusCode.OK, openListResponse.StatusCode);

            using var openListJson = await ReadJsonAsync(openListResponse);

            var projectIds = openListJson.RootElement
                .EnumerateArray()
                .Select(item => item.GetProperty("projectId").GetGuid())
                .ToArray();

            Assert.Contains(projectId, projectIds);

            using var closeResponse = await secondClient.PostAsync(
                $"/api/projects/{projectId}/close",
                content: null);

            Assert.Equal(HttpStatusCode.NoContent, closeResponse.StatusCode);
        }
        finally
        {
            if (Directory.Exists(rootPath))
            {
                Directory.Delete(rootPath, recursive: true);
            }
        }
    }

    [Fact]
    public async Task OpenProject_CorruptManifest_ReturnsStructuredErrorCode()
    {
        using var factory = new MetricBreakApiFactory();
        using var client = factory.CreateClient();

        var rootPath = Path.Combine(
            Path.GetTempPath(),
            "metricbreak-project-tests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(Path.Combine(rootPath, ".metricbreak"));

        var manifestPath = Path.Combine(
            rootPath,
            ".metricbreak",
            "project.mbproj.json");

        try
        {
            await File.WriteAllTextAsync(manifestPath, "{ not valid json }");

            using var response = await client.PostAsJsonAsync(
                "/api/projects/open",
                new { rootPath });

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

            using var json = await ReadJsonAsync(response);

            Assert.Equal(
                "ManifestCorrupt",
                json.RootElement.GetProperty("errorCode").GetString());
        }
        finally
        {
            if (Directory.Exists(rootPath))
            {
                Directory.Delete(rootPath, recursive: true);
            }
        }
    }

    [Fact]
    public async Task OpenProject_UnsupportedSchema_ReturnsStructuredErrorCode()
    {
        using var factory = new MetricBreakApiFactory();
        using var client = factory.CreateClient();

        var rootPath = Path.Combine(
            Path.GetTempPath(),
            "metricbreak-project-tests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(Path.Combine(rootPath, ".metricbreak"));

        var manifestPath = Path.Combine(
            rootPath,
            ".metricbreak",
            "project.mbproj.json");

        try
        {
            await File.WriteAllTextAsync(
                manifestPath,
                """
                {
                  "schemaVersion": 999,
                  "projectId": "00000000-0000-0000-0000-000000000001",
                  "projectName": "Unsupported",
                  "createdAtUtc": "2026-01-01T00:00:00+00:00",
                  "updatedAtUtc": "2026-01-01T00:00:00+00:00",
                  "investigations": []
                }
                """);

            using var response = await client.PostAsJsonAsync(
                "/api/projects/open",
                new { rootPath });

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

            using var json = await ReadJsonAsync(response);

            Assert.Equal(
                "UnsupportedManifestSchema",
                json.RootElement.GetProperty("errorCode").GetString());
        }
        finally
        {
            if (Directory.Exists(rootPath))
            {
                Directory.Delete(rootPath, recursive: true);
            }
        }
    }

    private static MultipartFormDataContent CreateImportContent(
        Guid projectId,
        Guid investigationId,
        string csv,
        string fileName)
    {
        var content = new MultipartFormDataContent();

        content.Add(
            new StringContent(projectId.ToString()),
            "projectId");

        content.Add(
            new StringContent(investigationId.ToString()),
            "investigationId");

        var fileContent = new ByteArrayContent(
            Encoding.UTF8.GetBytes(csv));

        fileContent.Headers.ContentType =
            new MediaTypeHeaderValue("text/csv");

        content.Add(fileContent, "file", fileName);

        return content;
    }

    private static MultipartFormDataContent CreateCsvUploadContent(
        string csv,
        string fileName)
    {
        var content = new MultipartFormDataContent();

        var fileContent = new ByteArrayContent(
            Encoding.UTF8.GetBytes(csv));

        fileContent.Headers.ContentType =
            new MediaTypeHeaderValue("text/csv");

        content.Add(fileContent, "file", fileName);

        return content;
    }

    private static async Task<JsonDocument> ReadJsonAsync(
        HttpResponseMessage response)
    {
        await using var stream =
            await response.Content.ReadAsStreamAsync();

        return await JsonDocument.ParseAsync(stream);
    }
}
