using MetricBreak.Domain.Recordings;

namespace MetricBreak.Domain.Breakpoints;

/// <summary>Compares values without coercing categorical or Boolean telemetry to doubles.</summary>
public static class TypedConditionEvaluator
{
    public static bool Evaluate(Guid channelId, ComparisonOperator op, double threshold,
        ChannelValue? expected, TelemetrySample current, TelemetrySample? previous)
    {
        var value = current.GetTypedValue(channelId);
        if (op is ComparisonOperator.Changed or ComparisonOperator.RisingEdge or ComparisonOperator.FallingEdge)
        {
            // A missing sample or a gap must never fabricate an edge.
            if (previous is null || previous.SequenceNumber + 1 != current.SequenceNumber) return false;
            return EdgeConditionEvaluator.Evaluate(op, previous.GetTypedValue(channelId), value, threshold);
        }
        if (value is null) return false;
        return (value, expected) switch
        {
            (ChannelValue.Numeric n, _) when double.IsFinite(n.Value) => op switch
            {
                ComparisonOperator.GreaterThan => n.Value > threshold,
                ComparisonOperator.GreaterThanOrEqual => n.Value >= threshold,
                ComparisonOperator.LessThan => n.Value < threshold,
                ComparisonOperator.LessThanOrEqual => n.Value <= threshold,
                ComparisonOperator.Equal => n.Value == threshold,
                ComparisonOperator.NotEqual => n.Value != threshold,
                _ => false
            },
            (ChannelValue.Boolean actual, ChannelValue.Boolean target) => op switch
            {
                ComparisonOperator.Equal => actual.Value == target.Value,
                ComparisonOperator.NotEqual => actual.Value != target.Value,
                _ => false
            },
            (ChannelValue.Categorical actual, ChannelValue.Categorical target) => op switch
            {
                ComparisonOperator.Equal => string.Equals(actual.Value, target.Value, StringComparison.Ordinal),
                ComparisonOperator.NotEqual => !string.Equals(actual.Value, target.Value, StringComparison.Ordinal),
                _ => false
            },
            _ => false
        };
    }
}
