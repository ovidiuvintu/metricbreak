
using MetricBreak.Domain.Recordings;

namespace MetricBreak.Domain.Breakpoints;

public static class BreakpointEvaluator
{
    public static bool Evaluate(
        ConditionalBreakpoint breakpoint,
        TelemetrySample sample,
        TelemetrySample? previous = null)
    {
        ArgumentNullException.ThrowIfNull(breakpoint);
        ArgumentNullException.ThrowIfNull(sample);

        return EvaluateCondition(
            breakpoint.ChannelId,
            breakpoint.Operator,
            breakpoint.Threshold,
            breakpoint.ExpectedValue, sample, previous);
    }

    public static bool Evaluate(
        CompositeBreakpoint breakpoint,
        TelemetrySample sample,
        TelemetrySample? previous = null)
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
                        condition.ExpectedValue, sample, previous)),

            LogicalOperator.Or =>
                breakpoint.Conditions.Any(condition =>
                    EvaluateCondition(
                        condition.ChannelId,
                        condition.Operator,
                        condition.Threshold,
                        condition.ExpectedValue, sample, previous)),

            _ => throw new ArgumentOutOfRangeException(
                nameof(breakpoint),
                "Unsupported logical operator.")
        };
    }

    public static bool Evaluate(
        IBreakpoint breakpoint,
        TelemetrySample sample,
        TelemetrySample? previous = null)
    {
        ArgumentNullException.ThrowIfNull(breakpoint);

        return breakpoint switch
        {
            ConditionalBreakpoint conditional =>
                Evaluate(conditional, sample, previous),

            CompositeBreakpoint composite =>
                Evaluate(composite, sample, previous),

            _ => throw new NotSupportedException(
                $"Unsupported breakpoint type: {breakpoint.GetType().Name}")
        };
    }

    public static BreakpointHit? EvaluateHit(
        ConditionalBreakpoint breakpoint,
        SamplePosition position,
        TelemetrySample sample,
        TelemetrySample? previous = null)
    {
        ArgumentNullException.ThrowIfNull(breakpoint);
        ArgumentNullException.ThrowIfNull(sample);

        ValidatePosition(position, sample);

        if (!Evaluate(breakpoint, sample, previous))
            return null;

        var typedValue = sample.GetTypedValue(breakpoint.ChannelId);
        double observedValue = typedValue is ChannelValue.Numeric n ? n.Value : 0d;

        return new BreakpointHit(
            breakpoint.Id,
            position,
            sample.Timestamp,
            breakpoint.ChannelId,
            observedValue,
            breakpoint.Operator,
            breakpoint.Threshold)
        {
            TypedObservedValue = typedValue,
            ExpectedValue = breakpoint.ExpectedValue
        };
    }

    public static BreakpointHit? EvaluateHit(
        CompositeBreakpoint breakpoint,
        SamplePosition position,
        TelemetrySample sample,
        TelemetrySample? previous = null)
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
                    condition.ExpectedValue, sample, previous);

                return new BreakpointConditionHit(
                    condition.ChannelId,
                    observedValue,
                    condition.Operator,
                    condition.Threshold,
                    matched)
                {
                    TypedObservedValue = sample.GetTypedValue(condition.ChannelId),
                    ExpectedValue = condition.ExpectedValue
                };
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
        TelemetrySample sample,
        TelemetrySample? previous = null)
    {
        ArgumentNullException.ThrowIfNull(breakpoint);

        return breakpoint switch
        {
            ConditionalBreakpoint conditional =>
                EvaluateHit(conditional, position, sample, previous),

            CompositeBreakpoint composite =>
                EvaluateHit(composite, position, sample, previous),

            _ => throw new NotSupportedException(
                $"Unsupported breakpoint type: {breakpoint.GetType().Name}")
        };
    }

    private static bool EvaluateCondition(
        Guid channelId, ComparisonOperator comparisonOperator, double threshold,
        ChannelValue? expectedValue, TelemetrySample sample, TelemetrySample? previous = null)
        => TypedConditionEvaluator.Evaluate(channelId, comparisonOperator, threshold,
            expectedValue, sample, previous);

    private static void ValidatePosition(
        SamplePosition position,
        TelemetrySample sample,
        TelemetrySample? previous = null)
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
