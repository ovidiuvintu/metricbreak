namespace MetricBreak.Api.Sessions;

public sealed record CreateBreakpointRequest(
    Guid ChannelId,
    string Operator,
    double Threshold);
