
using System.Security.Cryptography;
using System.Text;
using MetricBreak.Domain.Recordings;

namespace MetricBreak.Ingestion.Csv;

public sealed class CsvRecordingImporter
{
    private readonly CsvTelemetryParser _parser = new();

    public ImportedCsvRecording Import(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        // Read the original bytes once so the hash, file size,
        // and parsed content refer to the same snapshot.
        var bytes = File.ReadAllBytes(filePath);

        var source = new RecordingSource(
            Path.GetFileName(filePath),
            Convert.ToHexString(SHA256.HashData(bytes)),
            bytes.LongLength);

        // Strict UTF-8 prevents invalid byte sequences from
        // being silently replaced during decoding.
        var encoding = new UTF8Encoding(
            encoderShouldEmitUTF8Identifier: false,
            throwOnInvalidBytes: true);

        using var stream = new MemoryStream(bytes, writable: false);
        using var textReader = new StreamReader(
            stream,
            encoding,
            detectEncodingFromByteOrderMarks: true);

        var parsed = _parser.Parse(textReader);

        var recordingId = Guid.NewGuid();

        var recording = new Recording(
            recordingId,
            Path.GetFileNameWithoutExtension(filePath),
            source,
            DateTimeOffset.UtcNow);

        var reader = new CsvTelemetryReader(recordingId, parsed);

        return new ImportedCsvRecording(
            recording,
            parsed.Channels,
            reader);
    }
}
