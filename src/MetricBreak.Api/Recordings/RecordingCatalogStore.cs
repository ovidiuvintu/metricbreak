using System.Text.Json;
using MetricBreak.Api.Persistence;
using MetricBreak.Ingestion.Csv;

namespace MetricBreak.Api.Recordings;

public sealed class RecordingCatalogStore
{
    private const int _catalogSchemaVersion = 1;

    private static readonly JsonSerializerOptions _jsonOptions =
        new() { WriteIndented = true };

    private readonly string _catalogPath;
    private readonly string _archiveDirectory;

    public RecordingCatalogStore(
        string? catalogPath = null,
        string? archiveDirectory = null)
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MetricBreak");

        _catalogPath = Path.GetFullPath(
            catalogPath ?? Path.Combine(root, "recordings-catalog.json"));

        _archiveDirectory = Path.GetFullPath(
            archiveDirectory ?? Path.Combine(root, "recordings"));
    }

    public IReadOnlyList<RecordingCatalogItemResponse> List()
    {
        var document = LoadCatalog();
        return document.Recordings
            .OrderByDescending(item => item.ImportedAtUtc)
            .Select(ToResponse)
            .ToArray();
    }

    public bool TryGet(
        Guid recordingId,
        out RecordingCatalogEntry? entry)
    {
        entry = null;
        if (recordingId == Guid.Empty)
            return false;

        var document = LoadCatalog();
        entry = document.Recordings.FirstOrDefault(
            item => item.RecordingId == recordingId);

        return entry is not null;
    }

    public void RegisterImportedRecording(
        ImportedCsvRecording imported,
        string sourceFilePath)
    {
        ArgumentNullException.ThrowIfNull(imported);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFilePath);

        Directory.CreateDirectory(_archiveDirectory);

        var recording = imported.Recording;
        var archiveFileName = $"{recording.Id:N}.csv";
        var archivePath = Path.Combine(_archiveDirectory, archiveFileName);

        if (!File.Exists(archivePath))
        {
            File.Copy(sourceFilePath, archivePath, overwrite: false);
        }

        var document = LoadCatalog();
        var entries = document.Recordings
            .Where(item => item.RecordingId != recording.Id)
            .ToList();

        entries.Add(new RecordingCatalogEntry(
            recording.Id,
            recording.Name,
            recording.Source.Name,
            recording.Source.ContentHash,
            recording.Source.SizeInBytes,
            imported.Reader.SampleCount,
            archiveFileName.Replace('\\', '/'),
            DateTimeOffset.UtcNow));

        SaveCatalog(new RecordingCatalogDocument(_catalogSchemaVersion, entries));
    }

    public string GetArchiveFilePath(RecordingCatalogEntry entry)
    {
        return Path.Combine(_archiveDirectory, entry.ArchiveFileName);
    }

    private RecordingCatalogItemResponse ToResponse(RecordingCatalogEntry entry) =>
        new(
            entry.RecordingId,
            entry.Name,
            entry.SourceName,
            entry.SourceContentHash,
            entry.SourceSizeInBytes,
            entry.SampleCount,
            entry.ImportedAtUtc);

    private RecordingCatalogDocument LoadCatalog()
    {
        if (!File.Exists(_catalogPath))
            return new RecordingCatalogDocument(_catalogSchemaVersion, []);

        try
        {
            var json = File.ReadAllText(_catalogPath);
            var document = JsonSerializer.Deserialize<RecordingCatalogDocument>(
                json,
                _jsonOptions);

            if (document is null)
                return new RecordingCatalogDocument(_catalogSchemaVersion, []);

            return document with
            {
                Recordings = document.Recordings ?? []
            };
        }
        catch
        {
            return new RecordingCatalogDocument(_catalogSchemaVersion, []);
        }
    }

    private void SaveCatalog(RecordingCatalogDocument document)
    {
        var json = JsonSerializer.Serialize(document, _jsonOptions);
        AtomicJsonFile.Write(_catalogPath, json);
    }

    private sealed record RecordingCatalogDocument(
        int SchemaVersion,
        IReadOnlyList<RecordingCatalogEntry> Recordings);

    public sealed record RecordingCatalogEntry(
        Guid RecordingId,
        string Name,
        string SourceName,
        string SourceContentHash,
        long SourceSizeInBytes,
        long SampleCount,
        string ArchiveFileName,
        DateTimeOffset ImportedAtUtc);
}