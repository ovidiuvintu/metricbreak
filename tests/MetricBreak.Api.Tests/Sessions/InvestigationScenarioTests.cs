using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace MetricBreak.Api.Tests.Sessions;

/// <summary>
/// The investigation a user can demonstrate:
/// open a CSV, select channels, break, run to the hit, step backward,
/// see what changed, bookmark it, save, close, and resume at that state.
/// </summary>
public sealed class InvestigationScenarioTests
{
    private const string Csv =
        "Timestamp,Pressure,Rate\n" +
        "2026-11-08T10:00:00Z,100,1\n" +
        "2026-11-08T10:00:01Z,100,5\n" +
        "2026-11-08T10:00:02Z,200,5\n" +
        "2026-11-08T10:00:03Z,210,5\n";

    [Fact]
    public async Task SavedInvestigation_ResumesTheBreakpointFindingAfterRestart()
    {
        var rootPath = Path.Combine(
            Path.GetTempPath(),
            "metricbreak-scenario-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(rootPath);

        Guid projectId;
        Guid investigationId;
        Guid breakpointId;

        try
        {
            using (var factory = new MetricBreakApiFactory())
            using (var client = factory.CreateClient())
            {
                using (var createProject = await client.PostAsJsonAsync(
                           "/api/projects",
                           new { projectName = "Pressure rise", rootPath }))
                {
                    Assert.Equal(HttpStatusCode.Created, createProject.StatusCode);
                    using var json = await ReadJsonAsync(createProject);
                    projectId = json.RootElement.GetProperty("projectId").GetGuid();
                }

                Guid sessionId;
                using (var created = await client.PostAsync(
                           "/api/sessions",
                           CsvUpload(Csv, "pressure.csv")))
                {
                    var body = await created.Content.ReadAsStringAsync();
                    Assert.True(created.StatusCode == HttpStatusCode.Created, body);
                    using var json = JsonDocument.Parse(body);
                    sessionId = json.RootElement.GetProperty("sessionId").GetGuid();
                }

                Guid pressureId;
                Guid rateId;
                using (var channelsResponse = await client.GetAsync(
                           $"/api/sessions/{sessionId}/channels"))
                {
                    using var channels = await ReadJsonAsync(channelsResponse);
                    pressureId = ChannelId(channels.RootElement, "Pressure");
                    rateId = ChannelId(channels.RootElement, "Rate");
                }

                using (var breakpoint = await client.PostAsJsonAsync(
                           $"/api/sessions/{sessionId}/breakpoints",
                           new
                           {
                               channelId = pressureId,
                               @operator = "GreaterThan",
                               threshold = 150d,
                           }))
                {
                    var body = await breakpoint.Content.ReadAsStringAsync();
                    Assert.True(breakpoint.StatusCode == HttpStatusCode.Created, body);
                    using var json = JsonDocument.Parse(body);
                    breakpointId = json.RootElement.GetProperty("breakpointId").GetGuid();
                }

                long hitSequence;
                using (var continued = await client.PostAsync(
                           $"/api/sessions/{sessionId}/continue",
                           content: null))
                {
                    var body = await continued.Content.ReadAsStringAsync();
                    Assert.True(continued.StatusCode == HttpStatusCode.OK, body);
                    using var json = JsonDocument.Parse(body);
                    Assert.True(json.RootElement.GetProperty("hitBreakpoint").GetBoolean());
                    hitSequence = json.RootElement
                        .GetProperty("stop")
                        .GetProperty("sequence")
                        .GetInt64();
                    Assert.Equal(2, hitSequence);
                }

                using (var stepped = await client.PostAsync(
                           $"/api/sessions/{sessionId}/step-backward",
                           content: null))
                {
                    var body = await stepped.Content.ReadAsStringAsync();
                    Assert.True(stepped.StatusCode == HttpStatusCode.OK, body);
                    using var json = JsonDocument.Parse(body);
                    Assert.Equal(
                        1,
                        json.RootElement
                            .GetProperty("session")
                            .GetProperty("currentSample")
                            .GetProperty("sequence")
                            .GetInt64());
                }

                using (var analysis = await client.GetAsync(
                           $"/api/sessions/{sessionId}/analysis/event?sequence={hitSequence}&lookback=2"))
                {
                    var body = await analysis.Content.ReadAsStringAsync();
                    Assert.True(analysis.StatusCode == HttpStatusCode.OK, body);
                    using var json = JsonDocument.Parse(body);
                    var change = Assert.Single(
                        json.RootElement.GetProperty("immediateChanges").EnumerateArray().ToArray());
                    Assert.Equal("Pressure", change.GetProperty("name").GetString());
                    Assert.Equal(100, change.GetProperty("previous").GetProperty("numericValue").GetDouble());
                    Assert.Equal(200, change.GetProperty("current").GetProperty("numericValue").GetDouble());
                    Assert.Equal(1, change.GetProperty("previousSequence").GetInt64());

                    var firstMove = json.RootElement.GetProperty("firstMoves").EnumerateArray().First();
                    Assert.Equal("Rate", firstMove.GetProperty("name").GetString());
                    Assert.Equal(1, firstMove.GetProperty("firstChangeSequence").GetInt64());
                }

                using (var exported = await client.PostAsJsonAsync(
                           $"/api/sessions/{sessionId}/export?projectId={projectId}",
                           new
                           {
                               name = "Pressure crossed 150",
                               description = "Rate moved before pressure crossed 150.",
                               uiState = new
                               {
                                   selectedChannelIds = new[] { pressureId, rateId },
                                   rightAxisChannelIds = Array.Empty<Guid>(),
                                   selectedChannelNames = new[] { "Pressure", "Rate" },
                                   rightAxisChannelNames = Array.Empty<string>(),
                               },
                               breakpointHit = new
                               {
                                   sequenceNumber = hitSequence,
                                   breakpointId,
                                   label = "Pressure GreaterThan 150",
                               },
                           }))
                {
                    var body = await exported.Content.ReadAsStringAsync();
                    Assert.True(exported.StatusCode == HttpStatusCode.Created, body);
                    using var json = JsonDocument.Parse(body);
                    investigationId = json.RootElement.GetProperty("investigationId").GetGuid();
                }

                using (var bookmark = await client.PostAsJsonAsync(
                           $"/api/investigations/{investigationId}/bookmarks?projectId={projectId}",
                           new
                           {
                               sampleSequence = 1L,
                               timestampNanoseconds = Timestamp(1),
                               title = "Before sample 2",
                               annotation = "Pressure: 100 → 200",
                           }))
                {
                    var body = await bookmark.Content.ReadAsStringAsync();
                    Assert.True(bookmark.StatusCode == HttpStatusCode.Created, body);
                }

                using var closed = await client.DeleteAsync($"/api/sessions/{sessionId}");
                Assert.Equal(HttpStatusCode.NoContent, closed.StatusCode);
            }

            using var restarted = new MetricBreakApiFactory();
            using var resumed = restarted.CreateClient();

            using (var openedProject = await resumed.PostAsJsonAsync(
                       "/api/projects/open",
                       new { rootPath }))
            {
                var body = await openedProject.Content.ReadAsStringAsync();
                Assert.True(openedProject.StatusCode == HttpStatusCode.OK, body);
            }

            Guid resumedSessionId;
            using (var opened = await resumed.PostAsync(
                       $"/api/investigations/{investigationId}/open?projectId={projectId}",
                       content: null))
            {
                var body = await opened.Content.ReadAsStringAsync();
                Assert.True(opened.StatusCode == HttpStatusCode.Created, body);
                using var json = JsonDocument.Parse(body);
                Assert.Equal(1, json.RootElement.GetProperty("sequenceNumber").GetInt64());
                resumedSessionId = json.RootElement.GetProperty("sessionId").GetGuid();
            }

            using (var documentResponse = await resumed.GetAsync(
                       $"/api/investigations/{investigationId}?projectId={projectId}"))
            {
                var body = await documentResponse.Content.ReadAsStringAsync();
                Assert.True(documentResponse.StatusCode == HttpStatusCode.OK, body);
                using var json = JsonDocument.Parse(body);
                var root = json.RootElement;

                Assert.Equal(1, root.GetProperty("session").GetProperty("sequenceNumber").GetInt64());
                Assert.Equal(
                    2,
                    root.GetProperty("session")
                        .GetProperty("breakpointHit")
                        .GetProperty("sequenceNumber")
                        .GetInt64());
                Assert.Equal(
                    "Pressure crossed 150",
                    root.GetProperty("metadata").GetProperty("name").GetString());

                var names = root.GetProperty("uiState")
                    .GetProperty("selectedChannelNames")
                    .EnumerateArray()
                    .Select(item => item.GetString())
                    .ToArray();
                Assert.Equal(new[] { "Pressure", "Rate" }, names);

                var savedBreakpoint = Assert.Single(
                    root.GetProperty("breakpoints").EnumerateArray().ToArray());
                Assert.Equal("Pressure", savedBreakpoint.GetProperty("channelName").GetString());
                Assert.Equal("GreaterThan", savedBreakpoint.GetProperty("operator").GetString());
                Assert.Equal(150, savedBreakpoint.GetProperty("threshold").GetDouble());
            }

            using (var bookmarks = await resumed.GetAsync(
                       $"/api/investigations/{investigationId}/bookmarks?projectId={projectId}"))
            {
                using var json = await ReadJsonAsync(bookmarks);
                var bookmark = Assert.Single(
                    json.RootElement.GetProperty("bookmarks").EnumerateArray().ToArray());
                Assert.Equal("Before sample 2", bookmark.GetProperty("title").GetString());
                Assert.Equal(1, bookmark.GetProperty("sampleSequence").GetInt64());
                Assert.Equal("Pressure: 100 → 200", bookmark.GetProperty("annotation").GetString());
            }

            using (var session = await resumed.GetAsync($"/api/sessions/{resumedSessionId}"))
            {
                using var json = await ReadJsonAsync(session);
                Assert.Equal(
                    1,
                    json.RootElement
                        .GetProperty("currentSample")
                        .GetProperty("sequence")
                        .GetInt64());
            }

            using (var channelsResponse = await resumed.GetAsync(
                       $"/api/sessions/{resumedSessionId}/channels"))
            {
                using var channels = await ReadJsonAsync(channelsResponse);
                Assert.NotEqual(Guid.Empty, ChannelId(channels.RootElement, "Pressure"));
                Assert.NotEqual(Guid.Empty, ChannelId(channels.RootElement, "Rate"));
            }

            using (var breakpoints = await resumed.GetAsync(
                       $"/api/sessions/{resumedSessionId}/breakpoints"))
            {
                using var json = await ReadJsonAsync(breakpoints);
                var restored = Assert.Single(
                    json.RootElement.GetProperty("breakpoints").EnumerateArray().ToArray());
                Assert.Equal("GreaterThan", restored.GetProperty("operator").GetString());
                Assert.Equal(150, restored.GetProperty("threshold").GetDouble());
            }

            using (var analysis = await resumed.GetAsync(
                       $"/api/sessions/{resumedSessionId}/analysis/event?sequence=2&lookback=2"))
            {
                var body = await analysis.Content.ReadAsStringAsync();
                Assert.True(analysis.StatusCode == HttpStatusCode.OK, body);
                using var json = JsonDocument.Parse(body);
                var change = Assert.Single(
                    json.RootElement.GetProperty("immediateChanges").EnumerateArray().ToArray());
                Assert.Equal("Pressure", change.GetProperty("name").GetString());
                Assert.Equal(100, change.GetProperty("previous").GetProperty("numericValue").GetDouble());
                Assert.Equal(200, change.GetProperty("current").GetProperty("numericValue").GetDouble());
            }
        }
        finally
        {
            if (Directory.Exists(rootPath))
                Directory.Delete(rootPath, recursive: true);
        }
    }

    private static Guid ChannelId(JsonElement channels, string name)
    {
        foreach (var channel in channels.EnumerateArray())
        {
            if (channel.GetProperty("name").GetString() == name)
                return channel.GetProperty("channelId").GetGuid();
        }

        throw new InvalidOperationException($"Channel '{name}' was not imported.");
    }

    private static long Timestamp(int secondsPastStart)
    {
        var start = DateTimeOffset.Parse("2026-11-08T10:00:00Z");
        return (start + TimeSpan.FromSeconds(secondsPastStart)).ToUnixTimeMilliseconds() * 1_000_000L;
    }

    private static MultipartFormDataContent CsvUpload(string csv, string fileName)
    {
        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        content.Add(file, "file", fileName);
        return content;
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        await using var stream = await response.Content.ReadAsStreamAsync();
        return await JsonDocument.ParseAsync(stream);
    }
}
