using MetricBreak.Domain.Recordings;

namespace MetricBreak.Domain.Breakpoints;

/// <summary>Evaluates transitions between adjacent samples in recorded order.</summary>
public static class EdgeConditionEvaluator
{
    public static bool Evaluate(ComparisonOperator op, ChannelValue? previous,
        ChannelValue? current, double threshold = 0)
    {
        if (previous is null || current is null) return false;
        return (previous, current) switch
        {
            (ChannelValue.Numeric a, ChannelValue.Numeric b)
                when double.IsFinite(a.Value) && double.IsFinite(b.Value) => op switch
                {
                    ComparisonOperator.Changed => a.Value != b.Value,
                    ComparisonOperator.RisingEdge => a.Value <= threshold && b.Value > threshold,
                    ComparisonOperator.FallingEdge => a.Value >= threshold && b.Value < threshold,
                    _ => false
                },
            (ChannelValue.Boolean a, ChannelValue.Boolean b) => op switch
            {
                ComparisonOperator.Changed => a.Value != b.Value,
                ComparisonOperator.RisingEdge => !a.Value && b.Value,
                ComparisonOperator.FallingEdge => a.Value && !b.Value,
                _ => false
            },
            (ChannelValue.Categorical a, ChannelValue.Categorical b) =>
                op == ComparisonOperator.Changed &&
                !string.Equals(a.Value, b.Value, StringComparison.Ordinal),
            _ => false
        };
    }
}
