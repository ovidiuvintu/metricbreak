using MetricBreak.Domain.Recordings;

namespace MetricBreak.Domain.Analysis;

public static class SamplingHealthAnalyzer
{
    /// <summary>
    /// A single interval length must cover at least this share of the positive
    /// steps before it is treated as the recording's cadence.
    /// </summary>
    public const double ConfirmedCadenceShare = 0.8;

    public static SamplingHealth Analyze(
        ITelemetryReader reader,
        IReadOnlyList<ChannelDefinition> channels,
        long? declaredCadenceNanoseconds)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(channels);

        if (channels.Count == 0)
            throw new ArgumentException("At least one channel is required.", nameof(channels));

        if (reader.SampleCount == 0)
            throw new ArgumentOutOfRangeException(nameof(reader), "Recording contains no samples.");

        if (declaredCadenceNanoseconds is <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(declaredCadenceNanoseconds),
                "Expected interval must be positive.");

        var sampleCount = reader.SampleCount;
        var missingCounts = new long[channels.Count];
        var firstMissing = new long[channels.Count];
        var missingSeen = new bool[channels.Count];
        var steps = new List<IntervalStep>(sampleCount > 1 ? (int)Math.Min(sampleCount - 1, int.MaxValue) : 0);
        var positiveIntervals = new List<long>();
        var intervalCounts = new Dictionary<long, long>();
        var firstSequenceByTimestamp = new Dictionary<long, long>();
        var duplicateGroups = new Dictionary<long, List<long>>();

        long startTimestamp = 0;
        long endTimestamp = 0;
        long? previousTimestamp = null;
        long previousSequence = 0;

        for (var sequence = 0L; sequence < sampleCount; sequence++)
        {
            var sample = reader.Read(sequence);
            var timestamp = sample.Timestamp.UnixNanoseconds;
            if (sequence == 0)
                startTimestamp = timestamp;
            if (sequence == sampleCount - 1)
                endTimestamp = timestamp;

            if (previousTimestamp is long earlier)
            {
                var delta = timestamp - earlier;
                steps.Add(new IntervalStep(previousSequence, sequence, delta));
                if (delta > 0)
                {
                    positiveIntervals.Add(delta);
                    intervalCounts[delta] = intervalCounts.GetValueOrDefault(delta) + 1;
                }
            }

            if (duplicateGroups.TryGetValue(timestamp, out var group))
            {
                group.Add(sequence);
            }
            else if (firstSequenceByTimestamp.TryGetValue(timestamp, out var first))
            {
                duplicateGroups[timestamp] = [first, sequence];
            }
            else
            {
                firstSequenceByTimestamp[timestamp] = sequence;
            }

            for (var index = 0; index < channels.Count; index++)
            {
                if (IsPresent(channels[index], sample.GetTypedValue(channels[index].Id)))
                    continue;

                missingCounts[index]++;
                if (missingSeen[index])
                    continue;

                missingSeen[index] = true;
                firstMissing[index] = sequence;
            }

            previousTimestamp = timestamp;
            previousSequence = sequence;
        }

        long? mode = null;
        var modeCount = 0L;
        foreach (var (interval, count) in intervalCounts)
        {
            if (count > modeCount || (count == modeCount && (mode is null || interval < mode)))
            {
                mode = interval;
                modeCount = count;
            }
        }

        var positiveCount = positiveIntervals.Count;
        var confirmed = mode is not null &&
            positiveCount > 0 &&
            modeCount / (double)positiveCount >= ConfirmedCadenceShare;

        string? cadenceSource;
        long? cadence;
        if (declaredCadenceNanoseconds is long declared)
        {
            cadenceSource = "declared";
            cadence = declared;
        }
        else if (confirmed)
        {
            cadenceSource = "confirmed";
            cadence = mode;
        }
        else
        {
            cadenceSource = null;
            cadence = null;
        }

        long? median = null;
        long? minimum = null;
        long? maximum = null;
        if (positiveCount > 0)
        {
            positiveIntervals.Sort();
            minimum = positiveIntervals[0];
            maximum = positiveIntervals[positiveCount - 1];
            median = Median(positiveIntervals);
        }

        var gaps = new List<SamplingGap>();
        long? missingTimestamps = cadence is null ? null : 0;
        foreach (var step in steps)
        {
            if (step.Delta < 0)
            {
                gaps.Add(new SamplingGap(step.BeforeSequence, step.AfterSequence, step.Delta, null));
                continue;
            }

            if (step.Delta == 0)
                continue;

            if (cadence is long known && LongerThanCadence(step.Delta, known))
            {
                var missing = MissingTimestamps(step.Delta, known);
                missingTimestamps += missing;
                gaps.Add(new SamplingGap(step.BeforeSequence, step.AfterSequence, step.Delta, missing));
                continue;
            }

            if (cadence is null && median is long typical && LongerThanTwice(step.Delta, typical))
                gaps.Add(new SamplingGap(step.BeforeSequence, step.AfterSequence, step.Delta, null));
        }

        var duplicates = duplicateGroups
            .OrderBy(pair => pair.Value[0])
            .Select(pair => new DuplicateTimestampGroup(pair.Key, pair.Value))
            .ToArray();

        var duplicateSampleCount = duplicates.Sum(group => (long)group.Sequences.Count);

        var missingChannels = new List<ChannelCoverage>();
        for (var index = 0; index < channels.Count; index++)
        {
            if (missingCounts[index] == 0)
                continue;

            var channel = channels[index];
            missingChannels.Add(new ChannelCoverage(
                channel.Id,
                channel.Name,
                channel.Unit,
                missingCounts[index],
                firstMissing[index]));
        }

        missingChannels.Sort(static (left, right) =>
        {
            var byCount = right.MissingCount.CompareTo(left.MissingCount);
            return byCount != 0
                ? byCount
                : string.Compare(left.Name, right.Name, StringComparison.Ordinal);
        });

        return new SamplingHealth(
            sampleCount,
            startTimestamp,
            endTimestamp,
            positiveCount,
            minimum,
            median,
            maximum,
            mode,
            modeCount,
            cadenceSource,
            cadence,
            duplicates.Length,
            duplicateSampleCount,
            duplicates,
            gaps.Count,
            missingTimestamps,
            gaps,
            missingChannels,
            channels.Count);
    }

    private static long Median(List<long> sorted)
    {
        var mid = sorted.Count / 2;
        if (sorted.Count % 2 == 1)
            return sorted[mid];

        var left = sorted[mid - 1];
        var right = sorted[mid];
        return left + (right - left) / 2;
    }

    private static bool LongerThanCadence(long delta, long cadence)
        => cadence > 0 && delta > cadence && delta - cadence > cadence / 2;

    private static bool LongerThanTwice(long delta, long median)
        => median > 0 && delta > median && delta - median > median;

    private static long MissingTimestamps(long delta, long cadence)
    {
        var rounded = (delta + cadence / 2) / cadence;
        var missing = rounded - 1;
        return missing < 1 ? 1 : missing;
    }

    private static bool IsPresent(ChannelDefinition channel, ChannelValue? value)
    {
        return value switch
        {
            ChannelValue.Numeric number when channel.ValueType == ChannelValueType.Numeric &&
                double.IsFinite(number.Value) => true,
            ChannelValue.Boolean when channel.ValueType == ChannelValueType.Boolean => true,
            ChannelValue.Categorical text when channel.ValueType == ChannelValueType.Categorical &&
                text.Value.Length > 0 => true,
            _ => false,
        };
    }

    private readonly record struct IntervalStep(long BeforeSequence, long AfterSequence, long Delta);
}
