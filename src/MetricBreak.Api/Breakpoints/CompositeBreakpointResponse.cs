namespace MetricBreak.Api.Breakpoints;

/// <summary>
/// API representation of a composite breakpoint
/// containing multiple conditions joined by AND or OR.
/// </summary>
public sealed record CompositeBreakpointResponse(
    Guid BreakpointId,
    string LogicalOperator,
    IReadOnlyList<BreakpointConditionRequest> Conditions,
    bool IsEnabled);
