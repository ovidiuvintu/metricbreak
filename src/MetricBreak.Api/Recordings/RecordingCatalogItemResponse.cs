namespace MetricBreak.Api.Recordings;

public sealed record RecordingCatalogItemResponse(
    Guid RecordingId,
    string Name,
    string SourceName,
    string SourceContentHash,
    long SourceSizeInBytes,
    long SampleCount,
    DateTimeOffset ImportedAtUtc);