
namespace MetricBreak.Domain.Breakpoints;

public sealed class ConditionalBreakpoint
{
    public Guid Id { get; }

    public Guid ChannelId { get; }

    public ComparisonOperator Operator { get; }

    public double Threshold { get; }

    public ConditionalBreakpoint(
        Guid id,
        Guid channelId,
        ComparisonOperator comparisonOperator,
        double threshold)
    {
        if (id == Guid.Empty)
            throw new ArgumentException(
                "Breakpoint ID cannot be empty.",
                nameof(id));

        if (channelId == Guid.Empty)
            throw new ArgumentException(
                "Channel ID cannot be empty.",
                nameof(channelId));

        if (!Enum.IsDefined(comparisonOperator))
            throw new ArgumentOutOfRangeException(
                nameof(comparisonOperator));

        if (!double.IsFinite(threshold))
            throw new ArgumentOutOfRangeException(
                nameof(threshold),
                "Threshold must be finite.");

        Id = id;
        ChannelId = channelId;
        Operator = comparisonOperator;
        Threshold = threshold;
    }
}
