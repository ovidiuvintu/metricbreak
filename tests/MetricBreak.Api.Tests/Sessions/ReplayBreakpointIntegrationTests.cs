
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace MetricBreak.Api.Tests.Sessions;

public sealed class ReplayBreakpointIntegrationTests
{
    // Zero-based sequences: 0 through 7.
    private static readonly double[] _values =
    [
        5, 10, 25, 26, 30, 25, 40, 10
    ];

    [Fact]
    public async Task Continue_StopsAtFirstQualifyingSample()
    {
        using var context = await CreateSessionAsync();
        await AddBreakpointAsync(context, 25);

        using var result = await ContinueAsync(context);

        AssertBreakpointHit(result, expectedSequence: 3);
        Assert.Equal(26, ObservedValue(result));
    }

    [Fact]
    public async Task Continue_Twice_AdvancesToNextBreakpoint()
    {
        using var context = await CreateSessionAsync();
        await AddBreakpointAsync(context, 25);

        using var first = await ContinueAsync(context);
        using var second = await ContinueAsync(context);

        AssertBreakpointHit(first, expectedSequence: 3);
        AssertBreakpointHit(second, expectedSequence: 4);
    }

    [Fact]
    public async Task ContinueBackward_FindsNearestPreviousBreakpoint()
    {
        using var context = await CreateSessionAsync();
        await AddBreakpointAsync(context, 25);

        await SeekAsync(context, 7);

        using var result = await ContinueBackwardAsync(context);

        AssertBreakpointHit(result, expectedSequence: 6);
        Assert.Equal(40, ObservedValue(result));
    }

    [Fact]
    public async Task ContinueBackward_Twice_DoesNotRepeatSample()
    {
        using var context = await CreateSessionAsync();
        await AddBreakpointAsync(context, 25);

        await SeekAsync(context, 7);

        using var first = await ContinueBackwardAsync(context);
        using var second = await ContinueBackwardAsync(context);

        AssertBreakpointHit(first, expectedSequence: 6);
        AssertBreakpointHit(second, expectedSequence: 4);
    }

    [Fact]
    public async Task GreaterThan_DoesNotMatchEqualThreshold()
    {
        using var context = await CreateSessionAsync();
        await AddBreakpointAsync(context, 25);

        using var result = await ContinueAsync(context);

        // Sequence 2 has exactly 25 and must be skipped.
        AssertBreakpointHit(result, expectedSequence: 3);
    }

    [Fact]
    public async Task Continue_WithNoMatchingSamples_ReachesEnd()
    {
        using var context = await CreateSessionAsync();
        await AddBreakpointAsync(context, 100);

        using var result = await ContinueAsync(context);

        Assert.True(result.RootElement.GetProperty("executed").GetBoolean());
        Assert.False(result.RootElement.GetProperty("hitBreakpoint").GetBoolean());
        Assert.True(result.RootElement
            .GetProperty("reachedEndOfRecording").GetBoolean());
    }

    [Fact]
    public async Task ContinueBackward_WithNoEarlierMatch_ReachesStart()
    {
        using var context = await CreateSessionAsync();
        await AddBreakpointAsync(context, 100);

        await SeekAsync(context, 7);

        using var result = await ContinueBackwardAsync(context);

        Assert.True(result.RootElement.GetProperty("executed").GetBoolean());
        Assert.False(result.RootElement.GetProperty("hitBreakpoint").GetBoolean());
        Assert.True(result.RootElement
            .GetProperty("reachedStartOfRecording").GetBoolean());
    }

    private static void AssertBreakpointHit(
        JsonDocument result,
        long expectedSequence)
    {
        var root = result.RootElement;
        var stop = root.GetProperty("stop");
        var session = root.GetProperty("session");

        Assert.True(root.GetProperty("executed").GetBoolean());
        Assert.True(root.GetProperty("hitBreakpoint").GetBoolean());
        Assert.False(root.GetProperty("reachedEndOfRecording").GetBoolean());

        Assert.Equal(
            expectedSequence,
            stop.GetProperty("sequence").GetInt64());

        Assert.Equal(
            expectedSequence,
            session.GetProperty("currentSample")
                .GetProperty("sequence").GetInt64());

        Assert.Equal(
            "Paused",
            session.GetProperty("executionState").GetString());

        Assert.Equal(
            "Breakpoint",
            session.GetProperty("pauseReason").GetString());

        Assert.Equal(
            stop.GetProperty("timestampNanoseconds").ToString(),
            session.GetProperty("currentSample")
                .GetProperty("timestampNanoseconds").ToString());

        Assert.NotEmpty(stop.GetProperty("hits").EnumerateArray());
    }

