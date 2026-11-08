
using MetricBreak.Domain.Breakpoints;
using MetricBreak.Domain.Recordings;

namespace MetricBreak.Domain.Replay;

/// <summary>
/// Coordinates replay navigation and telemetry retrieval
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
    }

    public bool StepForward() => _cursor.StepForward();

    public bool StepBackward() => _cursor.StepBackward();

    public void Seek(long sequenceNumber) =>
        _cursor.Seek(sequenceNumber);

    public void Reset() => _cursor.Reset();


    /// <summary>
    /// Advances through the recording until the breakpoint matches
    /// or the end of the recording is reached.
    /// </summary>
    /// <returns>
    /// True if the breakpoint matched; otherwise false.
    /// </returns>
    public bool RunToBreakpoint(
    ConditionalBreakpoint breakpoint)
    {
        return RunToBreakpointHit(breakpoint) is not null;
    }


    public BreakpointHit? RunToBreakpointHit(
        ConditionalBreakpoint breakpoint)
    {
        ArgumentNullException.ThrowIfNull(breakpoint);

        while (_cursor.StepForward())
        {
            var sample = _reader.Read(
                _cursor.CurrentSequenceNumber);

            var hit = BreakpointEvaluator.EvaluateHit(
                breakpoint,
                _cursor.Position,
                sample);

            if (hit is not null)
                return hit;
        }

        return null;
    }


    /// <summary>
    /// Advances until any supplied breakpoint matches.
    /// Returns all matches at the first matching sample.
    /// </summary>
    public BreakpointStop? RunToBreakpoints(
        IEnumerable<ConditionalBreakpoint> breakpoints)
    {
        ArgumentNullException.ThrowIfNull(breakpoints);

        // Snapshot the definitions before advancing.
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

        // Reject duplicate breakpoint identities.
        if (activeBreakpoints
            .Select(b => b.Id)
            .Distinct()
            .Count() != activeBreakpoints.Length)
        {
            throw new ArgumentException(
                "Breakpoint IDs must be unique.",
                nameof(breakpoints));
        }

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
                return new BreakpointStop(
                    _cursor.Position,
                    sample.Timestamp,
                    hits);
            }
        }

        return null;
    }
}

