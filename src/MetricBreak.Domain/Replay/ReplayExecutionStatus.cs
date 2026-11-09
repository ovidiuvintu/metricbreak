namespace MetricBreak.Domain.Replay;

public enum ReplayExecutionStatus
{
    Ready,
    Running,
    Paused,
    Completed,
    Faulted
}
