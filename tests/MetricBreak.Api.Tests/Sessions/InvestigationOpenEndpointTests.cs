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