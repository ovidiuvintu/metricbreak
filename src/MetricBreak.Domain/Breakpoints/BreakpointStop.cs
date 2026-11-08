
using System.Collections.ObjectModel;
using MetricBreak.Domain.Recordings;

namespace MetricBreak.Domain.Breakpoints;

/// <summary>
/// Represents a replay stop caused by one or more
/// matching breakpoints at the same sample.
/// </summary>
public sealed class BreakpointStop
{
    public SamplePosition Position { get; }

    public TelemetryTimestamp Timestamp { get; }

    public IReadOnlyList<BreakpointHit> Hits { get; }

    public BreakpointStop(
        SamplePosition position,
        TelemetryTimestamp timestamp,
        IEnumerable<BreakpointHit> hits)
    {
        if (position.RecordingId == Guid.Empty ||
            position.SequenceNumber < 0)
        {
            throw new ArgumentException(
                "A valid sample position is required.",
                nameof(position));
        }

        ArgumentNullException.ThrowIfNull(hits);

        var materialized = hits.ToArray();

        if (materialized.Length == 0)
        {
            throw new ArgumentException(
                "A breakpoint stop must contain at least one hit.",
                nameof(hits));
        }

        foreach (var hit in materialized)
        {
            if (hit is null)
            {
                throw new ArgumentException(
                    "Hits cannot contain null entries.",
                    nameof(hits));
            }

            if (hit.Position != position ||
                hit.Timestamp != timestamp)
            {
                throw new ArgumentException(
                    "All hits must refer to the stop sample.",
                    nameof(hits));
            }
        }

        Position = position;
        Timestamp = timestamp;

        // Defensive copy with no mutable array exposure.
        Hits = new ReadOnlyCollection<BreakpointHit>(
            materialized);
    }
}
