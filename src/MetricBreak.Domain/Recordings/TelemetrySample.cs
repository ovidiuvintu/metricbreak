
using System.Collections.ObjectModel;

namespace MetricBreak.Domain.Recordings;

/// <summary>
/// Represents a single immutable telemetry sample within a recording.
/// </summary>
/// <remarks>
/// A sample is uniquely identified within its recording by its
/// sequence number, not its timestamp.
///
/// Multiple samples may have identical timestamps.
///
/// This class is intended as a logical representation of a sample,
/// not as the bulk storage format for millions of measurements.
/// </remarks>
public sealed class TelemetrySample
{
    /// <summary>
    /// Original zero-based position of the sample within its recording.
    /// </summary>
    public long SequenceNumber { get; }

    /// <summary>
    /// Absolute UTC timestamp with nanosecond precision.
    /// </summary>
    public TelemetryTimestamp Timestamp { get; }

    /// <summary>
    /// Channel measurements indexed by channel ID.
    /// A null value represents a missing measurement.
    /// </summary>
    public IReadOnlyDictionary<Guid, double?> Values { get; }

    public TelemetrySample(
        long sequenceNumber,
        TelemetryTimestamp timestamp,
        IReadOnlyDictionary<Guid, double?> values)
    {
        if (sequenceNumber < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sequenceNumber),
                "Sequence number cannot be negative.");
        }

        ArgumentNullException.ThrowIfNull(values);

        if (values.ContainsKey(Guid.Empty))
        {
            throw new ArgumentException(
                "Channel IDs cannot be empty.",
                nameof(values));
        }

        SequenceNumber = sequenceNumber;
        Timestamp = timestamp;

        // Defensive copy prevents external modification.
        Values = new ReadOnlyDictionary<Guid, double?>(
            new Dictionary<Guid, double?>(values));
    }

    /// <summary>
    /// Returns the measurement for a channel, or null when the
    /// channel is absent or its measurement is missing.
    /// </summary>
    public double? GetValue(Guid channelId)
    {
        if (channelId == Guid.Empty)
        {
            throw new ArgumentException(
                "Channel ID cannot be empty.",
                nameof(channelId));
        }

        return Values.TryGetValue(channelId, out var value)
            ? value
            : null;
    }

    /// <summary>
    /// Determines whether this sample contains the specified channel.
    /// </summary>
    public bool HasChannel(Guid channelId)
    {
        if (channelId == Guid.Empty)
        {
            return false;
        }

        return Values.ContainsKey(channelId);
    }
}
