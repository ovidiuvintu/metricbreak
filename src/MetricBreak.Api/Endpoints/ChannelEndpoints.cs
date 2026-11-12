
using MetricBreak.Api.Sessions;

namespace MetricBreak.Api.Endpoints;

internal static class ChannelEndpoints
{
    private const string _channelTag = "Channels";
    public static IEndpointRouteBuilder MapChannelEndpoints(
        this IEndpointRouteBuilder app)
    {
        var group = app
            .MapGroup("/api/sessions/{sessionId:guid}/channels")
            .WithTags(_channelTag);

        // GET ALL CHANNELS FOR A SESSION
        group.MapGet(
            "",
            (
                Guid sessionId,
                ReplaySessionManager manager) =>
            {
                if (!manager.TryGetChannels(
                        sessionId,
                        out var channels) ||
                    channels is null)
                {
                    return Results.NotFound(new
                    {
                        Error = "Replay session not found."
                    });
                }

                return Results.Ok(channels);
            })
            .WithName("GetSessionChannels")
            .Produces<IReadOnlyList<ChannelMetadataResponse>>(
                StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }
}
