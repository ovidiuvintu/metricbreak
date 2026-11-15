namespace MetricBreak.Api.Breakpoints;

/// <summary>
/// Request body for creating a composite breakpoint.
/// </summary>
public sealed record CreateCompositeBreakpointRequest(
    string LogicalOperator,
    IReadOnlyList<BreakpointConditionRequest> Conditions);
