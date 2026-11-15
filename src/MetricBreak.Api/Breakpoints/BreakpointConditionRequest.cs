
namespace MetricBreak.Api.Breakpoints;

/// <summary>
/// Defines a single condition within a composite breakpoint request.
/// </summary>
public sealed record BreakpointConditionRequest(
    Guid ChannelId,
    string Operator,
    double Threshold);
