
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
    public bool RunToBreakpoint(ConditionalBreakpoint breakpoint)
    {
        ArgumentNullException.ThrowIfNull(breakpoint);

        while (_cursor.StepForward())
        {
            var sample = _reader.Read(_cursor.CurrentSequenceNumber);

            if (BreakpointEvaluator.Evaluate(breakpoint, sample))
                return true;
        }

        return false;
    }

}

