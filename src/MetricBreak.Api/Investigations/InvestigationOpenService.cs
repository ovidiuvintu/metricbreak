using MetricBreak.Api.Recordings;
using MetricBreak.Api.Sessions;
using MetricBreak.Api.Workspaces;
using MetricBreak.Ingestion.Csv;

namespace MetricBreak.Api.Investigations;

public sealed class InvestigationOpenService
{
    private readonly RecordingCatalogStore _catalog;
    private readonly ProjectWorkspaceStore _projectStore;
    private readonly CsvRecordingImporter _importer;
    private readonly ReplaySessionManager _sessions;

    public InvestigationOpenService(
        RecordingCatalogStore catalog,
        ProjectWorkspaceStore projectStore,
        CsvRecordingImporter importer,
        ReplaySessionManager sessions)
    {
        _catalog = catalog;
        _projectStore = projectStore;
        _importer = importer;
        _sessions = sessions;
    }

    public bool TryOpen(
        ExportedInvestigation investigation,
        Guid? projectId,
        out OpenInvestigationResponse? response,
        out string? error)
    {
        response = null;
        error = null;

        investigation = InvestigationDocumentNormalizer.Normalize(investigation);

        string archivePath;
        Guid importRecordingId;

        if (projectId is Guid selectedProjectId
            && _projectStore.TryGetRecordingArchivePath(
                selectedProjectId,
                investigation,
                out var projectArchivePath,
                out _))
        {
            archivePath = projectArchivePath!;
            importRecordingId = investigation.Recording.RecordingId;
        }
        else if (TryResolveCatalogEntry(investigation, out var catalogEntry)
            && catalogEntry is not null)
        {
            archivePath = _catalog.GetArchiveFilePath(catalogEntry);
            importRecordingId = catalogEntry.RecordingId;
        }
        else
        {
            error =
                "Recording is not in the local catalog. Import the CSV once before opening this investigation.";
            return false;
        }

        if (!File.Exists(archivePath))
        {
            error = "Recording archive file is missing.";
            return false;
        }

        ImportedCsvRecording imported;
        try
        {
            imported = _importer.Import(
                archivePath,
                mapping: null,
                recordingId: importRecordingId != Guid.Empty
                    ? importRecordingId
                    : null);
        }
        catch (Exception exception) when (
            exception is ArgumentException or FormatException or InvalidDataException)
        {
            error = exception.Message;
            return false;
        }

        if (!string.Equals(
                imported.Recording.Source.ContentHash,
                investigation.Recording.SourceContentHash,
                StringComparison.OrdinalIgnoreCase)
            || imported.Recording.Source.SizeInBytes !=
                investigation.Recording.SourceSizeInBytes)
        {
            error = "Catalog recording does not match the saved investigation.";
            return false;
        }

        if (!_sessions.TryImportInvestigation(
                imported,
                investigation,
                out var sessionId,
                out var importError))
        {
            error = importError ?? "Unable to restore investigation session.";
            return false;
        }

        if (!_sessions.TryGetSnapshot(sessionId, out var snapshot)
            || snapshot is null)
        {
            error = "Session was created but could not be read.";
            return false;
        }

        response = new OpenInvestigationResponse(
            sessionId,
            investigation.InvestigationId,
            snapshot.RecordingId,
            snapshot.RecordingName,
            investigation.Metadata?.Name,
            snapshot.SampleCount,
            snapshot.Position.SequenceNumber,
            snapshot.ExecutionState.Status.ToString());

        return true;
    }

    private bool TryResolveCatalogEntry(
        ExportedInvestigation investigation,
        out RecordingCatalogStore.RecordingCatalogEntry? entry)
    {
        if (_catalog.TryGet(investigation.Recording.RecordingId, out entry))
            return true;

        return _catalog.TryFindBySource(
            investigation.Recording.SourceContentHash,
            investigation.Recording.SourceSizeInBytes,
            out entry);
    }
}