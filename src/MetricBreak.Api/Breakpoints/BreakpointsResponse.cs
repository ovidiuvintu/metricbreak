namespace MetricBreak.Api.Breakpoints;

public sealed record BreakpointsResponse(
    IReadOnlyList<BreakpointResponse> Breakpoints);
