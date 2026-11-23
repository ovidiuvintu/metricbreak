namespace MetricBreak.Domain.Analysis;

/// <summary>
/// Timeline integrity for a whole recording. Samples that share a timestamp stay
/// in the recording. Missing timestamps are counted only after a cadence is
/// confirmed from the intervals or declared by the caller.
/// </summary>
public sealed class SamplingHealth
{
    public SamplingHealth(
        long sampleCount,
        long startTimestampNanoseconds,
        long endTimestampNanoseconds,
        long positiveIntervalCount,
        long? intervalMinimumNanoseconds,
        long? intervalMedianNanoseconds,
        long? intervalMaximumNanoseconds,
        long? intervalModeNanoseconds,
        long intervalModeCount,
        string? cadenceSource,
        long? cadenceNanoseconds,
        long duplicateGroupCount,
        long duplicateSampleCount,
        IReadOnlyList<DuplicateTimestampGroup> duplicates,
        long gapCount,
        long? missingTimestampCount,
        IReadOnlyList<SamplingGap> gaps,
        IReadOnlyList<ChannelCoverage> missingChannels,
        int channelCount)
    {
        SampleCount = sampleCount;
        StartTimestampNanoseconds = startTimestampNanoseconds;
        EndTimestampNanoseconds = endTimestampNanoseconds;
        PositiveIntervalCount = positiveIntervalCount;
        IntervalMinimumNanoseconds = intervalMinimumNanoseconds;
        IntervalMedianNanoseconds = intervalMedianNanoseconds;
        IntervalMaximumNanoseconds = intervalMaximumNanoseconds;
        IntervalModeNanoseconds = intervalModeNanoseconds;
        IntervalModeCount = intervalModeCount;
        CadenceSource = cadenceSource;
        CadenceNanoseconds = cadenceNanoseconds;
        DuplicateGroupCount = duplicateGroupCount;
        DuplicateSampleCount = duplicateSampleCount;
        Duplicates = duplicates;
        GapCount = gapCount;
        MissingTimestampCount = missingTimestampCount;
        Gaps = gaps;
        MissingChannels = missingChannels;
        ChannelCount = channelCount;
    }

    public long SampleCount { get; }

    public long StartTimestampNanoseconds { get; }

    public long EndTimestampNanoseconds { get; }

    public long PositiveIntervalCount { get; }

    public long? IntervalMinimumNanoseconds { get; }

    public long? IntervalMedianNanoseconds { get; }

    public long? IntervalMaximumNanoseconds { get; }

    public long? IntervalModeNanoseconds { get; }

    public long IntervalModeCount { get; }

    /// <summary>"confirmed" from the observed intervals, "declared" by the caller, or null.</summary>
    public string? CadenceSource { get; }

    public long? CadenceNanoseconds { get; }

    public long DuplicateGroupCount { get; }

    public long DuplicateSampleCount { get; }

    public IReadOnlyList<DuplicateTimestampGroup> Duplicates { get; }

    public long GapCount { get; }

    /// <summary>Null when no cadence is confirmed or declared.</summary>
    public long? MissingTimestampCount { get; }

    public IReadOnlyList<SamplingGap> Gaps { get; }

    public IReadOnlyList<ChannelCoverage> MissingChannels { get; }

    public int ChannelCount { get; }
}

public sealed class DuplicateTimestampGroup
{
    public DuplicateTimestampGroup(long timestampNanoseconds, IReadOnlyList<long> sequences)
    {
        TimestampNanoseconds = timestampNanoseconds;
        Sequences = sequences;
    }

    public long TimestampNanoseconds { get; }

    public IReadOnlyList<long> Sequences { get; }
}

public sealed class SamplingGap
{
    public SamplingGap(
        long beforeSequence,
        long afterSequence,
        long intervalNanoseconds,
        long? missingTimestamps)
    {
        BeforeSequence = beforeSequence;
        AfterSequence = afterSequence;
        IntervalNanoseconds = intervalNanoseconds;
        MissingTimestamps = missingTimestamps;
    }

    public long BeforeSequence { get; }

    public long AfterSequence { get; }

    public long IntervalNanoseconds { get; }

    public long? MissingTimestamps { get; }
}

public sealed class ChannelCoverage
{
    public ChannelCoverage(
        Guid channelId,
        string name,
        string? unit,
        long missingCount,
        long firstSequence)
    {
        ChannelId = channelId;
        Name = name;
        Unit = unit;
        MissingCount = missingCount;
        FirstSequence = firstSequence;
    }

    public Guid ChannelId { get; }

    public string Name { get; }

    public string? Unit { get; }

    public long MissingCount { get; }

    public long FirstSequence { get; }
}
