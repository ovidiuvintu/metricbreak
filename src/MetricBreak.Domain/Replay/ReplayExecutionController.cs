
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

    public BreakpointStop? ContinueBackward(
        BreakpointCollection breakpoints)
    {
        ArgumentNullException.ThrowIfNull(breakpoints);

        return _session.RunBackwardToBreakpoints(breakpoints);
    }

    public BreakpointStop? RunToBreakpoint(
        IBreakpoint breakpoint,
        bool forward)
    {
        ArgumentNullException.ThrowIfNull(breakpoint);

        return _session.RunToBreakpoint(breakpoint, forward);
    }

    public void Reset()
    {
        _session.Reset();
    }

    public PlaybackAdvanceResult AdvancePlayback(
        int maxSamples,
        BreakpointCollection breakpoints)
    {
        ArgumentNullException.ThrowIfNull(breakpoints);

        return _session.AdvancePlayback(
            maxSamples,
            breakpoints);
    }

    public void PausePlayback()
    {
        _session.PausePlayback();
    }

    public IReadOnlyList<BreakpointHitOccurrence> FindBreakpointHitsInRange(
        BreakpointCollection breakpoints,
        long fromSequenceInclusive,
        long toSequenceExclusive,
        int maxHits = 2000)
    {
        ArgumentNullException.ThrowIfNull(breakpoints);

        return _session.FindBreakpointHitsInRange(
            breakpoints.GetAllActive(),
            fromSequenceInclusive,
            toSequenceExclusive,
            maxHits);
    }

}
