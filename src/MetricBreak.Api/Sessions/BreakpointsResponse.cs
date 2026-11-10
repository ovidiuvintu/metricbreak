namespace MetricBreak.Api.Sessions;

public sealed record BreakpointsResponse(
    IReadOnlyList<BreakpointResponse> Breakpoints);
