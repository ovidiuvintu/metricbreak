using MetricBreak.Domain.Analysis;

namespace MetricBreak.Api.Analysis;

public sealed record WindowAnalysisResponse(
    long StartSequence,
    long EndSequence,
    string StartTimestampNanoseconds,
    string EndTimestampNanoseconds,
    long SampleCount,
    IReadOnlyList<ChannelWindowResponse> Channels,
    IReadOnlyList<ChannelComparisonResponse> Comparisons)
{
    public static WindowAnalysisResponse From(WindowAnalysis analysis)
    {
        return new WindowAnalysisResponse(
            analysis.StartSequence,
            analysis.EndSequence,
            analysis.StartTimestampNanoseconds.ToString(System.Globalization.CultureInfo.InvariantCulture),
            analysis.EndTimestampNanoseconds.ToString(System.Globalization.CultureInfo.InvariantCulture),
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
                    channel.ModeCount))
                .ToArray(),
            analysis.Correlations
                .Select(pair => new ChannelComparisonResponse(
                    pair.LeftChannelId,
                    pair.LeftChannelName,
                    pair.RightChannelId,
                    pair.RightChannelName,
                    pair.OverlapCount,
                    pair.Correlation))
                .ToArray());
    }
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
    long? ModeCount);

public sealed record ChannelComparisonResponse(
    Guid LeftChannelId,
    string LeftChannelName,
    Guid RightChannelId,
    string RightChannelName,
    long OverlapCount,
    double? Correlation);