    private static double ObservedValue(JsonDocument result) =>
        result.RootElement
            .GetProperty("stop")
            .GetProperty("hits")[0]
            .GetProperty("observedValue")
            .GetDouble();

    private static async Task<TestContext> CreateSessionAsync()
    {
        var factory = new MetricBreakApiFactory();
        HttpClient? client = null;

        try
        {
            client = factory.CreateClient();

            using var content = new MultipartFormDataContent();

            // Adjust BuildCsv() to your importer's actual CSV schema.
            var csv = BuildCsv();

            var file = new ByteArrayContent(
                Encoding.UTF8.GetBytes(csv));

            file.Headers.ContentType =
                new System.Net.Http.Headers.MediaTypeHeaderValue(
                    "text/csv");

            content.Add(file, "file", "breakpoint-test.csv");

            using var response = await client.PostAsync(
                "/api/sessions", content);

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);

            using var body = await ReadJsonAsync(response);

            var sessionId = body.RootElement
                .GetProperty("sessionId").GetGuid();

            using var channelsResponse = await client.GetAsync(
                $"/api/sessions/{sessionId}/channels");

            Assert.Equal(HttpStatusCode.OK, channelsResponse.StatusCode);

            using var channels = await ReadJsonAsync(channelsResponse);

            // Assumes the channel endpoint returns a JSON array
            // with a "name" and "channelId" for each channel.
            var channel = channels.RootElement
                .EnumerateArray()
                .Single(x => x.GetProperty("name").GetString() == "value");

            var channelId = channel.GetProperty("channelId").GetGuid();

            return new TestContext(factory, client, sessionId, channelId);
        }
        catch
        {
            client?.Dispose();
            factory.Dispose();
            throw;
        }
    }

    private static string BuildCsv()
    {
        var builder = new StringBuilder();

        // Example schema. Match this to CsvSessionImportService.
        builder.AppendLine("Timestamp,value");

        for (var i = 0; i < _values.Length; i++)
        {
            var timestamp = DateTimeOffset
                .Parse("2026-11-10T12:00:00Z")
                .AddSeconds(i)
                .ToString("O");

            builder.AppendLine(
                $"{timestamp},{_values[i].ToString(
                    System.Globalization.CultureInfo.InvariantCulture)}");
        }

        return builder.ToString();
    }

    private static async Task AddBreakpointAsync(
        TestContext context,
        double threshold)
    {
        using var response = await context.Client.PostAsJsonAsync(
            $"/api/sessions/{context.SessionId}/breakpoints",
            new
            {
                channelId = context.ChannelId,
                @operator = "GreaterThan",
                threshold
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static async Task SeekAsync(
        TestContext context,
        long sequence)
    {
        using var response = await context.Client.PostAsJsonAsync(
            $"/api/sessions/{context.SessionId}/seek",
            new { sequenceNumber = sequence });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static Task<JsonDocument> ContinueAsync(TestContext context) =>
        PostAndReadAsync(
            context.Client,
            $"/api/sessions/{context.SessionId}/continue");

    private static Task<JsonDocument> ContinueBackwardAsync(
        TestContext context) =>
        PostAndReadAsync(
            context.Client,
            $"/api/sessions/{context.SessionId}/continue-backward");

    private static async Task<JsonDocument> PostAndReadAsync(
        HttpClient client,
        string path)
    {
        using var response = await client.PostAsync(path, null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return await ReadJsonAsync(response);
    }

    private static async Task<JsonDocument> ReadJsonAsync(
        HttpResponseMessage response)
    {
        var stream = await response.Content.ReadAsStreamAsync();
        return await JsonDocument.ParseAsync(stream);
    }

    private sealed class TestContext : IDisposable
    {
        private readonly MetricBreakApiFactory _factory;

        public TestContext(
            MetricBreakApiFactory factory,
            HttpClient client,
            Guid sessionId,
            Guid channelId)
        {
            _factory = factory;
            Client = client;
            SessionId = sessionId;
            ChannelId = channelId;
        }

        public HttpClient Client { get; }
        public Guid SessionId { get; }
        public Guid ChannelId { get; }

        public void Dispose()
        {
            Client.Dispose();
            _factory.Dispose();
        }
    }
}
