
using MetricBreak.Domain.Breakpoints;
using MetricBreak.Domain.Recordings;

namespace MetricBreak.Domain.Replay;

/// <summary>
/// Coordinates deterministic replay navigation,
/// breakpoint evaluation, and execution-state tracking
/// for a single recording.
/// </summary>
public sealed class ReplaySession
{
    private readonly ITelemetryReader _reader;
    private readonly ReplayCursor _cursor;

    public Guid RecordingId => _cursor.RecordingId;

    public long SampleCount => _cursor.SampleCount;

    public SamplePosition Position => _cursor.Position;

    public bool CanStepForward => _cursor.CanStepForward;

    public bool CanStepBackward => _cursor.CanStepBackward;

    public TelemetrySample CurrentSample =>
        _reader.Read(_cursor.CurrentSequenceNumber);

    /// <summary>
    /// Immutable snapshot of the current execution state.
    /// </summary>
    public ReplayExecutionState ExecutionState { get; private set; }

    public ReplaySession(
        Recording recording,
        ITelemetryReader reader)
    {
        ArgumentNullException.ThrowIfNull(recording);
        ArgumentNullException.ThrowIfNull(reader);

        if (recording.Id != reader.RecordingId)
        {
            throw new ArgumentException(
                "The reader belongs to a different recording.",
                nameof(reader));
        }

        if (reader.SampleCount <= 0)
        {
            throw new ArgumentException(
                "Cannot replay a recording without samples.",
                nameof(reader));
        }

        _reader = reader;

        _cursor = new ReplayCursor(
            recording.Id,
            reader.SampleCount);

        ExecutionState = new ReplayExecutionState(
            ReplayExecutionStatus.Ready,
            ReplayPauseReason.None,
            null,
            null);
    }

    /// <summary>
    /// Advances to the next sample.
    /// Returns false when already at the end.
    /// </summary>
    public bool StepForward()
    {
        if (!_cursor.CanStepForward)
            return false;

        try
        {
            _cursor.StepForward();

            UpdateExecutionState(
                ReplayExecutionStatus.Paused,
                ReplayPauseReason.User);

            return true;
        }
        catch
        {
            SetFaultedState();
            throw;
        }
    }

    /// <summary>
    /// Moves to the previous sample.
    /// Returns false when already at the beginning.
    /// </summary>
    public bool StepBackward()
    {
        if (!_cursor.CanStepBackward)
            return false;

        try
        {
            _cursor.StepBackward();

            UpdateExecutionState(
                ReplayExecutionStatus.Paused,
                ReplayPauseReason.User);

            return true;
        }
        catch
        {
            SetFaultedState();
            throw;
        }
    }

