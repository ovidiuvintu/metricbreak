using MetricBreak.Domain.Recordings;

namespace MetricBreak.Domain.Breakpoints;

/// <summary>
/// Captures a condition and its observed value
/// when a composite breakpoint matches.
/// </summary>
public sealed record BreakpointConditionHit(
    Guid ChannelId,
    double? ObservedValue,
    ComparisonOperator Operator,
    double Threshold,
    bool Matched)
{
    public ChannelValue? TypedObservedValue { get; init; }

    public ChannelValue? ExpectedValue { get; init; }
}
