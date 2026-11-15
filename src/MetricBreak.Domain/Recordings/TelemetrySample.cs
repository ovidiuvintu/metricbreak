using System.Collections.ObjectModel;

namespace MetricBreak.Domain.Recordings;

public sealed class TelemetrySample
{
    public long SequenceNumber { get; }
    public TelemetryTimestamp Timestamp { get; }

    // Numeric-only compatibility view. For typed samples, nonnumeric channels are absent.
    public IReadOnlyDictionary<Guid, double?> Values { get; }

    // Includes all known channels; null represents a missing measurement.
    public IReadOnlyDictionary<Guid, ChannelValue?> TypedValues { get; }

    // Existing dictionary-backed constructor.
    public TelemetrySample(
        long sequenceNumber,
        TelemetryTimestamp timestamp,
        IReadOnlyDictionary<Guid, double?> values)
    {
        ValidateSequence(sequenceNumber);
        ArgumentNullException.ThrowIfNull(values);
        ValidateIds(values.Keys);

        SequenceNumber = sequenceNumber;
        Timestamp = timestamp;

        var numericCopy = new Dictionary<Guid, double?>(values);
        Values = new ReadOnlyDictionary<Guid, double?>(numericCopy);
        TypedValues = ToTyped(numericCopy);
    }

    // Existing indexed constructor with explicit channel ordering.
    public TelemetrySample(
        long sequenceNumber,
        TelemetryTimestamp timestamp,
        ChannelIndex channelIndex,
        IReadOnlyList<Guid> channelIds,
        double?[] values)
    {
        ValidateSequence(sequenceNumber);
        ArgumentNullException.ThrowIfNull(channelIndex);
        ArgumentNullException.ThrowIfNull(channelIds);
        ArgumentNullException.ThrowIfNull(values);

        if (channelIndex.Count != channelIds.Count)
            throw new ArgumentException("Channel count must match the index.", nameof(channelIds));

        for (var position = 0; position < channelIds.Count; position++)
        {
            if (channelIndex.ChannelIds[position] != channelIds[position])
                throw new ArgumentException("Channel order must match the index.", nameof(channelIds));
        }

        SequenceNumber = sequenceNumber;
        Timestamp = timestamp;

        var copiedValues = (double?[])values.Clone();
        Values = new IndexedValueDictionary(channelIndex, copiedValues);
        TypedValues = ToTyped(Values);
    }

    // Existing indexed constructor.
    public TelemetrySample(
        long sequenceNumber,
        TelemetryTimestamp timestamp,
        ChannelIndex channelIndex,
        double?[] values)
    {
        ValidateSequence(sequenceNumber);
        ArgumentNullException.ThrowIfNull(channelIndex);
        ArgumentNullException.ThrowIfNull(values);

        SequenceNumber = sequenceNumber;
        Timestamp = timestamp;

        var copiedValues = (double?[])values.Clone();
        Values = new IndexedValueDictionary(channelIndex, copiedValues);
        TypedValues = ToTyped(Values);
    }

    private TelemetrySample(
        long sequenceNumber,
        TelemetryTimestamp timestamp,
        IReadOnlyDictionary<Guid, ChannelValue?> typedValues,
        bool typedMarker)
    {
        ValidateSequence(sequenceNumber);
        ArgumentNullException.ThrowIfNull(typedValues);
        ValidateIds(typedValues.Keys);

        SequenceNumber = sequenceNumber;
        Timestamp = timestamp;

        var typedCopy = new Dictionary<Guid, ChannelValue?>(typedValues);
        TypedValues = new ReadOnlyDictionary<Guid, ChannelValue?>(typedCopy);
        Values = NumericProjection(typedCopy);
    }

    // Named factory avoids ambiguous overloads for null dictionary arguments.
    public static TelemetrySample FromTypedValues(
        long sequenceNumber,
        TelemetryTimestamp timestamp,
        IReadOnlyDictionary<Guid, ChannelValue?> values)
        => new(sequenceNumber, timestamp, values, typedMarker: true);

    // Indexed typed factory preserves channel order and includes missing channels.
    public static TelemetrySample FromIndexedTypedValues(
        long sequenceNumber,
        TelemetryTimestamp timestamp,
        ChannelIndex channelIndex,
        ChannelValue?[] values)
    {
        ArgumentNullException.ThrowIfNull(channelIndex);
        ArgumentNullException.ThrowIfNull(values);

        if (channelIndex.Count != values.Length)
            throw new ArgumentException("Value count must match the channel index.", nameof(values));

        var copy = new Dictionary<Guid, ChannelValue?>(values.Length);
        for (var i = 0; i < values.Length; i++)
            copy.Add(channelIndex.ChannelIds[i], values[i]);

        return FromTypedValues(sequenceNumber, timestamp, copy);
    }

    public double? GetValue(Guid channelId)
        => Values.TryGetValue(channelId, out var value) ? value : null;

    public ChannelValue? GetTypedValue(Guid channelId)
        => TypedValues.TryGetValue(channelId, out var value) ? value : null;

    public bool HasChannel(Guid channelId)
        => TypedValues.ContainsKey(channelId);

    private static void ValidateSequence(long sequenceNumber)
    {
        if (sequenceNumber < 0)
            throw new ArgumentOutOfRangeException(nameof(sequenceNumber));
    }

    private static void ValidateIds(IEnumerable<Guid> channelIds)
    {
        if (channelIds.Any(id => id == Guid.Empty))
            throw new ArgumentException("Channel IDs cannot be empty.", nameof(channelIds));
    }

    private static IReadOnlyDictionary<Guid, ChannelValue?> ToTyped(
        IReadOnlyDictionary<Guid, double?> numericValues)
    {
        var copy = new Dictionary<Guid, ChannelValue?>(numericValues.Count);
        foreach (var (id, value) in numericValues)
            copy.Add(id, value.HasValue ? new ChannelValue.Numeric(value.Value) : null);

        return new ReadOnlyDictionary<Guid, ChannelValue?>(copy);
    }

    private static IReadOnlyDictionary<Guid, double?> NumericProjection(
        IReadOnlyDictionary<Guid, ChannelValue?> typedValues)
    {
        var copy = new Dictionary<Guid, double?>();
        foreach (var (id, value) in typedValues)
        {
            if (value is ChannelValue.Numeric numeric)
                copy.Add(id, numeric.Value);
            else if (value is null)
            {
                // Without channel definitions, the value alone cannot identify
                // whether a missing measurement belongs to a numeric channel.
                // Such channels remain in TypedValues, not in the numeric view.
            }
        }

        return new ReadOnlyDictionary<Guid, double?>(copy);
    }
}
