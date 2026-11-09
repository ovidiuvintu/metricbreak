
using MetricBreak.Domain.Breakpoints;
using MetricBreak.Domain.Recordings;

namespace MetricBreak.Domain.Replay;

/// <summary>
/// Provides a command-oriented interface for controlling
/// a deterministic replay session.
/// </summary>
public sealed class ReplayExecutionController
{
    private readonly ReplaySession _session;

    public ReplayExecutionController(ReplaySession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        _session = session;
    }

    public ReplayExecutionState State =>
        _session.ExecutionState;

    public SamplePosition Position =>
        _session.Position;

    public TelemetrySample CurrentSample =>
        _session.CurrentSample;

    public bool StepForward()
    {
        return _session.StepForward();
    }

    public bool StepBackward()
    {
        return _session.StepBackward();
    }

    public void Seek(long sequenceNumber)
    {
        _session.Seek(sequenceNumber);
    }

    public BreakpointStop? Continue(
        BreakpointCollection breakpoints)
    {
        ArgumentNullException.ThrowIfNull(breakpoints);

        return _session.RunToBreakpoints(breakpoints);
    }

    public void Reset()
    {
        _session.Reset();
    }
}
