using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace MetricBreak.Api.Tests.Sessions;

public sealed class EndpointMappingsMetadataTests
{
    [Fact]
    public void GetSessionSamples_HasExpectedNameTagAndSummary()
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
        Assert.NotNull(summaryMetadata);
        Assert.Equal(
            "Retrieve a range of recording samples",
            summaryMetadata!.Summary);
    }

    [Fact]
    public void GetSessionChannels_HasExpectedNameTagAndSummary()
    {
        using var factory = new MetricBreakApiFactory();

        var endpoint = FindRouteEndpoint(
            factory,
            "/api/sessions/{sessionId:guid}/channels",
            "GET");

        Assert.NotNull(endpoint);

        var nameMetadata = endpoint!.Metadata.GetMetadata<IEndpointNameMetadata>();
        Assert.NotNull(nameMetadata);
        Assert.Equal("GetSessionChannels", nameMetadata!.EndpointName);

        var tagsMetadata = endpoint.Metadata.GetMetadata<ITagsMetadata>();
        Assert.NotNull(tagsMetadata);
        Assert.Contains("Sessions", tagsMetadata!.Tags);

        var summaryMetadata = endpoint.Metadata.GetMetadata<IEndpointSummaryMetadata>();
        Assert.NotNull(summaryMetadata);
        Assert.Equal(
            "List channels for a replay session",
            summaryMetadata!.Summary);
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

        var summaryMetadata = endpoint.Metadata.GetMetadata<IEndpointSummaryMetadata>();
        Assert.NotNull(summaryMetadata);
        Assert.Equal("Close an open project", summaryMetadata!.Summary);
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
