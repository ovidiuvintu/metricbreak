
namespace MetricBreak.Domain.Recordings;

/// <summary>
/// Identifies the exact position of a telemetry sample
/// within a recording, independently of its timestamp.
/// </summary>
public readonly record struct SamplePosition
{
    public Guid RecordingId { get; }

    public long SequenceNumber { get; }

    public SamplePosition(Guid recordingId, long sequenceNumber)
    {
        if (recordingId == Guid.Empty)
        {
            throw new ArgumentException(
                "Recording ID cannot be empty.",
                nameof(recordingId));
        }

        if (sequenceNumber < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sequenceNumber),
                "Sequence number cannot be negative.");
        }

        RecordingId = recordingId;
        SequenceNumber = sequenceNumber;
    }
}
