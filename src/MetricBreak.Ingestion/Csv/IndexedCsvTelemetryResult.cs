
using MetricBreak.Domain.Recordings;

namespace MetricBreak.Ingestion.Csv;

public sealed class IndexedCsvTelemetryResult
{
    private readonly ChannelDefinition[] _channels;
    private readonly TelemetrySample[] _samples;

    public IReadOnlyList<ChannelDefinition> Channels { get; }

    public IReadOnlyList<TelemetrySample> Samples { get; }

    public IndexedCsvTelemetryResult(
        IEnumerable<ChannelDefinition> channels,
        IEnumerable<TelemetrySample> samples)
    {
        ArgumentNullException.ThrowIfNull(channels);
        ArgumentNullException.ThrowIfNull(samples);

        _channels = channels.ToArray();
        _samples = samples.ToArray();

        Channels = Array.AsReadOnly(_channels);
        Samples = Array.AsReadOnly(_samples);
    }
}

