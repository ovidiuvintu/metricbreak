
using MetricBreak.Domain.Recordings;

namespace MetricBreak.Ingestion.Csv;

public sealed class CsvTelemetryReader : ITelemetryReader
{
    private readonly IReadOnlyList<TelemetrySample> _samples;

    public Guid RecordingId { get; }

    public long SampleCount => _samples.Count;

    public CsvTelemetryReader(
        Guid recordingId,
        CsvTelemetryResult result)
    {
        if (recordingId == Guid.Empty)
        {
            throw new ArgumentException(
                "Recording ID cannot be empty.",
                nameof(recordingId));
        }

        ArgumentNullException.ThrowIfNull(result);

        if (result.Samples.Count == 0)
        {
            throw new ArgumentException(
                "CSV result must contain at least one sample.",
                nameof(result));
        }

        _samples = Array.AsReadOnly(result.Samples.ToArray());

        for (var i = 0; i < _samples.Count; i++)
        {
            if (_samples[i].SequenceNumber != i)
            {
                throw new ArgumentException(
                    "Samples must have contiguous zero-based sequence numbers.",
                    nameof(result));
            }
        }

        RecordingId = recordingId;
    }

    public TelemetrySample Read(long sequenceNumber)
    {
        if (sequenceNumber < 0 ||
            sequenceNumber >= SampleCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sequenceNumber));
        }

        return _samples[checked((int)sequenceNumber)];
    }
}

