using MetricBreak.Domain.Recordings;

namespace MetricBreak.Domain.Breakpoints;

/// <summary>
/// Represents a single channel comparison within a composite breakpoint.
/// </summary>
public sealed record BreakpointCondition
{
    public Guid ChannelId { get; }

    public ComparisonOperator Operator { get; }

    public double Threshold { get; }

    public ChannelValue? ExpectedValue { get; }

    public BreakpointCondition(
        Guid channelId,
        ComparisonOperator comparisonOperator,
        double threshold,
        ChannelValue? expectedValue = null)
    {
        if (channelId == Guid.Empty)
        {
            throw new ArgumentException(
                "Channel ID cannot be empty.",
                nameof(channelId));
        }

        if (!Enum.IsDefined(comparisonOperator))
        {
            throw new ArgumentOutOfRangeException(
                nameof(comparisonOperator));
        }

        if (!double.IsFinite(threshold))
        {
            throw new ArgumentOutOfRangeException(
                nameof(threshold),
                "Threshold must be finite.");
        }

        ChannelId = channelId;
        Operator = comparisonOperator;
        Threshold = threshold;
        ExpectedValue = expectedValue;
    }
}
