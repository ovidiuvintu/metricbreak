namespace MetricBreak.Api.Breakpoints;

public sealed record BreakpointResponse(
    Guid BreakpointId,
    Guid ChannelId,
    string Operator,
    double Threshold);
