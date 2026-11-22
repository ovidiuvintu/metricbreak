using MetricBreak.Domain.Recordings;

namespace MetricBreak.Domain.Breakpoints;

public enum BreakpointCoverageRelation
{
    Above,
    AtOrAbove,
    Below,
    AtOrBelow,
    Matching,
}

/// <summary>
/// Crossings through a threshold, and how long the signal stayed on that side.
/// A rising edge at 8,000 psi can cross hundreds of times while a greater-than
/// at 9,000 psi is one short run. Both numbers are needed to tell those apart.
/// </summary>
public sealed record BreakpointCoverage(
    Guid BreakpointId,
    int? Crossings,
    int MatchingSamples,
    long MatchingNanoseconds,
    int LongestRunSamples,
    long LongestRunNanoseconds,
    BreakpointCoverageRelation Relation);

public static class BreakpointCoverageAnalyzer
{
    public static IReadOnlyList<BreakpointCoverage> Measure(
        IReadOnlyList<IBreakpoint> breakpoints,
        long sampleCount,
        Func<long, TelemetrySample> readSample)
    {
        ArgumentNullException.ThrowIfNull(breakpoints);
        ArgumentNullException.ThrowIfNull(readSample);

        if (sampleCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sampleCount));
        }

        var trackers = breakpoints
            .Where(breakpoint => breakpoint is not null)
            .Select(CreateTracker)
            .Where(tracker => tracker is not null)
            .Cast<Tracker>()
            .ToArray();

        if (trackers.Length == 0 || sampleCount == 0)
        {
            return trackers
                .Select(tracker => tracker.ToCoverage())
                .ToArray();
        }

        TelemetrySample? previous = null;
        var lastGap = 0L;

        for (var sequence = 0L; sequence < sampleCount; sequence++)
        {
            var sample = readSample(sequence);
            if (previous is not null)
            {
                var gap = TelemetryTimestamp.DifferenceInNanoseconds(
                    sample.Timestamp,
                    previous.Timestamp);
                if (gap < 0)
                {
                    gap = 0;
                }

                lastGap = gap;
                foreach (var tracker in trackers)
                {
                    tracker.AddInterval(gap);
                }
            }

            foreach (var tracker in trackers)
            {
                tracker.Observe(previous, sample);
            }

            previous = sample;
        }

        foreach (var tracker in trackers)
        {
            tracker.Finish(lastGap);
        }

        return trackers
            .Where(tracker => !tracker.Unsupported)
            .Select(tracker => tracker.ToCoverage())
            .ToArray();
    }

    private static Tracker? CreateTracker(IBreakpoint breakpoint)
    {
        return breakpoint switch
        {
            ConditionalBreakpoint conditional when TryRelation(
                conditional.Operator,
                out var relation) =>
                new ThresholdTracker(conditional, relation),

            CompositeBreakpoint composite =>
                new CompositeTracker(composite),

            _ => null,
        };
    }

    private static bool TryRelation(
        ComparisonOperator op,
        out BreakpointCoverageRelation relation)
    {
        switch (op)
        {
            case ComparisonOperator.GreaterThan:
            case ComparisonOperator.RisingEdge:
                relation = BreakpointCoverageRelation.Above;
                return true;
            case ComparisonOperator.GreaterThanOrEqual:
                relation = BreakpointCoverageRelation.AtOrAbove;
                return true;
            case ComparisonOperator.LessThan:
            case ComparisonOperator.FallingEdge:
                relation = BreakpointCoverageRelation.Below;
                return true;
            case ComparisonOperator.LessThanOrEqual:
                relation = BreakpointCoverageRelation.AtOrBelow;
                return true;
            default:
                relation = default;
                return false;
        }
    }

    private abstract class Tracker
    {
        public bool Unsupported { get; protected set; }

        public abstract void AddInterval(long nanoseconds);

        public abstract void Observe(TelemetrySample? previous, TelemetrySample sample);

        public abstract void Finish(long lastGapNanoseconds);

        public abstract BreakpointCoverage ToCoverage();
    }

    private sealed class ThresholdTracker : Tracker
    {
        private readonly ConditionalBreakpoint _breakpoint;
        private readonly BreakpointCoverageRelation _relation;
        private int _crossings;
        private int _matchingSamples;
        private long _matchingNanoseconds;
        private int _runSamples;
        private long _runNanoseconds;
        private int _longestSamples;
        private long _longestNanoseconds;
        private bool _inRun;

        public ThresholdTracker(
            ConditionalBreakpoint breakpoint,
            BreakpointCoverageRelation relation)
        {
            _breakpoint = breakpoint;
            _relation = relation;
        }

        public override void AddInterval(long nanoseconds)
        {
            if (!_inRun || Unsupported)
            {
                return;
            }

            _runNanoseconds += nanoseconds;
            _matchingNanoseconds += nanoseconds;
        }

        public override void Observe(TelemetrySample? previous, TelemetrySample sample)
        {
            if (Unsupported)
            {
                return;
            }

            if (!TryNumeric(sample, _breakpoint.ChannelId, out var current))
            {
                if (sample.GetTypedValue(_breakpoint.ChannelId) is not null)
                {
                    Unsupported = true;
                }

                CloseRun();
                return;
            }

            if (previous is not null
                && TryNumeric(previous, _breakpoint.ChannelId, out var prior)
                && IsCrossing(_breakpoint.Operator, prior, current, _breakpoint.Threshold))
            {
                _crossings++;
            }

            if (IsOnSide(_breakpoint.Operator, current, _breakpoint.Threshold))
            {
                if (!_inRun)
                {
                    _inRun = true;
                    _runSamples = 0;
                    _runNanoseconds = 0;
                }

                _runSamples++;
                _matchingSamples++;
                return;
            }

            CloseRun();
        }

        public override void Finish(long lastGapNanoseconds)
        {
            if (_inRun && lastGapNanoseconds > 0)
            {
                _runNanoseconds += lastGapNanoseconds;
                _matchingNanoseconds += lastGapNanoseconds;
            }

            CloseRun();
        }

        public override BreakpointCoverage ToCoverage() =>
            new(
                _breakpoint.Id,
                _crossings,
                _matchingSamples,
                _matchingNanoseconds,
                _longestSamples,
                _longestNanoseconds,
                _relation);

        private void CloseRun()
        {
            if (!_inRun)
            {
                return;
            }

            if (_runSamples > _longestSamples
                || (_runSamples == _longestSamples && _runNanoseconds > _longestNanoseconds))
            {
                _longestSamples = _runSamples;
                _longestNanoseconds = _runNanoseconds;
            }

            _inRun = false;
            _runSamples = 0;
            _runNanoseconds = 0;
        }
    }

    private sealed class CompositeTracker : Tracker
    {
        private readonly CompositeBreakpoint _breakpoint;
        private int _matchingSamples;
        private long _matchingNanoseconds;
        private int _runSamples;
        private long _runNanoseconds;
        private int _longestSamples;
        private long _longestNanoseconds;
        private bool _inRun;

        public CompositeTracker(CompositeBreakpoint breakpoint)
        {
            _breakpoint = breakpoint;
        }

        public override void AddInterval(long nanoseconds)
        {
            if (!_inRun)
            {
                return;
            }

            _runNanoseconds += nanoseconds;
            _matchingNanoseconds += nanoseconds;
        }

        public override void Observe(TelemetrySample? previous, TelemetrySample sample)
        {
            if (!BreakpointEvaluator.Evaluate(_breakpoint, sample, previous))
            {
                CloseRun();
                return;
            }

            if (!_inRun)
            {
                _inRun = true;
                _runSamples = 0;
                _runNanoseconds = 0;
            }

            _runSamples++;
            _matchingSamples++;
        }

        public override void Finish(long lastGapNanoseconds)
        {
            if (_inRun && lastGapNanoseconds > 0)
            {
                _runNanoseconds += lastGapNanoseconds;
                _matchingNanoseconds += lastGapNanoseconds;
            }

            CloseRun();
        }

        public override BreakpointCoverage ToCoverage() =>
            new(
                _breakpoint.Id,
                null,
                _matchingSamples,
                _matchingNanoseconds,
                _longestSamples,
                _longestNanoseconds,
                BreakpointCoverageRelation.Matching);

        private void CloseRun()
        {
            if (!_inRun)
            {
                return;
            }

            if (_runSamples > _longestSamples
                || (_runSamples == _longestSamples && _runNanoseconds > _longestNanoseconds))
            {
                _longestSamples = _runSamples;
                _longestNanoseconds = _runNanoseconds;
            }

            _inRun = false;
            _runSamples = 0;
            _runNanoseconds = 0;
        }
    }

    private static bool TryNumeric(
        TelemetrySample sample,
        Guid channelId,
        out double value)
    {
        if (sample.GetTypedValue(channelId) is ChannelValue.Numeric numeric
            && double.IsFinite(numeric.Value))
        {
            value = numeric.Value;
            return true;
        }

        value = 0;
        return false;
    }

    private static bool IsOnSide(
        ComparisonOperator op,
        double value,
        double threshold) =>
        op switch
        {
            ComparisonOperator.GreaterThan or ComparisonOperator.RisingEdge =>
                value > threshold,
            ComparisonOperator.GreaterThanOrEqual =>
                value >= threshold,
            ComparisonOperator.LessThan or ComparisonOperator.FallingEdge =>
                value < threshold,
            ComparisonOperator.LessThanOrEqual =>
                value <= threshold,
            _ => false,
        };

    private static bool IsCrossing(
        ComparisonOperator op,
        double previous,
        double current,
        double threshold) =>
        op switch
        {
            ComparisonOperator.GreaterThan or ComparisonOperator.RisingEdge =>
                previous <= threshold && current > threshold,
            ComparisonOperator.GreaterThanOrEqual =>
                previous < threshold && current >= threshold,
            ComparisonOperator.LessThan or ComparisonOperator.FallingEdge =>
                previous >= threshold && current < threshold,
            ComparisonOperator.LessThanOrEqual =>
                previous > threshold && current <= threshold,
            _ => false,
        };
}
