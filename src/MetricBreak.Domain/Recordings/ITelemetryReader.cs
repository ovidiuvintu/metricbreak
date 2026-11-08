namespace MetricBreak.Domain.Recordings;

public interface ITelemetryReader
{
    Guid RecordingId { get; }

    long SampleCount { get; }

    TelemetrySample Read(long sequenceNumber);
}
