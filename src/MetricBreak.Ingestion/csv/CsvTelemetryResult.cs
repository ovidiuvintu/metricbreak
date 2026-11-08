
namespace MetricBreak.Ingestion.Csv;


using MetricBreak.Domain.Recordings;

public sealed class CsvTelemetryResult
{
    public IReadOnlyList<ChannelDefinition> Channels { get; }

    public IReadOnlyList<TelemetrySample> Samples { get; }

    public CsvTelemetryResult(
        IEnumerable<ChannelDefinition> channels,
        IEnumerable<TelemetrySample> samples)
    {
        ArgumentNullException.ThrowIfNull(channels);
        ArgumentNullException.ThrowIfNull(samples);

        Channels = Array.AsReadOnly(channels.ToArray());
        Samples = Array.AsReadOnly(samples.ToArray());
    }
}
