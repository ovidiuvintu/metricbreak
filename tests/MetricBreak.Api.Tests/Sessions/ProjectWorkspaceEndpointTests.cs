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
