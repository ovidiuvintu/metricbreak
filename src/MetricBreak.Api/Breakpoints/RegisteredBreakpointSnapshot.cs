using MetricBreak.Domain.Breakpoints;

namespace MetricBreak.Api.Sessions;

public sealed record RegisteredBreakpointSnapshot(
    ConditionalBreakpoint Breakpoint,
    bool IsEnabled);
