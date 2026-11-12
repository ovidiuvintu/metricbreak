using MetricBreak.Api.Sessions;

namespace MetricBreak.Api;

internal static class ChannelEndpointMappings
{
    public static void Map(WebApplication app)
    {
        var group = app.MapGroup(
            "/api/sessions/{sessionId:guid}/channels")
            .WithTags("Channels");

        group.MapGet("", (
            Guid sessionId,
            ReplaySessionManager manager) =>
        {
            if (!manager.TryGetChannels(sessionId, out var channels))
                return Results.NotFound();

            return Results.Ok(channels);
        })
        .WithName("GetSessionChannels")
        .Produces<IReadOnlyList<ChannelMetadataResponse>>(200)
        .Produces(404);
    }
}
