
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

        return EvaluateCondition(
            breakpoint.ChannelId,
            breakpoint.Operator,
            breakpoint.Threshold,
            sample);
    }

    public static bool Evaluate(
        CompositeBreakpoint breakpoint,
        TelemetrySample sample)
    {
        ArgumentNullException.ThrowIfNull(breakpoint);
        ArgumentNullException.ThrowIfNull(sample);

        return breakpoint.LogicalOperator switch
        {
            LogicalOperator.And =>
                breakpoint.Conditions.All(condition =>
                    EvaluateCondition(
                        condition.ChannelId,
                        condition.Operator,
                        condition.Threshold,
                        sample)),

            LogicalOperator.Or =>
                breakpoint.Conditions.Any(condition =>
                    EvaluateCondition(
                        condition.ChannelId,
                        condition.Operator,
                        condition.Threshold,
                        sample)),

            _ => throw new ArgumentOutOfRangeException(
                nameof(breakpoint),
                "Unsupported logical operator.")
        };
    }

    public static bool Evaluate(
        IBreakpoint breakpoint,
        TelemetrySample sample)
    {
        ArgumentNullException.ThrowIfNull(breakpoint);

        return breakpoint switch
        {
            ConditionalBreakpoint conditional =>
                Evaluate(conditional, sample),

            CompositeBreakpoint composite =>
                Evaluate(composite, sample),

            _ => throw new NotSupportedException(
                $"Unsupported breakpoint type: {breakpoint.GetType().Name}")
        };
    }

    public static BreakpointHit? EvaluateHit(
        ConditionalBreakpoint breakpoint,
        SamplePosition position,
        TelemetrySample sample)
    {
        ArgumentNullException.ThrowIfNull(breakpoint);
        ArgumentNullException.ThrowIfNull(sample);

        ValidatePosition(position, sample);

        if (!Evaluate(breakpoint, sample))
            return null;

        double observedValue =
            sample.GetValue(breakpoint.ChannelId)!.Value;

        return new BreakpointHit(
            breakpoint.Id,
            position,
            sample.Timestamp,
            breakpoint.ChannelId,
            observedValue,
            breakpoint.Operator,
            breakpoint.Threshold);
    }

    public static BreakpointHit? EvaluateHit(
        CompositeBreakpoint breakpoint,
        SamplePosition position,
        TelemetrySample sample)
    {
        ArgumentNullException.ThrowIfNull(breakpoint);
        ArgumentNullException.ThrowIfNull(sample);

        ValidatePosition(position, sample);

        var conditionHits = breakpoint.Conditions
            .Select(condition =>
            {
                double? observedValue =
                    sample.GetValue(condition.ChannelId);

                if (observedValue is double value &&
                    !double.IsFinite(value))
                {
                    observedValue = null;
                }

                bool matched = EvaluateCondition(
                    condition.ChannelId,
                    condition.Operator,
                    condition.Threshold,
                    sample);

                return new BreakpointConditionHit(
                    condition.ChannelId,
                    observedValue,
                    condition.Operator,
                    condition.Threshold,
                    matched);
            })
            .ToArray();

        bool matched = breakpoint.LogicalOperator switch
        {
            LogicalOperator.And =>
                conditionHits.All(condition => condition.Matched),

            LogicalOperator.Or =>
                conditionHits.Any(condition => condition.Matched),

            _ => throw new ArgumentOutOfRangeException(
                nameof(breakpoint),
                "Unsupported logical operator.")
        };

        if (!matched)
            return null;

        // Exactly one hit for the entire composite breakpoint.
        return new BreakpointHit(
            breakpoint.Id,
            position,
            sample.Timestamp,
            breakpoint.LogicalOperator,
            conditionHits);
    }

    public static BreakpointHit? EvaluateHit(
        IBreakpoint breakpoint,
        SamplePosition position,
        TelemetrySample sample)
    {
        ArgumentNullException.ThrowIfNull(breakpoint);

        return breakpoint switch
        {
            ConditionalBreakpoint conditional =>
                EvaluateHit(conditional, position, sample),

            CompositeBreakpoint composite =>
                EvaluateHit(composite, position, sample),

            _ => throw new NotSupportedException(
                $"Unsupported breakpoint type: {breakpoint.GetType().Name}")
        };
    }

    private static bool EvaluateCondition(
        Guid channelId,
        ComparisonOperator comparisonOperator,
        double threshold,
        TelemetrySample sample)
    {
        double? observedValue = sample.GetValue(channelId);

        if (observedValue is not double value ||
            !double.IsFinite(value))
        {
            return false;
        }

        return comparisonOperator switch
        {
            ComparisonOperator.GreaterThan =>
                value > threshold,

            ComparisonOperator.GreaterThanOrEqual =>
                value >= threshold,

            ComparisonOperator.LessThan =>
                value < threshold,

            ComparisonOperator.LessThanOrEqual =>
                value <= threshold,

            ComparisonOperator.Equal =>
                value == threshold,

            ComparisonOperator.NotEqual =>
                value != threshold,

            _ => throw new ArgumentOutOfRangeException(
                nameof(comparisonOperator),
                "Unsupported comparison operator.")
        };
    }

    private static void ValidatePosition(
        SamplePosition position,
        TelemetrySample sample)
    {
        if (position.RecordingId == Guid.Empty ||
            position.SequenceNumber != sample.SequenceNumber)
        {
            throw new ArgumentException(
                "Position must identify the supplied sample.",
                nameof(position));
        }
    }
}
