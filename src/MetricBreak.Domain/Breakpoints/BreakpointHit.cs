
using MetricBreak.Domain.Recordings;

namespace MetricBreak.Domain.Breakpoints;

/// <summary>
/// An immutable record of a breakpoint matching a sample.
/// </summary>
public sealed record BreakpointHit
{
    public Guid BreakpointId { get; }
    public SamplePosition Position { get; }
    public TelemetryTimestamp Timestamp { get; }
    public Guid ChannelId { get; }
    public double ObservedValue { get; }
    public ComparisonOperator Operator { get; }
    public double Threshold { get; }

    public BreakpointHit(
        Guid breakpointId,
        SamplePosition position,
        TelemetryTimestamp timestamp,
        Guid channelId,
        double observedValue,
        ComparisonOperator comparisonOperator,
        double threshold)
    {
        if (breakpointId == Guid.Empty)
            throw new ArgumentException(
                "Breakpoint ID cannot be empty.",
                nameof(breakpointId));

        if (position.RecordingId == Guid.Empty)
            throw new ArgumentException(
                "Sample position must identify a recording.",
                nameof(position));

        if (position.SequenceNumber < 0)
            throw new ArgumentOutOfRangeException(
                nameof(position));

        if (channelId == Guid.Empty)
            throw new ArgumentException(
                "Channel ID cannot be empty.",
                nameof(channelId));

        if (!Enum.IsDefined(comparisonOperator))
            throw new ArgumentOutOfRangeException(
                nameof(comparisonOperator));

        if (double.IsNaN(observedValue))
            throw new ArgumentOutOfRangeException(
                nameof(observedValue));

        if (!double.IsFinite(threshold))
            throw new ArgumentOutOfRangeException(
                nameof(threshold));

        BreakpointId = breakpointId;
        Position = position;
        Timestamp = timestamp;
        ChannelId = channelId;
        ObservedValue = observedValue;
        Operator = comparisonOperator;
        Threshold = threshold;
    }
}
