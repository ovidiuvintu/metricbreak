
using MetricBreak.Domain.Recordings;

namespace MetricBreak.Domain.Replay;

public sealed record ReplayExecutionState
{
    public ReplayExecutionStatus Status { get; }

    public ReplayPauseReason PauseReason { get; }

    public SamplePosition? Position { get; }

    public TelemetryTimestamp? Timestamp { get; }

    public ReplayExecutionState(
        ReplayExecutionStatus status,
        ReplayPauseReason pauseReason,
        SamplePosition? position,
        TelemetryTimestamp? timestamp)
    {
        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(
                nameof(status));
        }

        if (!Enum.IsDefined(pauseReason))
        {
            throw new ArgumentOutOfRangeException(
                nameof(pauseReason));
        }

        if (position.HasValue != timestamp.HasValue)
        {
            throw new ArgumentException(
                "_position and timestamp must either both be present or both be absent.");
        }

        if (status == ReplayExecutionStatus.Ready &&
            (pauseReason != ReplayPauseReason.None ||
             position.HasValue))
        {
            throw new ArgumentException(
                "A ready replay cannot have a pause reason or current position.");
        }

        if (status == ReplayExecutionStatus.Running &&
            pauseReason != ReplayPauseReason.None)
        {
            throw new ArgumentException(
                "A running replay cannot have a pause reason.");
        }

        if (status == ReplayExecutionStatus.Paused &&
            pauseReason is ReplayPauseReason.None or
                ReplayPauseReason.EndOfRecording)
        {
            throw new ArgumentException(
                "A paused replay requires a valid pause reason.");
        }

        if (status == ReplayExecutionStatus.Completed &&
            pauseReason is not ReplayPauseReason.EndOfRecording
                and not ReplayPauseReason.StartOfRecording)
        {
            throw new ArgumentException(
                "A completed replay must indicate a recording boundary.");
        }

        if (status == ReplayExecutionStatus.Faulted &&
            pauseReason != ReplayPauseReason.None)
        {
            throw new ArgumentException(
                "A faulted replay cannot have a pause reason.");
        }

        Status = status;
        PauseReason = pauseReason;
        Position = position;
        Timestamp = timestamp;
    }
}
