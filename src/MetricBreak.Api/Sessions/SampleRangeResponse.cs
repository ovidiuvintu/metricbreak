
namespace MetricBreak.Api.Sessions;

public sealed record SampleRangeResponse(
    Guid SessionId,
    long Start,
    int Count,
    long TotalSamples,
    IReadOnlyList<SampleRangeItemResponse> Samples);

public sealed record SampleRangeItemResponse(
    long Sequence,
    string TimestampNanoseconds,
    IReadOnlyDictionary<Guid, double?> Channels);

