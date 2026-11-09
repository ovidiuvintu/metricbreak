
using System.Security.Cryptography;
using System.Text;
using MetricBreak.Domain.Recordings;

namespace MetricBreak.Ingestion.Csv;

public sealed class CsvRecordingImporter
{
    public ImportedCsvRecording Import(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        // Read the original file bytes once.
        // Preserve the exact source identity.
        var bytes = File.ReadAllBytes(filePath);

        var contentHash = Convert.ToHexString(
            SHA256.HashData(bytes));

        var source = new RecordingSource(
            Path.GetFileName(filePath),
            contentHash,
            bytes.LongLength);

        // Parse directly into indexed telemetry samples.
        // No intermediate GUID-keyed dictionaries.
        using var stream = new MemoryStream(
            bytes,
            writable: false);

        using var reader = new StreamReader(
            stream,
            new UTF8Encoding(
                encoderShouldEmitUTF8Identifier: false,
                throwOnInvalidBytes: true),
            detectEncodingFromByteOrderMarks: true);

        var parsed = new IndexedCsvTelemetryParser()
            .Parse(reader);

        var recordingId = Guid.NewGuid();

        var recording = new Recording(
            recordingId,
            Path.GetFileNameWithoutExtension(filePath),
            source,
            DateTimeOffset.UtcNow);

        var telemetryReader = new IndexedTelemetryReader(
            recordingId,
            parsed.Samples);

        return new ImportedCsvRecording(
            recording,
            parsed.Channels,
            telemetryReader);
    }
}
