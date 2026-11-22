namespace MetricBreak.Api.Breakpoints;

public sealed record CreateMissingDataBreakpointRequest(
    double MinimumSeconds,
    Guid? ChannelId = null);
