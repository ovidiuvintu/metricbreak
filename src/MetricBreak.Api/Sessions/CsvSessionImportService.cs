
using MetricBreak.Ingestion.Csv;

namespace MetricBreak.Api.Sessions;

public sealed class CsvSessionImportService
{
    private readonly ReplaySessionManager _manager;
    private readonly CsvRecordingImporter _importer = new();

    public CsvSessionImportService(
        ReplaySessionManager manager)
    {
        ArgumentNullException.ThrowIfNull(manager);
        _manager = manager;
    }

    public async Task<CreateSessionResponse> ImportAsync(
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);

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

        // Keep the uploaded filename separate from the
        // server-generated temporary path.
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
                await file.CopyToAsync(
                    stream,
                    cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();

            var imported = _importer.Import(temporaryPath);

            var sessionId = _manager.Create(imported);

            return new CreateSessionResponse(
                sessionId,
                imported.Recording.Id,
                imported.Recording.Name,
                imported.Reader.SampleCount,
                "Ready");
        }
        finally
        {
            if (Directory.Exists(temporaryDirectory))
            {
                Directory.Delete(
                    temporaryDirectory,
                    recursive: true);
            }
        }
    }
}

