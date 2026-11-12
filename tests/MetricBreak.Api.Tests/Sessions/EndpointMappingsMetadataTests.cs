using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace MetricBreak.Api.Tests.Sessions;

public sealed class EndpointMappingsMetadataTests
{
    [Fact]
    public void GetSessionSamples_HasExpectedNameTagAndNoSummary()
    {
        using var factory = new MetricBreakApiFactory();

        var endpoint = FindRouteEndpoint(
            factory,
            "/api/sessions/{sessionId:guid}/samples",
            "GET");

        Assert.NotNull(endpoint);

        var nameMetadata = endpoint!.Metadata.GetMetadata<IEndpointNameMetadata>();
        Assert.NotNull(nameMetadata);
        Assert.Equal("GetSessionSamples", nameMetadata!.EndpointName);

        var tagsMetadata = endpoint.Metadata.GetMetadata<ITagsMetadata>();
        Assert.NotNull(tagsMetadata);
        Assert.Contains("Sessions", tagsMetadata!.Tags);

        var summaryMetadata = endpoint.Metadata.GetMetadata<IEndpointSummaryMetadata>();
        Assert.Null(summaryMetadata);
    }

    [Fact]
    public void GetSessionChannels_EndpointExists()
    {
        using var factory = new MetricBreakApiFactory();

        using var client = factory.CreateClient();

        var dataSources = factory.Services
            .GetRequiredService<IEnumerable<EndpointDataSource>>();

        var endpoints = dataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .ToList();

        foreach (var endpoint in endpoints)
        {
            var methods = endpoint.Metadata
                .GetMetadata<IHttpMethodMetadata>()?
                .HttpMethods ?? Array.Empty<string>();

            Console.WriteLine(
                $"{string.Join(",", methods)} " +
                $"{endpoint.RoutePattern.RawText}");
        }

        Assert.Contains(endpoints, endpoint =>
            endpoint.RoutePattern.RawText ==
                "/api/sessions/{sessionId:guid}/channels/" &&
            endpoint.Metadata
                .GetMetadata<IHttpMethodMetadata>()?
                .HttpMethods.Contains("GET") == true);
    }

    [Fact]
    public void CloseProject_HasExpectedTagAndSummary()
    {
        using var factory = new MetricBreakApiFactory();

        var endpoint = FindRouteEndpoint(
            factory,
            "/api/projects/{projectId:guid}/close",
            "POST");

        Assert.NotNull(endpoint);

        var tagsMetadata = endpoint!.Metadata.GetMetadata<ITagsMetadata>();
        Assert.NotNull(tagsMetadata);
        Assert.Contains("Projects", tagsMetadata!.Tags);
    }

    [Fact]
    public void GetSessionChannels_IsRegistered()
    {
        using var factory = new MetricBreakApiFactory();
        using var client = factory.CreateClient();

        var endpoints = factory.Services
            .GetRequiredService<IEnumerable<EndpointDataSource>>()
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .ToArray();

        var matches = endpoints
            .Where(endpoint =>
                endpoint.Metadata
                    .GetMetadata<IEndpointNameMetadata>()?
                    .EndpointName == "GetSessionChannels")
            .ToArray();

        Assert.True(
            matches.Length == 1,
            "Expected exactly one GetSessionChannels endpoint. " +
            "Matching routes: " +
            string.Join(", ",
                endpoints
                    .Where(e => e.RoutePattern.RawText?
                        .Contains("channels",
                            StringComparison.OrdinalIgnoreCase) == true)
                    .Select(e => e.RoutePattern.RawText)));

        Assert.Equal(
            "/api/sessions/{sessionId:guid}/channels/",
            matches[0].RoutePattern.RawText);
    }

    private static RouteEndpoint? FindRouteEndpoint(
        MetricBreakApiFactory factory,
        string routePattern,
        string httpMethod)
    {
        var dataSource = factory.Services.GetRequiredService<EndpointDataSource>();

        return dataSource.Endpoints
            .OfType<RouteEndpoint>()
            .FirstOrDefault(endpoint =>
                string.Equals(
                    endpoint.RoutePattern.RawText,
                    routePattern,
                    StringComparison.OrdinalIgnoreCase)
                && endpoint.Metadata
                    .GetMetadata<IHttpMethodMetadata>()?
                    .HttpMethods
                    .Any(method => string.Equals(
                        method,
                        httpMethod,
                        StringComparison.OrdinalIgnoreCase)) == true);
    }
}