    /// <summary>
    /// Moves directly to the specified sequence number.
    /// </summary>
    public void Seek(long sequenceNumber)
    {
        // Validate before changing execution state.
        // An invalid seek is a caller error, not a replay fault.
        if (sequenceNumber < 0 ||
            sequenceNumber >= SampleCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sequenceNumber));
        }

        try
        {
            _cursor.Seek(sequenceNumber);

            UpdateExecutionState(
                ReplayExecutionStatus.Paused,
                ReplayPauseReason.User);
        }
        catch
        {
            SetFaultedState();
            throw;
        }
    }

    /// <summary>
    /// Restores the cursor and execution state
    /// to their initial values.
    /// </summary>
    public void Reset()
    {
        _cursor.Reset();

        ExecutionState = new ReplayExecutionState(
            ReplayExecutionStatus.Ready,
            ReplayPauseReason.None,
            null,
            null);
    }

    /// <summary>
    /// Advances until the breakpoint matches
    /// or the recording is exhausted.
    /// </summary>
    public bool RunToBreakpoint(
        ConditionalBreakpoint breakpoint)
    {
        return RunToBreakpointHit(breakpoint) is not null;
    }

    /// <summary>
    /// Advances until a breakpoint matches.
    /// Returns the first matching hit, or null.
    /// </summary>
    public BreakpointHit? RunToBreakpointHit(
        ConditionalBreakpoint breakpoint)
    {
        ArgumentNullException.ThrowIfNull(breakpoint);

        try
        {
            UpdateExecutionState(
                ReplayExecutionStatus.Running,
                ReplayPauseReason.None);

            while (_cursor.StepForward())
            {
                var sample = _reader.Read(
                    _cursor.CurrentSequenceNumber);

                var hit = BreakpointEvaluator.EvaluateHit(
                    breakpoint,
                    _cursor.Position,
                    sample);

                if (hit is not null)
                {
                    UpdateExecutionState(
                        ReplayExecutionStatus.Paused,
                        ReplayPauseReason.Breakpoint,
                        sample);

                    return hit;
                }
            }

            UpdateExecutionState(
                ReplayExecutionStatus.Completed,
                ReplayPauseReason.EndOfRecording);

            return null;
        }
        catch
        {
            SetFaultedState();
            throw;
        }
    }

    /// <summary>
    /// Advances until an enabled breakpoint matches.
    /// Does not advance if none are enabled.
    /// </summary>
    public BreakpointStop? RunToBreakpoints(
        BreakpointCollection breakpoints)
    {
        ArgumentNullException.ThrowIfNull(breakpoints);

        var active = breakpoints.GetActive();

        if (active.Count == 0)
            return null;

        return RunToBreakpoints(
            (IEnumerable<ConditionalBreakpoint>)active);
    }

    /// <summary>
    /// Moves backward until an enabled breakpoint matches.
    /// Does not move if none are enabled.
    /// </summary>
    public BreakpointStop? RunBackwardToBreakpoints(
        BreakpointCollection breakpoints)
    {
        ArgumentNullException.ThrowIfNull(breakpoints);

        var active = breakpoints.GetActive();

        if (active.Count == 0)
            return null;

        return RunBackwardToBreakpoints(
            (IEnumerable<ConditionalBreakpoint>)active);
    }

    /// <summary>
    /// Advances until any supplied breakpoint matches.
    /// Returns all hits at the first matching sample.
    /// </summary>
    public BreakpointStop? RunToBreakpoints(
        IEnumerable<ConditionalBreakpoint> breakpoints)
    {
        ArgumentNullException.ThrowIfNull(breakpoints);

        // Validate before changing execution state.
        var activeBreakpoints = breakpoints.ToArray();

        if (activeBreakpoints.Any(b => b is null))
        {
            throw new ArgumentException(
                "Breakpoint collection cannot contain null entries.",
                nameof(breakpoints));
        }

        if (activeBreakpoints.Length == 0)
        {
            throw new ArgumentException(
                "At least one breakpoint is required.",
                nameof(breakpoints));
        }

        if (activeBreakpoints
            .Select(b => b.Id)
            .Distinct()
            .Count() != activeBreakpoints.Length)
        {
            throw new ArgumentException(
                "Breakpoint IDs must be unique.",
                nameof(breakpoints));
        }

        try
        {
            UpdateExecutionState(
                ReplayExecutionStatus.Running,
                ReplayPauseReason.None);

            while (_cursor.StepForward())
            {
                var sample = _reader.Read(
                    _cursor.CurrentSequenceNumber);

                var hits = new List<BreakpointHit>();

                foreach (var breakpoint in activeBreakpoints)
                {
                    var hit = BreakpointEvaluator.EvaluateHit(
                        breakpoint,
                        _cursor.Position,
                        sample);

                    if (hit is not null)
                        hits.Add(hit);
                }

                if (hits.Count > 0)
                {
                    UpdateExecutionState(
                        ReplayExecutionStatus.Paused,
                        ReplayPauseReason.Breakpoint,
                        sample);

                    return new BreakpointStop(
                        _cursor.Position,
                        sample.Timestamp,
                        hits);
                }
            }

            UpdateExecutionState(
                ReplayExecutionStatus.Completed,
                ReplayPauseReason.EndOfRecording);

            return null;
        }
        catch
        {
            SetFaultedState();
            throw;
        }
    }

    /// <summary>
    /// Moves backward until any supplied breakpoint matches.
    /// Returns all hits at the first matching sample.
    /// </summary>
    public BreakpointStop? RunBackwardToBreakpoints(
        IEnumerable<ConditionalBreakpoint> breakpoints)
    {
        ArgumentNullException.ThrowIfNull(breakpoints);

        var activeBreakpoints = breakpoints.ToArray();

        if (activeBreakpoints.Any(b => b is null))
        {
            throw new ArgumentException(
                "Breakpoint collection cannot contain null entries.",
                nameof(breakpoints));
        }

        if (activeBreakpoints.Length == 0)
        {
            throw new ArgumentException(
                "At least one breakpoint is required.",
                nameof(breakpoints));
        }

        if (activeBreakpoints
            .Select(b => b.Id)
            .Distinct()
            .Count() != activeBreakpoints.Length)
        {
            throw new ArgumentException(
                "Breakpoint IDs must be unique.",
                nameof(breakpoints));
        }

        try
        {
            UpdateExecutionState(
                ReplayExecutionStatus.Running,
                ReplayPauseReason.None);

            while (_cursor.StepBackward())
            {
                var sample = _reader.Read(
                    _cursor.CurrentSequenceNumber);

                var hits = new List<BreakpointHit>();

                foreach (var breakpoint in activeBreakpoints)
                {
                    var hit = BreakpointEvaluator.EvaluateHit(
                        breakpoint,
                        _cursor.Position,
                        sample);

                    if (hit is not null)
                        hits.Add(hit);
                }

                if (hits.Count > 0)
                {
                    UpdateExecutionState(
                        ReplayExecutionStatus.Paused,
                        ReplayPauseReason.Breakpoint,
                        sample);

                    return new BreakpointStop(
                        _cursor.Position,
                        sample.Timestamp,
                        hits);
                }
            }

            UpdateExecutionState(
                ReplayExecutionStatus.Completed,
                ReplayPauseReason.StartOfRecording);

            return null;
        }
        catch
        {
            SetFaultedState();
            throw;
        }
    }

    /// <summary>
    /// Creates a new immutable execution-state snapshot
    /// using the current cursor position.
    /// </summary>
    private void UpdateExecutionState(
        ReplayExecutionStatus status,
        ReplayPauseReason reason,
        TelemetrySample? sample = null)
    {
        if (status == ReplayExecutionStatus.Ready)
        {
            ExecutionState = new ReplayExecutionState(
                status,
                reason,
                null,
                null);

            return;
        }

        sample ??= CurrentSample;

        ExecutionState = new ReplayExecutionState(
            status,
            reason,
            _cursor.Position,
            sample.Timestamp);
    }

    /// <summary>
    /// Marks execution as faulted without attempting
    /// another telemetry read.
    /// </summary>
    private void SetFaultedState()
    {
        ExecutionState = new ReplayExecutionState(
            ReplayExecutionStatus.Faulted,
            ReplayPauseReason.None,
            null,
            null);
    }
}
