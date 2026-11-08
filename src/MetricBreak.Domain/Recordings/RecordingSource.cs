
namespace MetricBreak.Domain.Recordings;
public sealed record RecordingSource(
    string Name,
    string ContentHash,
    long SizeInBytes);
