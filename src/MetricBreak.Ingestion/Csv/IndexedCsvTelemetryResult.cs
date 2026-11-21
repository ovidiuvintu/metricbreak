
using MetricBreak.Domain.Recordings;

namespace MetricBreak.Ingestion.Csv;

public sealed class IndexedCsvTelemetryResult
{
    private readonly ChannelDefinition[] _channels;
    private readonly TelemetrySample[] _samples;

    public IReadOnlyList<ChannelDefinition> Channels { get; }

    public IReadOnlyList<TelemetrySample> Samples { get; }

    public TimestampOrigin TimestampOrigin { get; }

    public IndexedCsvTelemetryResult(
        IEnumerable<ChannelDefinition> channels,
        IEnumerable<TelemetrySample> samples,
        TimestampOrigin timestampOrigin = TimestampOrigin.Unix)
    {
        ArgumentNullException.ThrowIfNull(channels);
        ArgumentNullException.ThrowIfNull(samples);
        if (!Enum.IsDefined(timestampOrigin))
            throw new ArgumentOutOfRangeException(nameof(timestampOrigin));

        _channels = channels.ToArray();
        _samples = samples.ToArray();

        Channels = Array.AsReadOnly(_channels);
        Samples = Array.AsReadOnly(_samples);
        TimestampOrigin = timestampOrigin;
    }
}

