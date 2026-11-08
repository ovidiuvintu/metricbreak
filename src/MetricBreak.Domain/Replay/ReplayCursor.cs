
using MetricBreak.Domain.Recordings;

namespace MetricBreak.Domain.Replay;

/// <summary>
/// Maintains the current sample position within a recording.
/// Supports deterministic forward and backward navigation.
/// </summary>
/// <remarks>
/// This class manages positions only.
/// It does not load samples, evaluate rules, or control playback timing.
/// </remarks>
public sealed class ReplayCursor
{
    public Guid RecordingId { get; }

    public long SampleCount { get; }

    public long CurrentSequenceNumber { get; private set; }

    public SamplePosition Position =>
        new(RecordingId, CurrentSequenceNumber);

    public bool CanStepForward =>
        CurrentSequenceNumber < SampleCount - 1;

    public bool CanStepBackward =>
        CurrentSequenceNumber > 0;

    public ReplayCursor(Guid recordingId, long sampleCount)
    {
        if (recordingId == Guid.Empty)
        {
            throw new ArgumentException(
                "Recording ID cannot be empty.",
                nameof(recordingId));
        }

        if (sampleCount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sampleCount),
                "Sample count must be greater than zero.");
        }

        RecordingId = recordingId;
        SampleCount = sampleCount;
        CurrentSequenceNumber = 0;
    }

    /// <summary>
    /// Advances the cursor by one sample.
    /// Returns false if already at the end.
    /// </summary>
    public bool StepForward()
    {
        if (!CanStepForward)
            return false;

        CurrentSequenceNumber++;
        return true;
    }

    /// <summary>
    /// Moves the cursor backward by one sample.
    /// Returns false if already at the beginning.
    /// </summary>
    public bool StepBackward()
    {
        if (!CanStepBackward)
            return false;

        CurrentSequenceNumber--;
        return true;
    }

    /// <summary>
    /// Moves directly to the specified sequence number.
    /// </summary>
    public void Seek(long sequenceNumber)
    {
        if (sequenceNumber < 0 || sequenceNumber >= SampleCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sequenceNumber),
                "Sequence number is outside the recording.");
        }

        CurrentSequenceNumber = sequenceNumber;
    }

    /// <summary>
    /// Returns the cursor to the first sample.
    /// </summary>
    public void Reset()
    {
        CurrentSequenceNumber = 0;
    }
}
