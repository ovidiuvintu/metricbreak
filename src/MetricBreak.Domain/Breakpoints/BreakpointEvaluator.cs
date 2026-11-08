
using MetricBreak.Domain.Recordings;

namespace MetricBreak.Domain.Breakpoints;

public static class BreakpointEvaluator
{
    public static bool Evaluate(
        ConditionalBreakpoint breakpoint,
        TelemetrySample sample)
    {
        ArgumentNullException.ThrowIfNull(breakpoint);
        ArgumentNullException.ThrowIfNull(sample);

        double? value = sample.GetValue(breakpoint.ChannelId);

        // A missing or null measurement cannot satisfy
        // a numeric breakpoint.
        if (value is null || double.IsNaN(value.Value))
            return false;

        return breakpoint.Operator switch
        {
            ComparisonOperator.GreaterThan =>
                value.Value > breakpoint.Threshold,

            ComparisonOperator.GreaterThanOrEqual =>
                value.Value >= breakpoint.Threshold,

            ComparisonOperator.LessThan =>
                value.Value < breakpoint.Threshold,

            ComparisonOperator.LessThanOrEqual =>
                value.Value <= breakpoint.Threshold,

            ComparisonOperator.Equal =>
                value.Value == breakpoint.Threshold,

            ComparisonOperator.NotEqual =>
                value.Value != breakpoint.Threshold,

            _ => throw new ArgumentOutOfRangeException(
                nameof(breakpoint),
                "Unsupported comparison operator.")
        };
    }
}
