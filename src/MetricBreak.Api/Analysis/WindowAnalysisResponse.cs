using MetricBreak.Domain.Analysis;

namespace MetricBreak.Api.Analysis;

public sealed record WindowAnalysisResponse(
    long StartSequence,
    long EndSequence,
    string StartTimestampNanoseconds,
    string EndTimestampNanoseconds,
    long SampleCount,
    IReadOnlyList<ChannelWindowResponse> Channels,
    IReadOnlyList<ChannelComparisonResponse> Comparisons,
    SamplingHealthResponse Sampling)
{
    public static WindowAnalysisResponse From(WindowAnalysis analysis, SamplingHealth sampling)
    {
        return new WindowAnalysisResponse(
            analysis.StartSequence,
            analysis.EndSequence,
            Nanoseconds(analysis.StartTimestampNanoseconds),
            Nanoseconds(analysis.EndTimestampNanoseconds),
            analysis.SampleCount,
            analysis.Channels
                .Select(channel => new ChannelWindowResponse(
                    channel.ChannelId,
                    channel.Name,
                    channel.Unit,
                    channel.ValueType,
                    channel.PresentCount,
                    channel.MissingCount,
                    channel.ChangeCount,
                    channel.Minimum,
                    channel.MinimumSequence,
                    channel.Maximum,
                    channel.MaximumSequence,
                    channel.Mean,
                    channel.TrueCount,
                    channel.FalseCount,
                    channel.DistinctCount,
                    channel.Mode,
                    channel.ModeCount,
                    channel.Median,
                    channel.StandardDeviation,
                    channel.Percentile05,
                    channel.Percentile25,
                    channel.Percentile75,
                    channel.Percentile95))
                .ToArray(),
            analysis.Correlations
                .Select(pair => new ChannelComparisonResponse(
                    pair.LeftChannelId,
                    pair.LeftChannelName,
                    pair.RightChannelId,
                    pair.RightChannelName,
                    pair.OverlapCount,
                    pair.Correlation))
                .ToArray(),
            SamplingHealthResponse.From(sampling));
    }

    private static string Nanoseconds(long value)
        => value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public sealed record ChannelWindowResponse(
    Guid ChannelId,
    string Name,
    string? Unit,
    string ValueType,
    long PresentCount,
    long MissingCount,
    long? ChangeCount,
    double? Minimum,
    long? MinimumSequence,
    double? Maximum,
    long? MaximumSequence,
    double? Mean,
    long? TrueCount,
    long? FalseCount,
    int? DistinctCount,
    string? Mode,
    long? ModeCount,
    double? Median,
    double? StandardDeviation,
    double? Percentile05,
    double? Percentile25,
    double? Percentile75,
    double? Percentile95);

public sealed record SamplingHealthResponse(
    long SampleCount,
    string StartTimestampNanoseconds,
    string EndTimestampNanoseconds,
    long PositiveIntervalCount,
    string? IntervalMinimumNanoseconds,
    string? IntervalMedianNanoseconds,
    string? IntervalMaximumNanoseconds,
    string? IntervalModeNanoseconds,
    long IntervalModeCount,
    string? CadenceSource,
    string? CadenceNanoseconds,
    long DuplicateGroupCount,
    long DuplicateSampleCount,
    IReadOnlyList<DuplicateTimestampResponse> Duplicates,
    long GapCount,
    long? MissingTimestampCount,
    IReadOnlyList<SamplingGapResponse> Gaps,
    IReadOnlyList<ChannelCoverageResponse> MissingChannels,
    int ChannelCount)
{
    public static SamplingHealthResponse From(SamplingHealth sampling)
    {
        return new SamplingHealthResponse(
            sampling.SampleCount,
            Nanoseconds(sampling.StartTimestampNanoseconds),
            Nanoseconds(sampling.EndTimestampNanoseconds),
            sampling.PositiveIntervalCount,
            Nanoseconds(sampling.IntervalMinimumNanoseconds),
            Nanoseconds(sampling.IntervalMedianNanoseconds),
            Nanoseconds(sampling.IntervalMaximumNanoseconds),
            Nanoseconds(sampling.IntervalModeNanoseconds),
            sampling.IntervalModeCount,
            sampling.CadenceSource,
            Nanoseconds(sampling.CadenceNanoseconds),
            sampling.DuplicateGroupCount,
            sampling.DuplicateSampleCount,
            sampling.Duplicates
                .Select(group => new DuplicateTimestampResponse(
                    Nanoseconds(group.TimestampNanoseconds),
                    group.Sequences))
                .ToArray(),
            sampling.GapCount,
            sampling.MissingTimestampCount,
            sampling.Gaps
                .Select(gap => new SamplingGapResponse(
                    gap.BeforeSequence,
                    gap.AfterSequence,
                    Nanoseconds(gap.IntervalNanoseconds),
                    gap.MissingTimestamps))
                .ToArray(),
            sampling.MissingChannels
                .Select(channel => new ChannelCoverageResponse(
                    channel.ChannelId,
                    channel.Name,
                    channel.Unit,
                    channel.MissingCount,
                    channel.FirstSequence))
                .ToArray(),
            sampling.ChannelCount);
    }

    private static string Nanoseconds(long value)
        => value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static string? Nanoseconds(long? value)
        => value?.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public sealed record DuplicateTimestampResponse(
    string TimestampNanoseconds,
    IReadOnlyList<long> Sequences);

public sealed record SamplingGapResponse(
    long BeforeSequence,
    long AfterSequence,
    string IntervalNanoseconds,
    long? MissingTimestamps);

public sealed record ChannelCoverageResponse(
    Guid ChannelId,
    string Name,
    string? Unit,
    long MissingCount,
    long FirstSequence);

public sealed record ChannelComparisonResponse(
    Guid LeftChannelId,
    string LeftChannelName,
    Guid RightChannelId,
    string RightChannelName,
    long OverlapCount,
    double? Correlation);
