
using MetricBreak.Domain.Recordings;

namespace MetricBreak.Ingestion;

public sealed class IndexedTelemetryReader : ITelemetryReader
{
    private readonly TelemetrySample[] _samples;

    public Guid RecordingId { get; }

    public long SampleCount => _samples.LongLength;

    public IndexedTelemetryReader(
        Guid recordingId,
        IEnumerable<TelemetrySample> samples)
    {
        if (recordingId == Guid.Empty)
        {
            throw new ArgumentException(
                "Recording ID cannot be empty.",
                nameof(recordingId));
        }

        ArgumentNullException.ThrowIfNull(samples);

        var snapshot = samples.ToArray();

        for (var position = 0; position < snapshot.Length; position++)
        {
            var sample = snapshot[position];

            if (sample is null)
            {
                throw new ArgumentException(
                    "Samples cannot contain null entries.",
                    nameof(samples));
            }

            if (sample.SequenceNumber != position)
            {
                throw new ArgumentException(
                    "Sample sequence numbers must be contiguous and zero-based.",
                    nameof(samples));
            }
        }

        RecordingId = recordingId;
        _samples = snapshot;
    }

    public TelemetrySample Read(long sequenceNumber)
    {
        if (sequenceNumber < 0 ||
            sequenceNumber >= SampleCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sequenceNumber));
        }

        return _samples[(int)sequenceNumber];
    }
}
