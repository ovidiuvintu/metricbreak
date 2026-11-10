namespace MetricBreak.Domain.Breakpoints;

public sealed record RegisteredBreakpoint(
    ConditionalBreakpoint Breakpoint,
    bool IsEnabled);
