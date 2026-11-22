
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
        TelemetrySample? previous = null,
        Func<long, TelemetrySample>? readSample = null)
    {
        ArgumentNullException.ThrowIfNull(breakpoint);

        return breakpoint switch
        {
            ConditionalBreakpoint conditional =>
                Evaluate(conditional, sample, previous),

            CompositeBreakpoint composite =>
                Evaluate(composite, sample, previous),

            MissingDataBreakpoint missing =>
                Evaluate(missing, sample, previous, readSample),

            _ => throw new NotSupportedException(
                $"Unsupported breakpoint type: {breakpoint.GetType().Name}")
        };
    }

    public static bool Evaluate(
        MissingDataBreakpoint breakpoint,
        TelemetrySample sample,
        TelemetrySample? previous = null,
        Func<long, TelemetrySample>? readSample = null)
    {
        ArgumentNullException.ThrowIfNull(breakpoint);
        ArgumentNullException.ThrowIfNull(sample);

        return TryMeasureGap(
            breakpoint,
            sample,
            previous,
            readSample,
            out _);
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
        MissingDataBreakpoint breakpoint,
        SamplePosition position,
        TelemetrySample sample,
        TelemetrySample? previous = null,
        Func<long, TelemetrySample>? readSample = null)
    {
        ArgumentNullException.ThrowIfNull(breakpoint);
        ArgumentNullException.ThrowIfNull(sample);

        ValidatePosition(position, sample);

        if (!TryMeasureGap(
                breakpoint,
                sample,
                previous,
                readSample,
                out var gapNanoseconds))
        {
            return null;
        }

        return BreakpointHit.ForMissingData(
            breakpoint.Id,
            position,
            sample.Timestamp,
            breakpoint.ChannelId ?? Guid.Empty,
            gapNanoseconds / 1_000_000_000d,
            breakpoint.MinimumSeconds);
    }

    public static BreakpointHit? EvaluateHit(
        IBreakpoint breakpoint,
        SamplePosition position,
        TelemetrySample sample,
        TelemetrySample? previous = null,
        Func<long, TelemetrySample>? readSample = null)
    {
        ArgumentNullException.ThrowIfNull(breakpoint);

        return breakpoint switch
        {
            ConditionalBreakpoint conditional =>
                EvaluateHit(conditional, position, sample, previous),

            CompositeBreakpoint composite =>
                EvaluateHit(composite, position, sample, previous),

            MissingDataBreakpoint missing =>
                EvaluateHit(missing, position, sample, previous, readSample),

            _ => throw new NotSupportedException(
                $"Unsupported breakpoint type: {breakpoint.GetType().Name}")
        };
    }

    private static bool TryMeasureGap(
        MissingDataBreakpoint breakpoint,
        TelemetrySample sample,
        TelemetrySample? previous,
        Func<long, TelemetrySample>? readSample,
        out long gapNanoseconds)
    {
        gapNanoseconds = 0;

        if (breakpoint.ChannelId is not Guid channelId)
        {
            if (previous is null)
                return false;

            var gap = Gap(sample, previous);
            if (gap <= breakpoint.MinimumNanoseconds)
                return false;

            gapNanoseconds = gap;
            return true;
        }

        if (!IsMissing(sample, channelId))
            return false;

        var elapsed = ElapsedSinceLastPresent(
            sample,
            channelId,
            breakpoint.MinimumNanoseconds,
            previous,
            readSample);

        if (elapsed <= breakpoint.MinimumNanoseconds)
            return false;

        if (previous is not null && IsMissing(previous, channelId))
        {
            var previousElapsed = ElapsedSinceLastPresent(
                previous,
                channelId,
                breakpoint.MinimumNanoseconds,
                previous: null,
                readSample);

            if (previousElapsed > breakpoint.MinimumNanoseconds)
                return false;
        }

        gapNanoseconds = elapsed;
        return true;
    }

    /// <summary>
    /// Time from the latest earlier sample that still has a value.
    /// The walk stops once the gap already exceeds the threshold, and it
    /// stops if timestamps stop moving forward so a flat clock cannot scan
    /// the whole recording.
    /// </summary>
    private static long ElapsedSinceLastPresent(
        TelemetrySample sample,
        Guid channelId,
        long limitNanoseconds,
        TelemetrySample? previous,
        Func<long, TelemetrySample>? readSample)
    {
        if (readSample is null)
        {
            if (previous is null || IsMissing(previous, channelId))
                return 0;

            return Gap(sample, previous);
        }

        var sequence = sample.SequenceNumber;
        while (sequence > 0)
        {
            var earlier = readSample(sequence - 1);
            var gap = Gap(sample, earlier);
            if (gap <= 0)
                return IsMissing(earlier, channelId) ? 0 : gap;

            if (!IsMissing(earlier, channelId))
                return gap;

            if (gap > limitNanoseconds)
                return gap;

            sequence--;
        }

        var first = sample.SequenceNumber == 0
            ? sample
            : readSample(0);

        return Gap(sample, first);
    }

    private static long Gap(TelemetrySample later, TelemetrySample earlier) =>
        TelemetryTimestamp.DifferenceInNanoseconds(later.Timestamp, earlier.Timestamp);

    private static bool IsMissing(TelemetrySample sample, Guid channelId) =>
        sample.GetTypedValue(channelId) is null;

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
