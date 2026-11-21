using System.Text;
using MetricBreak.Api.Recordings;
using MetricBreak.Ingestion.Csv;

namespace MetricBreak.Api.Sessions;

public sealed class CsvSessionImportService
{
    private readonly ReplaySessionManager _manager;
    private readonly RecordingCatalogStore _catalog;
    private readonly CsvRecordingImporter _importer = new();

    public CsvSessionImportService(
        ReplaySessionManager manager,
        RecordingCatalogStore catalog)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(catalog);
        _manager = manager;
        _catalog = catalog;
    }

    public Task<CreateSessionResponse> ImportAsync(
        IFormFile file,
        CancellationToken cancellationToken = default) =>
        ImportAsync(file, mapping: null, cancellationToken);

    public async Task<CreateSessionResponse> ImportAsync(
        IFormFile file,
        TimestampMapping? mapping,
        CancellationToken cancellationToken = default)
    {
        return await UseTemporaryFileAsync(
            file,
            path =>
            {
                var imported = _importer.Import(path, mapping);
                _catalog.RegisterImportedRecording(imported, path);
                var sessionId = _manager.Create(imported);

                return Task.FromResult(new CreateSessionResponse(
                    sessionId,
                    imported.Recording.Id,
                    imported.Recording.Name,
                    imported.Reader.SampleCount,
                    "Ready"));
            },
            cancellationToken);
    }

    public Task<TimestampPreviewResponse> PreviewAsync(
        IFormFile file,
        TimestampMapping? mapping,
        CancellationToken cancellationToken = default)
    {
        return UseTemporaryFileAsync(file, path =>
        {
            using var reader = OpenReader(path);
            var inspection = CsvTimestampInspector.Inspect(reader, mapping);
            var detection = inspection.Detection;
            var applied = inspection.Applied;

            return Task.FromResult(new TimestampPreviewResponse(
                detection.Columns
                    .Select(column => new TimestampColumnResponse(
                        column.Index,
                        column.Name))
                    .ToArray(),
                detection.ColumnIndex,
                detection.ColumnName,
                detection.Format.ToString(),
                detection.Ambiguous,
                applied.ColumnIndex,
                applied.Format.ToString(),
                applied.Origin.ToString(),
                inspection.Preview
                    .Select(row => new TimestampPreviewRowResponse(
                        row.SampleIndex,
                        row.Raw,
                        row.Normalized))
                    .ToArray(),
                detection.Note));
        }, cancellationToken);
    }

    public Task<ImportedCsvRecording> ImportRecordingAsync(
        IFormFile file,
        CancellationToken cancellationToken = default) =>
        ImportRecordingAsync(file, mapping: null, cancellationToken);

    public Task<ImportedCsvRecording> ImportRecordingAsync(
        IFormFile file,
        TimestampMapping? mapping,
        CancellationToken cancellationToken = default)
    {
        return UseTemporaryFileAsync(
            file,
            path => Task.FromResult(_importer.Import(path, mapping)),
            cancellationToken);
    }

    private static async Task<T> UseTemporaryFileAsync<T>(
        IFormFile file,
        Func<string, Task<T>> useFile,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(useFile);

        if (file.Length == 0)
        {
            throw new ArgumentException(
                "The uploaded CSV file is empty.",
                nameof(file));
        }

        var temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            "metricbreak",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(temporaryDirectory);

        var originalName = Path.GetFileName(file.FileName);
        var temporaryPath = Path.Combine(
            temporaryDirectory,
            string.IsNullOrWhiteSpace(originalName)
                ? "recording.csv"
                : originalName);

        try
        {
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None))
            {
                await file.CopyToAsync(stream, cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            return await useFile(temporaryPath);
        }
        finally
        {
            if (Directory.Exists(temporaryDirectory))
            {
                Directory.Delete(temporaryDirectory, recursive: true);
            }
        }
    }

    private static StreamReader OpenReader(string path) =>
        new(
            path,
            new UTF8Encoding(
                encoderShouldEmitUTF8Identifier: false,
                throwOnInvalidBytes: true),
            detectEncodingFromByteOrderMarks: true);
}
