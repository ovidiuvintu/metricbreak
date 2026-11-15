namespace MetricBreak.Api.Sessions;

public sealed record ChannelMetadataResponse(
    Guid ChannelId,
    string Name,
    string? Unit)
{
    public string ValueType { get; init; } = "Numeric";
}
