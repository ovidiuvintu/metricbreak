
namespace MetricBreak.Api.Sessions;

public sealed record ChannelMetadataResponse(
    Guid ChannelId,
    string Name,
    string? Unit);
