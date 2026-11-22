namespace MetricBreak.Domain.Analysis;

public sealed class WindowAnalysis
{
    public WindowAnalysis(
        long startSequence,
        long endSequence,
        long startTimestampNanoseconds,
        long endTimestampNanoseconds,
        long sampleCount,
        IReadOnlyList<ChannelWindowSummary> channels,
        IReadOnlyList<ChannelCorrelation> correlations)
    {
        StartSequence = startSequence;
        EndSequence = endSequence;
        StartTimestampNanoseconds = startTimestampNanoseconds;
        EndTimestampNanoseconds = endTimestampNanoseconds;
        SampleCount = sampleCount;
        Channels = channels;
        Correlations = correlations;
    }

    public long StartSequence { get; }

    public long EndSequence { get; }

    public long StartTimestampNanoseconds { get; }

    public long EndTimestampNanoseconds { get; }

    public long SampleCount { get; }

    public IReadOnlyList<ChannelWindowSummary> Channels { get; }

    public IReadOnlyList<ChannelCorrelation> Correlations { get; }
}

public sealed class ChannelWindowSummary
{
    public ChannelWindowSummary(
        Guid channelId,
        string name,
        string? unit,
        string valueType,
        long presentCount,
        long missingCount,
        long? changeCount,
        double? minimum,
        long? minimumSequence,
        double? maximum,
        long? maximumSequence,
        double? mean,
        long? trueCount,
        long? falseCount,
        int? distinctCount,
        string? mode,
        long? modeCount)
    {
        ChannelId = channelId;
        Name = name;
        Unit = unit;
        ValueType = valueType;
        PresentCount = presentCount;
        MissingCount = missingCount;
        ChangeCount = changeCount;
        Minimum = minimum;
        MinimumSequence = minimumSequence;
        Maximum = maximum;
        MaximumSequence = maximumSequence;
        Mean = mean;
        TrueCount = trueCount;
        FalseCount = falseCount;
        DistinctCount = distinctCount;
        Mode = mode;
        ModeCount = modeCount;
    }

    public Guid ChannelId { get; }

    public string Name { get; }

    public string? Unit { get; }

    public string ValueType { get; }

    public long PresentCount { get; }

    public long MissingCount { get; }

    public long? ChangeCount { get; }

    public double? Minimum { get; }

    public long? MinimumSequence { get; }

    public double? Maximum { get; }

    public long? MaximumSequence { get; }

    public double? Mean { get; }

    public long? TrueCount { get; }

    public long? FalseCount { get; }

    public int? DistinctCount { get; }

    public string? Mode { get; }

    public long? ModeCount { get; }
}

public sealed class ChannelCorrelation
{
    public ChannelCorrelation(
        Guid leftChannelId,
        string leftChannelName,
        Guid rightChannelId,
        string rightChannelName,
        long overlapCount,
        double? correlation)
    {
        LeftChannelId = leftChannelId;
        LeftChannelName = leftChannelName;
        RightChannelId = rightChannelId;
        RightChannelName = rightChannelName;
        OverlapCount = overlapCount;
        Correlation = correlation;
    }

    public Guid LeftChannelId { get; }

    public string LeftChannelName { get; }

    public Guid RightChannelId { get; }

    public string RightChannelName { get; }

    public long OverlapCount { get; }

    /// <summary>
    /// Pearson correlation over samples where both channels have a value.
    /// Null when fewer than two overlapping samples exist or either channel is constant.
    /// </summary>
    public double? Correlation { get; }
}
