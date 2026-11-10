namespace MetricBreak.Api.Sessions;

public sealed record BreakpointResponse(
    Guid BreakpointId,
    Guid ChannelId,
    string Operator,
    double Threshold);
