using MetricBreak.Domain.Analysis;

namespace MetricBreak.Api.Analysis;

public sealed record RecordingComparisonResponse(
    Guid ReferenceRecordingId,
    string ReferenceName,
    long SubjectSampleCount,
    long ReferenceSampleCount,
    long ComparedSampleCount,
    IReadOnlyList<string> OnlyInSubject,
    IReadOnlyList<string> OnlyInReference,
    IReadOnlyList<ChannelDivergenceResponse> Channels)
{
    public static RecordingComparisonResponse From(
        Guid referenceRecordingId,
        string referenceName,
        RecordingComparison comparison)
    {
        return new RecordingComparisonResponse(
            referenceRecordingId,
            referenceName,
            comparison.SubjectSampleCount,
            comparison.ReferenceSampleCount,
            comparison.ComparedSampleCount,
            comparison.OnlyInSubject,
            comparison.OnlyInReference,
            comparison.Channels
                .Select(channel => new ChannelDivergenceResponse(
                    channel.ChannelId,
                    channel.Name,
                    channel.Unit,
                    channel.ValueType,
                    channel.ComparedCount,
                    channel.MismatchCount,
                    channel.FirstMismatchSequence,
                    channel.MaxAbsoluteDelta,
                    channel.MaxAbsoluteDeltaSequence))
                .ToArray());
    }
}

public sealed record ChannelDivergenceResponse(
    Guid ChannelId,
    string Name,
    string? Unit,
    string ValueType,
    long ComparedCount,
    long MismatchCount,
    long? FirstMismatchSequence,
    double? MaxAbsoluteDelta,
    long? MaxAbsoluteDeltaSequence);
