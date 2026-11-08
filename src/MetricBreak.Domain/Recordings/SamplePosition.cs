
namespace MetricBreak.Domain.Recordings;

public readonly record struct SamplePosition(
    Guid RecordingId,
    long SequenceNumber);
