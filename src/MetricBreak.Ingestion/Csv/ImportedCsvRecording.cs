
using MetricBreak.Domain.Recordings;

namespace MetricBreak.Ingestion.Csv;

public sealed class ImportedCsvRecording
{
    public Recording Recording { get; }

    public IReadOnlyList<ChannelDefinition> Channels { get; }

    public ITelemetryReader Reader { get; }

    public ImportedCsvRecording(
        Recording recording,
        IReadOnlyList<ChannelDefinition> channels,
        ITelemetryReader reader)
    {
        ArgumentNullException.ThrowIfNull(recording);
        ArgumentNullException.ThrowIfNull(channels);
        ArgumentNullException.ThrowIfNull(reader);

        if (reader.RecordingId != recording.Id)
        {
            throw new ArgumentException(
                "Reader must belong to the supplied recording.",
                nameof(reader));
        }

        Recording = recording;
        Channels = Array.AsReadOnly(channels.ToArray());
        Reader = reader;
    }
}

