namespace MetricBreak.Api.Breakpoints;

public sealed record CreateTimelineBreakpointRequest(
    string Kind,
    string? Name = null);
