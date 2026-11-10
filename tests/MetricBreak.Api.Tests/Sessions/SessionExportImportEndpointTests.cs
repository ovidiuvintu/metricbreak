using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace MetricBreak.Api.Tests.Sessions;

public sealed class SessionExportImportEndpointTests
{
    private const string _csvContent =
        "Timestamp,Pressure,Rate\n" +
        "2026-11-08T10:00:00Z,8500,40\n" +
        "2026-11-08T10:00:01Z,8700,42\n" +
        "2026-11-08T10:00:02Z,9500,45\n";

    private const string _differentCsvContent =
        "Timestamp,Pressure,Rate\n" +
        "2026-11-08T10:00:00Z,100,1\n" +
        "2026-11-08T10:00:01Z,200,2\n" +
        "2026-11-08T10:00:02Z,300,3\n";

    [Fact]
    public async Task ExportThenImport_RestoresPositionAndBreakpointStates()
    {
        using var factory = new MetricBreakApiFactory();
        using var client = factory.CreateClient();

        var (sessionId, pressureChannelId) = await CreateSessionAsync(client);

        using (var activeBreakpoint = await client.PostAsJsonAsync(
                   $"/api/sessions/{sessionId}/breakpoints",
                   new
                   {
                       channelId = pressureChannelId,
                       @operator = "GreaterThan",
                       threshold = 9000d
                   }))
        {
            Assert.Equal(HttpStatusCode.Created, activeBreakpoint.StatusCode);
        }

        Guid disabledBreakpointId;

        using (var inactiveBreakpoint = await client.PostAsJsonAsync(
                   $"/api/sessions/{sessionId}/breakpoints",
                   new
                   {
                       channelId = pressureChannelId,
                       @operator = "LessThan",
                       threshold = 8600d
                   }))
        {
            Assert.Equal(HttpStatusCode.Created, inactiveBreakpoint.StatusCode);

            using var json = await ReadJsonAsync(inactiveBreakpoint);

            disabledBreakpointId = json.RootElement
                .GetProperty("breakpointId")
                .GetGuid();
        }

        using (var disableBreakpoint = await client.PatchAsJsonAsync(
                   $"/api/sessions/{sessionId}/breakpoints/{disabledBreakpointId}",
                   new { isEnabled = false }))
        {
            Assert.Equal(HttpStatusCode.NoContent, disableBreakpoint.StatusCode);
        }

        using (var seekResponse = await client.PostAsJsonAsync(
                   $"/api/sessions/{sessionId}/seek",
                   new { sequenceNumber = 1L }))
        {
            Assert.Equal(HttpStatusCode.OK, seekResponse.StatusCode);
        }

        Guid investigationId;

        using (var exportResponse = await client.PostAsync(
                   $"/api/sessions/{sessionId}/export",
                   content: null))
        {
            Assert.Equal(HttpStatusCode.Created, exportResponse.StatusCode);

            using var json = await ReadJsonAsync(exportResponse);

            investigationId = json.RootElement
                .GetProperty("investigationId")
                .GetGuid();
        }

        Guid importedSessionId;

        using (var importResponse = await client.PostAsync(
                   "/api/sessions/import",
                   CreateImportContent(investigationId, _csvContent, "recording.csv")))
        {
            Assert.Equal(HttpStatusCode.Created, importResponse.StatusCode);

            using var json = await ReadJsonAsync(importResponse);

            importedSessionId = json.RootElement
                .GetProperty("sessionId")
                .GetGuid();
        }

        using (var stateResponse = await client.GetAsync(
                   $"/api/sessions/{importedSessionId}"))
        {
            Assert.Equal(HttpStatusCode.OK, stateResponse.StatusCode);

            using var stateJson = await ReadJsonAsync(stateResponse);

            var root = stateJson.RootElement;

            Assert.Equal(
                1,
                root.GetProperty("currentSample")
                    .GetProperty("sequence")
                    .GetInt64());
        }

        using (var continueResponse = await client.PostAsync(
                   $"/api/sessions/{importedSessionId}/continue",
                   content: null))
        {
            Assert.Equal(HttpStatusCode.OK, continueResponse.StatusCode);

            using var continueJson = await ReadJsonAsync(continueResponse);
            var root = continueJson.RootElement;

            Assert.True(root.GetProperty("hitBreakpoint").GetBoolean());

            var stop = root.GetProperty("stop");

            Assert.Equal(2, stop.GetProperty("sequence").GetInt64());
        }

        using var backwardResponse = await client.PostAsync(
            $"/api/sessions/{importedSessionId}/continue-backward",
            content: null);

        Assert.Equal(HttpStatusCode.OK, backwardResponse.StatusCode);

        using var backwardJson = await ReadJsonAsync(backwardResponse);
        var backwardRoot = backwardJson.RootElement;

        Assert.True(backwardRoot.GetProperty("executed").GetBoolean());
        Assert.False(backwardRoot.GetProperty("hitBreakpoint").GetBoolean());
        Assert.True(backwardRoot.GetProperty("reachedStartOfRecording").GetBoolean());
    }

    [Fact]
    public async Task Import_WithMismatchedRecording_Returns400()
    {
        using var factory = new MetricBreakApiFactory();
        using var client = factory.CreateClient();

        var (sessionId, _) = await CreateSessionAsync(client);

        Guid investigationId;

        using (var exportResponse = await client.PostAsync(
                   $"/api/sessions/{sessionId}/export",
                   content: null))
        {
            Assert.Equal(HttpStatusCode.Created, exportResponse.StatusCode);

            using var json = await ReadJsonAsync(exportResponse);

            investigationId = json.RootElement
                .GetProperty("investigationId")
                .GetGuid();
        }

        using var importResponse = await client.PostAsync(
            "/api/sessions/import",
            CreateImportContent(
                investigationId,
                _differentCsvContent,
                "different.csv"));

        Assert.Equal(HttpStatusCode.BadRequest, importResponse.StatusCode);
    }

    private static MultipartFormDataContent CreateImportContent(
        Guid investigationId,
        string csv,
        string fileName)
    {
        var content = new MultipartFormDataContent();

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

    private static async Task<JsonDocument> ReadJsonAsync(
        HttpResponseMessage response)
    {
        await using var stream =
            await response.Content.ReadAsStreamAsync();

        return await JsonDocument.ParseAsync(stream);
    }

    private static async Task<(Guid SessionId, Guid PressureChannelId)>
        CreateSessionAsync(HttpClient client)
    {
        using var createResponse = await client.PostAsync(
            "/api/sessions",
            CreateCsvUploadContent(_csvContent, "recording.csv"));

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        using var createJson = await ReadJsonAsync(createResponse);

        var sessionId = createJson.RootElement
            .GetProperty("sessionId")
            .GetGuid();

        using var stateResponse = await client.GetAsync(
            $"/api/sessions/{sessionId}");

        Assert.Equal(HttpStatusCode.OK, stateResponse.StatusCode);

        using var stateJson = await ReadJsonAsync(stateResponse);

        var pressureChannel = stateJson.RootElement
            .GetProperty("currentSample")
            .GetProperty("channels")
            .EnumerateObject()
            .Single(property => property.Value.GetDouble() == 8500d)
            .Name;

        return (sessionId, Guid.Parse(pressureChannel));
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
}
