using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace MetricBreak.Api.Tests.Sessions;

public sealed class CompositeBreakpointEndpointTests
{
    [Theory]
    [InlineData("And")]
    [InlineData("Or")]
    public async Task CreateComposite_ValidRequest_ReturnsCreated(
        string logicalOperator)
    {
        using var factory = new MetricBreakApiFactory();
        using var client = factory.CreateClient();

        var session = await CreateTestSessionAsync(client);

        using var response = await client.PostAsJsonAsync(
            CompositeUrl(session.SessionId),
            CreateRequest(session, logicalOperator));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var json = await ReadJsonAsync(response);
        var root = json.RootElement;

        Assert.NotEqual(
            Guid.Empty,
            root.GetProperty("breakpointId").GetGuid());

        Assert.Equal(
            logicalOperator,
            root.GetProperty("logicalOperator").GetString());

        Assert.Equal(
            2,
            root.GetProperty("conditions").GetArrayLength());

        Assert.True(root.GetProperty("isEnabled").GetBoolean());

        Assert.NotNull(response.Headers.Location);
    }

    [Fact]
    public async Task CreateComposite_InvalidLogicalOperator_ReturnsBadRequest()
    {
        using var factory = new MetricBreakApiFactory();
        using var client = factory.CreateClient();

        var session = await CreateTestSessionAsync(client);

        using var response = await client.PostAsJsonAsync(
            CompositeUrl(session.SessionId),
            CreateRequest(session, "Xor"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateComposite_OneCondition_ReturnsBadRequest()
    {
        using var factory = new MetricBreakApiFactory();
        using var client = factory.CreateClient();

        var session = await CreateTestSessionAsync(client);

        using var response = await client.PostAsJsonAsync(
            CompositeUrl(session.SessionId),
            CreateRequest(session, conditionCount: 1));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateComposite_InvalidComparisonOperator_ReturnsBadRequest()
    {
        using var factory = new MetricBreakApiFactory();
        using var client = factory.CreateClient();

        var session = await CreateTestSessionAsync(client);

        var request = new
        {
            logicalOperator = "And",
            conditions = new object[]
            {
                new
                {
                    channelId = session.PressureId,
                    @operator = "InvalidOperator",
                    threshold = 25.0
                },
                new
                {
                    channelId = session.RateId,
                    @operator = "LessThan",
                    threshold = 20.0
                }
            }
        };

        using var response = await client.PostAsJsonAsync(
            CompositeUrl(session.SessionId),
            request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateComposite_UnknownChannel_ReturnsBadRequest()
    {
        using var factory = new MetricBreakApiFactory();
        using var client = factory.CreateClient();

        var session = await CreateTestSessionAsync(client);

        var request = new
        {
            logicalOperator = "And",
            conditions = new object[]
            {
                new
                {
                    channelId = Guid.NewGuid(),
                    @operator = "GreaterThan",
                    threshold = 25.0
                },
                new
                {
                    channelId = session.RateId,
                    @operator = "LessThan",
                    threshold = 20.0
                }
            }
        };

        using var response = await client.PostAsJsonAsync(
            CompositeUrl(session.SessionId),
            request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateComposite_UnknownSession_ReturnsNotFound()
    {
        using var factory = new MetricBreakApiFactory();
        using var client = factory.CreateClient();

        var session = await CreateTestSessionAsync(client);

        using var response = await client.PostAsJsonAsync(
            CompositeUrl(Guid.NewGuid()),
            CreateRequest(session));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetBreakpoints_IncludesComposite()
    {
        using var factory = new MetricBreakApiFactory();
        using var client = factory.CreateClient();

        var session = await CreateTestSessionAsync(client);
        var breakpointId = await AddCompositeAsync(client, session);

        using var response = await client.GetAsync(
            BreakpointsUrl(session.SessionId));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = await ReadJsonAsync(response);

        var composites = json.RootElement
            .GetProperty("compositeBreakpoints");

        var composite = Assert.Single(composites.EnumerateArray());

        Assert.Equal(
            breakpointId,
            composite.GetProperty("breakpointId").GetGuid());

        Assert.Equal(
            "And",
            composite.GetProperty("logicalOperator").GetString());

        Assert.Equal(
            2,
            composite.GetProperty("conditions").GetArrayLength());

        Assert.True(composite.GetProperty("isEnabled").GetBoolean());
    }

    [Fact]
    public async Task GetBreakpoints_ConditionalAndComposite_ReturnsBoth()
    {
        using var factory = new MetricBreakApiFactory();
        using var client = factory.CreateClient();

        var session = await CreateTestSessionAsync(client);

        using var conditionalResponse = await client.PostAsJsonAsync(
            BreakpointsUrl(session.SessionId),
            new
            {
                channelId = session.PressureId,
                @operator = "GreaterThan",
                threshold = 25.0
            });

        Assert.Equal(
            HttpStatusCode.Created,
            conditionalResponse.StatusCode);

        var compositeId = await AddCompositeAsync(client, session);

        using var response = await client.GetAsync(
            BreakpointsUrl(session.SessionId));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = await ReadJsonAsync(response);

        Assert.Single(
            json.RootElement
                .GetProperty("breakpoints")
                .EnumerateArray());

        var composites = json.RootElement
            .GetProperty("compositeBreakpoints");

        var composite = Assert.Single(composites.EnumerateArray());

        Assert.Equal(
            compositeId,
            composite.GetProperty("breakpointId").GetGuid());
    }

    [Fact]
    public async Task DisableComposite_UpdatesEnabledState()
    {
        using var factory = new MetricBreakApiFactory();
        using var client = factory.CreateClient();

        var session = await CreateTestSessionAsync(client);
        var breakpointId = await AddCompositeAsync(client, session);

        using var patchResponse = await client.PatchAsJsonAsync(
            $"{BreakpointsUrl(session.SessionId)}/{breakpointId}",
            new { isEnabled = false });

        Assert.Equal(
            HttpStatusCode.NoContent,
            patchResponse.StatusCode);

        using var getResponse = await client.GetAsync(
            BreakpointsUrl(session.SessionId));

        using var json = await ReadJsonAsync(getResponse);

        var composite = Assert.Single(
            json.RootElement
                .GetProperty("compositeBreakpoints")
                .EnumerateArray());

        Assert.False(composite.GetProperty("isEnabled").GetBoolean());
    }

    [Fact]
    public async Task EnableComposite_UpdatesEnabledState()
    {
        using var factory = new MetricBreakApiFactory();
        using var client = factory.CreateClient();

        var session = await CreateTestSessionAsync(client);
        var breakpointId = await AddCompositeAsync(client, session);

        var url = $"{BreakpointsUrl(session.SessionId)}/{breakpointId}";

        using var disableResponse = await client.PatchAsJsonAsync(
            url,
            new { isEnabled = false });

        Assert.Equal(HttpStatusCode.NoContent, disableResponse.StatusCode);

        using var enableResponse = await client.PatchAsJsonAsync(
            url,
            new { isEnabled = true });

        Assert.Equal(HttpStatusCode.NoContent, enableResponse.StatusCode);

        using var getResponse = await client.GetAsync(
            BreakpointsUrl(session.SessionId));

        using var json = await ReadJsonAsync(getResponse);

        var composite = Assert.Single(
            json.RootElement
                .GetProperty("compositeBreakpoints")
                .EnumerateArray());

        Assert.True(composite.GetProperty("isEnabled").GetBoolean());
    }

    [Fact]
    public async Task DeleteComposite_RemovesBreakpoint()
    {
        using var factory = new MetricBreakApiFactory();
        using var client = factory.CreateClient();

        var session = await CreateTestSessionAsync(client);
        var breakpointId = await AddCompositeAsync(client, session);

        using var deleteResponse = await client.DeleteAsync(
            $"{BreakpointsUrl(session.SessionId)}/{breakpointId}");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        using var getResponse = await client.GetAsync(
            BreakpointsUrl(session.SessionId));

        using var json = await ReadJsonAsync(getResponse);

        Assert.Empty(
            json.RootElement
                .GetProperty("compositeBreakpoints")
                .EnumerateArray());
    }

    [Fact]
    public async Task Continue_AndComposite_StopsAtMatchingSample()
    {
        using var factory = new MetricBreakApiFactory();
        using var client = factory.CreateClient();

        var session = await CreateTestSessionAsync(client);
        var breakpointId = await AddCompositeAsync(client, session);

        var url = $"/api/sessions/{session.SessionId}/continue";

        using var response = await client.PostAsync(url, null);

        var responseBody = await response.Content.ReadAsStringAsync();

        var allowedMethods = response.Headers.TryGetValues(
            "Allow", out var methods)
            ? string.Join(", ", methods)
            : "Not provided";

        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Request: POST {url}\n" +
            $"Status: {(int)response.StatusCode} {response.StatusCode}\n" +
            $"Final URI: {response.RequestMessage?.RequestUri}\n" +
            $"Allowed methods: {allowedMethods}\n" +
            $"Response: {responseBody}");

        //using var response = await client.PostAsync(
        //    $"/api/sessions/{session.SessionId}/continue",
        //    null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = await ReadJsonAsync(response);
        var root = json.RootElement;

        Assert.True(root.GetProperty("hitBreakpoint").GetBoolean());

        var stop = root.GetProperty("stop");

        Assert.Equal(
            3,
            stop.GetProperty("sequence").GetInt64());

        var hit = Assert.Single(
            stop.GetProperty("hits").EnumerateArray());

        Assert.Equal(
            breakpointId,
            hit.GetProperty("breakpointId").GetGuid());
    }


    [Fact]
    public async Task Continue_CompositeBreakpoint_SerializesWithoutNonFiniteNumbers()
    {
        using var factory = new MetricBreakApiFactory();
        using var client = factory.CreateClient();

        var session = await CreateTestSessionAsync(client);
        var breakpointId = await AddCompositeAsync(client, session);

        using var response = await client.PostAsync(
            $"/api/sessions/{session.SessionId}/continue",
            null);

        // Regression: composite breakpoint hits previously contained
        // NaN values that System.Text.Json could not serialize.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var responseBody = await response.Content.ReadAsStringAsync();

        using var json = JsonDocument.Parse(responseBody);
        var root = json.RootElement;

        Assert.True(root.GetProperty("hitBreakpoint").GetBoolean());

        var stop = root.GetProperty("stop");
        Assert.Equal(3, stop.GetProperty("sequence").GetInt64());

        var hit = Assert.Single(
            stop.GetProperty("hits").EnumerateArray());

        Assert.Equal(
            breakpointId,
            hit.GetProperty("breakpointId").GetGuid());

        // Reject nonstandard JSON number literals and their
        // string-encoded equivalents anywhere in the response.
        AssertNoNonFiniteNumbers(root);
    }

    private static void AssertNoNonFiniteNumbers(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    AssertNoNonFiniteNumbers(property.Value);
                }
                break;

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    AssertNoNonFiniteNumbers(item);
                }
                break;

            case JsonValueKind.Number:
                Assert.True(
                    double.IsFinite(element.GetDouble()),
                    $"Non-finite JSON number: {element.GetRawText()}");
                break;

            case JsonValueKind.String:
                var value = element.GetString();

                Assert.DoesNotContain(
                    value,
                    new[] { "NaN", "Infinity", "-Infinity" });
                break;
        }
    }


    private static string BreakpointsUrl(Guid sessionId) =>
        $"/api/sessions/{sessionId}/breakpoints";

    private static string CompositeUrl(Guid sessionId) =>
        $"{BreakpointsUrl(sessionId)}/composite";

    private static object CreateRequest(
        SessionFixture session,
        string logicalOperator = "And",
        int conditionCount = 2)
    {
        var conditions = new object[]
        {
            new
            {
                channelId = session.PressureId,
                @operator = "GreaterThan",
                threshold = 25.0
            },
            new
            {
                channelId = session.RateId,
                @operator = "LessThan",
                threshold = 20.0
            }
        };

        return new
        {
            logicalOperator,
            conditions = conditions.Take(conditionCount).ToArray()
        };
    }

    private static async Task<Guid> AddCompositeAsync(
        HttpClient client,
        SessionFixture session)
    {
        using var response = await client.PostAsJsonAsync(
            CompositeUrl(session.SessionId),
            CreateRequest(session));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var json = await ReadJsonAsync(response);

        return json.RootElement
            .GetProperty("breakpointId")
            .GetGuid();
    }

    private static async Task<JsonDocument> ReadJsonAsync(
        HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();

        var stream = await response.Content.ReadAsStreamAsync();

        return await JsonDocument.ParseAsync(stream);
    }

    private static async Task<SessionFixture> CreateTestSessionAsync(
        HttpClient client)
    {
        const string csv = """
        Timestamp,Pressure,Rate
        2026-01-01T00:00:00Z,10,40
        2026-01-01T00:00:01Z,15,35
        2026-01-01T00:00:02Z,25,30
        2026-01-01T00:00:03Z,30,15
        2026-01-01T00:00:04Z,35,10
        2026-01-01T00:00:05Z,20,25
        """;

        using var content = new MultipartFormDataContent();

        var fileContent = new ByteArrayContent(
            Encoding.UTF8.GetBytes(csv));

        content.Add(fileContent, "file", "composite-test.csv");

        using var response = await client.PostAsync(
            "/api/sessions",
            content);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var sessionJson = await ReadJsonAsync(response);

        var sessionId = sessionJson.RootElement
            .GetProperty("sessionId")
            .GetGuid();

        using var channelsResponse = await client.GetAsync(
            $"/api/sessions/{sessionId}/channels");

        Assert.Equal(HttpStatusCode.OK, channelsResponse.StatusCode);

        using var channelsJson = await ReadJsonAsync(channelsResponse);

        var channels = channelsJson.RootElement
            .EnumerateArray()
            .ToArray();

        var pressureId = channels
            .Single(channel =>
                channel.GetProperty("name").GetString() == "Pressure")
            .GetProperty("channelId")
            .GetGuid();

        var rateId = channels
            .Single(channel =>
                channel.GetProperty("name").GetString() == "Rate")
            .GetProperty("channelId")
            .GetGuid();

        return new SessionFixture(
            sessionId,
            pressureId,
            rateId);
    }

    private sealed record SessionFixture(
        Guid SessionId,
        Guid PressureId,
        Guid RateId);
}
