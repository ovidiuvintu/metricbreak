
using System.Collections.ObjectModel;

namespace MetricBreak.Domain.Recordings;

public sealed class TelemetrySample
{
    public long SequenceNumber { get; }

    public TelemetryTimestamp Timestamp { get; }

    public IReadOnlyDictionary<Guid, double?> Values { get; }

    // Existing dictionary-backed constructor.
    public TelemetrySample(
        long sequenceNumber,
        TelemetryTimestamp timestamp,
        IReadOnlyDictionary<Guid, double?> values)
    {
        if (sequenceNumber < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sequenceNumber));
        }

        ArgumentNullException.ThrowIfNull(values);

        foreach (var channelId in values.Keys)
        {
            if (channelId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Channel IDs cannot be empty.",
                    nameof(values));
            }
        }

        SequenceNumber = sequenceNumber;
        Timestamp = timestamp;

        var copy = new Dictionary<Guid, double?>(values);

        Values = new ReadOnlyDictionary<Guid, double?>(copy);
    }

    // New indexed constructor.

    public TelemetrySample(
        long sequenceNumber,
        TelemetryTimestamp timestamp,
        ChannelIndex channelIndex,
        IReadOnlyList<Guid> channelIds,
        double?[] values)
    {
        if (sequenceNumber < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sequenceNumber));
        }

        ArgumentNullException.ThrowIfNull(channelIndex);
        ArgumentNullException.ThrowIfNull(channelIds);
        ArgumentNullException.ThrowIfNull(values);

        if (channelIndex.Count != channelIds.Count)
        {
            throw new ArgumentException(
                "Channel count must match the index.",
                nameof(channelIds));
        }

        for (var position = 0; position < channelIds.Count; position++)
        {
            if (channelIndex.ChannelIds[position] != channelIds[position])
            {
                throw new ArgumentException(
                    "Channel order must match the index.",
                    nameof(channelIds));
            }
        }

        SequenceNumber = sequenceNumber;
        Timestamp = timestamp;

        var copiedValues = (double?[])values.Clone();

        Values = new IndexedValueDictionary(
            channelIndex,
            copiedValues);
    }


    public TelemetrySample(
        long sequenceNumber,
        TelemetryTimestamp timestamp,
        ChannelIndex channelIndex,
        double?[] values)
    {
        if (sequenceNumber < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sequenceNumber));
        }

        ArgumentNullException.ThrowIfNull(channelIndex);
        ArgumentNullException.ThrowIfNull(values);

        SequenceNumber = sequenceNumber;
        Timestamp = timestamp;

        var copiedValues = (double?[])values.Clone();

        Values = new IndexedValueDictionary(
            channelIndex,
            copiedValues);
    }



    public double? GetValue(Guid channelId)
    {
        return Values.TryGetValue(channelId, out var value)
            ? value
            : null;
    }

    public bool HasChannel(Guid channelId)
    {
        return Values.ContainsKey(channelId);
    }
}
