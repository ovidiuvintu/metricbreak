namespace MetricBreak.Domain.Breakpoints;

/// <summary>
/// Represents a snapshot of a registered breakpoint
/// and its enabled state.
///
/// Supports both conditional and composite breakpoints.
/// </summary>
public sealed record RegisteredBreakpointSnapshot(
    IBreakpoint Breakpoint,
    bool IsEnabled);
