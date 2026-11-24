using MetricBreak.Domain.Breakpoints;
using MetricBreak.Domain.Recordings;

namespace MetricBreak.Domain.Analysis;

public static class EventAnalyzer
{
    public static EventAnalysis Analyze(
        ITelemetryReader reader,
        IReadOnlyList<ChannelDefinition> channels,
        long eventSequence,
        int lookbackSamples,
        IReadOnlyList<RegisteredBreakpointSnapshot> breakpoints)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(channels);
        ArgumentNullException.ThrowIfNull(breakpoints);

        if (channels.Count == 0)
            throw new ArgumentException("At least one channel is required.", nameof(channels));

        if (reader.SampleCount == 0)
            throw new ArgumentOutOfRangeException(nameof(reader), "Recording contains no samples.");

        if (eventSequence < 0 || eventSequence >= reader.SampleCount)
            throw new ArgumentOutOfRangeException(nameof(eventSequence));

        if (lookbackSamples < 0)
            throw new ArgumentOutOfRangeException(nameof(lookbackSamples));

        var ordered = channels
            .OrderBy(channel => channel.Name, StringComparer.Ordinal)
            .ToArray();
        var current = reader.Read(eventSequence);
        var precedingSequence = eventSequence > 0 ? eventSequence - 1 : (long?)null;
        var previous = precedingSequence is long prior ? reader.Read(prior) : null;
        var lookbackStart = eventSequence - Math.Min(eventSequence, lookbackSamples);
        var baseline = lookbackStart == eventSequence ? current : reader.Read(lookbackStart);

        var states = new ChannelSnapshot[ordered.Length];
        var changes = new List<PrecedingChange>();
        for (var index = 0; index < ordered.Length; index++)
        {
            var channel = ordered[index];
            var value = current.GetTypedValue(channel.Id);
            states[index] = new ChannelSnapshot(
                channel.Id,
                channel.Name,
                channel.Unit,
                eventSequence,
                AnalysisReading.From(value));

            if (previous is null)
                continue;

            var priorValue = previous.GetTypedValue(channel.Id);
            if (Same(priorValue, value))
                continue;

            changes.Add(new PrecedingChange(
                channel.Id,
                channel.Name,
                channel.Unit,
                precedingSequence!.Value,
                eventSequence,
                AnalysisReading.From(priorValue),
                AnalysisReading.From(value)));
        }

        var firstMoves = new List<ChannelFirstMove>();
        if (lookbackStart < eventSequence)
        {
            foreach (var channel in ordered)
            {
                var starting = baseline.GetTypedValue(channel.Id);
                for (var sequence = lookbackStart + 1; sequence <= eventSequence; sequence++)
                {
                    var sample = reader.Read(sequence);
                    var value = sample.GetTypedValue(channel.Id);
                    if (Same(starting, value))
                        continue;

                    firstMoves.Add(new ChannelFirstMove(
                        channel.Id,
                        channel.Name,
                        channel.Unit,
                        lookbackStart,
                        sequence,
                        AnalysisReading.From(starting),
                        AnalysisReading.From(value)));
                    break;
                }
            }

            firstMoves.Sort(static (left, right) =>
            {
                var bySequence = left.FirstChangeSequence.CompareTo(right.FirstChangeSequence);
                return bySequence != 0
                    ? bySequence
                    : string.Compare(left.Name, right.Name, StringComparison.Ordinal);
            });
        }

        var runs = new ConditionRun[breakpoints.Count];
        for (var index = 0; index < breakpoints.Count; index++)
        {
            var registered = breakpoints[index];
            runs[index] = MeasureRun(
                reader,
                registered.Breakpoint,
                registered.IsEnabled,
                eventSequence);
        }

        return new EventAnalysis(
            eventSequence,
            current.Timestamp.UnixNanoseconds,
            lookbackStart,
            precedingSequence,
            states,
            changes,
            firstMoves,
            runs);
    }

    private static ConditionRun MeasureRun(
        ITelemetryReader reader,
        IBreakpoint breakpoint,
        bool isEnabled,
        long eventSequence)
    {
        if (!Matches(reader, breakpoint, eventSequence))
        {
            return new ConditionRun(
                breakpoint.Id,
                isEnabled,
                matchesAtEvent: false,
                null,
                null,
                null,
                null);
        }

        var start = eventSequence;
        while (start > 0 && Matches(reader, breakpoint, start - 1))
            start--;

        var end = eventSequence;
        var last = reader.SampleCount - 1;
        while (end < last && Matches(reader, breakpoint, end + 1))
            end++;

        var duration = TelemetryTimestamp.DifferenceInNanoseconds(
            reader.Read(end).Timestamp,
            reader.Read(start).Timestamp);
        if (duration < 0)
            duration = 0;

        return new ConditionRun(
            breakpoint.Id,
            isEnabled,
            matchesAtEvent: true,
            start,
            end,
            end - start + 1,
            duration);
    }

    private static bool Matches(
        ITelemetryReader reader,
        IBreakpoint breakpoint,
        long sequence)
    {
        var sample = reader.Read(sequence);
        var previous = sequence > 0 ? reader.Read(sequence - 1) : null;
        return BreakpointEvaluator.Evaluate(breakpoint, sample, previous, reader.Read);
    }

    private static bool Same(ChannelValue? left, ChannelValue? right)
    {
        if (left is null || right is null)
            return left is null && right is null;

        return (left, right) switch
        {
            (ChannelValue.Numeric a, ChannelValue.Numeric b) =>
                a.Value.Equals(b.Value) || (double.IsNaN(a.Value) && double.IsNaN(b.Value)),
            (ChannelValue.Boolean a, ChannelValue.Boolean b) => a.Value == b.Value,
            (ChannelValue.Categorical a, ChannelValue.Categorical b) =>
                string.Equals(a.Value, b.Value, StringComparison.Ordinal),
            _ => false,
        };
    }
}
