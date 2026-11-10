namespace MetricBreak.Api.Breakpoints;

public sealed record CreateBreakpointRequest(
    Guid ChannelId,
    string Operator,
    double Threshold);
