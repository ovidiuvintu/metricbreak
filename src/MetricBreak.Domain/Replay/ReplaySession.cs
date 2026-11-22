
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
    private readonly Func<long, TelemetrySample> _readSample;

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
        _readSample = sequence => _reader.Read(sequence);

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
                    sample,
                    _cursor.CurrentSequenceNumber > 0 ? _reader.Read(_cursor.CurrentSequenceNumber - 1) : null);

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
    /// Advances until any enabled breakpoint matches.
    /// Supports conditional and composite breakpoints.
    /// Does not advance if none are enabled.
    /// </summary>
    public BreakpointStop? RunToBreakpoints(
        BreakpointCollection breakpoints)
    {
        ArgumentNullException.ThrowIfNull(breakpoints);

        var active = breakpoints.GetAllActive();

        if (active.Count == 0)
            return null;

        return ExecuteToBreakpoints(active, forward: true);
    }

    /// <summary>
    /// Advances in the requested direction until the breakpoint matches
    /// or the recording boundary is reached. Ignores global enabled state.
    /// </summary>
    public BreakpointStop? RunToBreakpoint(
        IBreakpoint breakpoint,
        bool forward)
    {
        ArgumentNullException.ThrowIfNull(breakpoint);
        return ExecuteToBreakpoints(new[] { breakpoint }, forward);
    }

    /// <summary>
    /// Moves backward until any enabled breakpoint matches.
    /// Does not move if none are enabled.
    /// </summary>
    public BreakpointStop? RunBackwardToBreakpoints(
        BreakpointCollection breakpoints)
    {
        ArgumentNullException.ThrowIfNull(breakpoints);

        var active = breakpoints.GetAllActive();

        if (active.Count == 0)
            return null;

        return ExecuteToBreakpoints(active, forward: false);
    }

    /// <summary>
    /// Legacy overload for conditional breakpoints.
    /// Preserved for existing callers.
    /// </summary>
    public BreakpointStop? RunToBreakpoints(
        IEnumerable<ConditionalBreakpoint> breakpoints)
    {
        var active = ValidateBreakpoints(breakpoints);

        return ExecuteToBreakpoints(active, forward: true);
    }

    /// <summary>
    /// Legacy overload for conditional breakpoints.
    /// Preserved for existing callers.
    /// </summary>
    public BreakpointStop? RunBackwardToBreakpoints(
        IEnumerable<ConditionalBreakpoint> breakpoints)
    {
        var active = ValidateBreakpoints(breakpoints);

        return ExecuteToBreakpoints(active, forward: false);
    }

    /// <summary>
    /// Validates a supplied conditional breakpoint sequence
    /// before changing replay execution state.
    /// </summary>
    private static IReadOnlyList<IBreakpoint> ValidateBreakpoints(
        IEnumerable<ConditionalBreakpoint> breakpoints)
    {
        ArgumentNullException.ThrowIfNull(breakpoints);

        var active = breakpoints.ToArray();

        if (active.Any(b => b is null))
        {
            throw new ArgumentException(
                "Breakpoint collection cannot contain null entries.",
                nameof(breakpoints));
        }

        if (active.Length == 0)
        {
            throw new ArgumentException(
                "At least one breakpoint is required.",
                nameof(breakpoints));
        }

        if (active.Select(b => b.Id).Distinct().Count()
            != active.Length)
        {
            throw new ArgumentException(
                "Breakpoint IDs must be unique.",
                nameof(breakpoints));
        }

        return active;
    }

    /// <summary>
    /// Executes replay in the specified direction until
    /// one or more breakpoints match at the same sample.
    /// </summary>
    private BreakpointStop? ExecuteToBreakpoints(
        IReadOnlyList<IBreakpoint> activeBreakpoints,
        bool forward)
    {
        try
        {
            UpdateExecutionState(
                ReplayExecutionStatus.Running,
                ReplayPauseReason.None);

            while (forward
                ? _cursor.StepForward()
                : _cursor.StepBackward())
            {
                var sample = _reader.Read(
                    _cursor.CurrentSequenceNumber);

                var hits = new List<BreakpointHit>();

                foreach (var breakpoint in activeBreakpoints)
                {
                    var hit = BreakpointEvaluator.EvaluateHit(
                        breakpoint,
                        _cursor.Position,
                        sample,
                        _cursor.CurrentSequenceNumber > 0 ? _reader.Read(_cursor.CurrentSequenceNumber - 1) : null,
                        _readSample);

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
                forward
                    ? ReplayPauseReason.EndOfRecording
                    : ReplayPauseReason.StartOfRecording);

            return null;
        }
        catch
        {
            SetFaultedState();
            throw;
        }
    }

    /// <summary>
    /// Advances at most maxSamples, evaluating every traversed sample.
    /// A completed batch leaves the session Running.
    /// </summary>
    public PlaybackAdvanceResult AdvancePlayback(int maxSamples, BreakpointCollection breakpoints)
    {
        ArgumentNullException.ThrowIfNull(breakpoints);
        if (maxSamples <= 0 || maxSamples > 5000)
            throw new ArgumentOutOfRangeException(nameof(maxSamples));

        var active = breakpoints.GetAllActive();
        var advanced = 0;
        try
        {
            if (!_cursor.CanStepForward)
            {
                UpdateExecutionState(ReplayExecutionStatus.Completed, ReplayPauseReason.EndOfRecording);
                return new PlaybackAdvanceResult(0, true, null);
            }

            UpdateExecutionState(ReplayExecutionStatus.Running, ReplayPauseReason.None);
            while (advanced < maxSamples && _cursor.StepForward())
            {
                advanced++;
                var sample = _reader.Read(_cursor.CurrentSequenceNumber);
                var hits = new List<BreakpointHit>();
                foreach (var breakpoint in active)
                {
                    var hit = BreakpointEvaluator.EvaluateHit(breakpoint, _cursor.Position, sample,
                        _cursor.CurrentSequenceNumber > 0 ? _reader.Read(_cursor.CurrentSequenceNumber - 1) : null,
                        _readSample);
                    if (hit is not null)
                        hits.Add(hit);
                }

                if (hits.Count > 0)
                {
                    UpdateExecutionState(ReplayExecutionStatus.Paused, ReplayPauseReason.Breakpoint, sample);
                    return new PlaybackAdvanceResult(advanced, false,
                        new BreakpointStop(_cursor.Position, sample.Timestamp, hits));
                }
            }

            if (!_cursor.CanStepForward)
            {
                UpdateExecutionState(ReplayExecutionStatus.Completed, ReplayPauseReason.EndOfRecording);
                return new PlaybackAdvanceResult(advanced, true, null);
            }

            UpdateExecutionState(ReplayExecutionStatus.Running, ReplayPauseReason.None);
            return new PlaybackAdvanceResult(advanced, false, null);
        }
        catch
        {
            SetFaultedState();
            throw;
        }
    }

    /// <summary>Pauses an active playback batch sequence at its current position.</summary>
    public void PausePlayback()
    {
        if (ExecutionState.Status == ReplayExecutionStatus.Running)
            UpdateExecutionState(ReplayExecutionStatus.Paused, ReplayPauseReason.User);
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

    /// <summary>
  /// Scans a sequence range for breakpoint matches without moving the replay cursor.
  /// </summary>
    public IReadOnlyList<BreakpointHitOccurrence> FindBreakpointHitsInRange(
        IEnumerable<IBreakpoint> breakpoints,
        long fromSequenceInclusive,
        long toSequenceExclusive,
        int maxHits = 2000)
    {
        ArgumentNullException.ThrowIfNull(breakpoints);

        if (maxHits <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxHits));

        var active = breakpoints as IReadOnlyList<IBreakpoint>
            ?? breakpoints.Where(b => b is not null).ToArray();

        if (active.Count == 0)
            return Array.Empty<BreakpointHitOccurrence>();

        var from = Math.Max(0, fromSequenceInclusive);
        var to = Math.Min(SampleCount, Math.Max(from, toSequenceExclusive));

        var results = new List<BreakpointHitOccurrence>(Math.Min(maxHits, 64));
        for (var sequence = from; sequence < to && results.Count < maxHits; sequence++)
        {
            var sample = _reader.Read(sequence);
            var previous = sequence > 0 ? _reader.Read(sequence - 1) : null;
            var position = new SamplePosition(RecordingId, sequence);

            foreach (var breakpoint in active)
            {
                if (results.Count >= maxHits)
                    break;

                if (BreakpointEvaluator.EvaluateHit(
                        breakpoint,
                        position,
                        sample,
                        previous,
                        _readSample) is null)
                {
                    continue;
                }

                results.Add(new BreakpointHitOccurrence(breakpoint.Id, sequence));
            }
        }

        return results;
    }

    /// <summary>
    /// Counts threshold crossings and time on each side without moving the cursor.
    /// </summary>
    public IReadOnlyList<BreakpointCoverage> MeasureBreakpointCoverage(
        IReadOnlyList<IBreakpoint> breakpoints) =>
        BreakpointCoverageAnalyzer.Measure(
            breakpoints,
            SampleCount,
            _readSample);
}
