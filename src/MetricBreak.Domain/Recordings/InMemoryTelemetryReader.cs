
using MetricBreak.Domain.Recordings;

namespace MetricBreak.Domain.Tests.TestDoubles;

/// <summary>
/// In-memory implementation of ITelemetryReader for testing.
/// Samples are indexed by their sequence number.
/// </summary>
public sealed class InMemoryTelemetryReader : ITelemetryReader
{
    private readonly TelemetrySample[] _samples;

    public Guid RecordingId { get; }

    public long SampleCount => _samples.LongLength;

    public InMemoryTelemetryReader(
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

        // Materialize the input once to prevent external
        // modifications to the collection.
        _samples = samples.ToArray();

        // Enforce the contract:
        // array index == original sample sequence number.
        for (int i = 0; i < _samples.Length; i++)
        {
            if (_samples[i] is null)
            {
                throw new ArgumentException(
                    "Samples cannot contain null entries.",
                    nameof(samples));
            }

            if (_samples[i].SequenceNumber != i)
            {
                throw new ArgumentException(
                    "Samples must have contiguous zero-based " +
                    "sequence numbers in original order.",
                    nameof(samples));
            }
        }

        RecordingId = recordingId;
    }

    /// <summary>
    /// Returns the sample at the specified sequence number.
    /// </summary>
    public TelemetrySample Read(long sequenceNumber)
    {
        if (sequenceNumber < 0 ||
            sequenceNumber >= SampleCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sequenceNumber),
                "Sequence number is outside the recording.");
        }

        return _samples[(int)sequenceNumber];
    }
}
