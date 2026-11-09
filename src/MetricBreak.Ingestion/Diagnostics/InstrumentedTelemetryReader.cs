
using MetricBreak.Domain.Recordings;

namespace MetricBreak.Ingestion.Diagnostics;

public sealed class InstrumentedTelemetryReader : ITelemetryReader
{
    private readonly ITelemetryReader _inner;
    private long _readCount;

    public Guid RecordingId => _inner.RecordingId;

    public long SampleCount => _inner.SampleCount;

    public long ReadCount => Interlocked.Read(ref _readCount);

    public InstrumentedTelemetryReader(ITelemetryReader inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public TelemetrySample Read(long sequenceNumber)
    {
        var sample = _inner.Read(sequenceNumber);

        Interlocked.Increment(ref _readCount);

        return sample;
    }

    public void ResetCounters()
    {
        Interlocked.Exchange(ref _readCount, 0);
    }
}
