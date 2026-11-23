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
        long? modeCount,
        double? median,
        double? standardDeviation,
        double? percentile05,
        double? percentile25,
        double? percentile75,
        double? percentile95)
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
        Median = median;
        StandardDeviation = standardDeviation;
        Percentile05 = percentile05;
        Percentile25 = percentile25;
        Percentile75 = percentile75;
        Percentile95 = percentile95;
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

    public double? Median { get; }

    /// <summary>Sample standard deviation of the present numeric values. Null when fewer than two values exist.</summary>
    public double? StandardDeviation { get; }

    public double? Percentile05 { get; }

    public double? Percentile25 { get; }

    public double? Percentile75 { get; }

    public double? Percentile95 { get; }
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
