using System.Collections.Concurrent;
using System.Text.Json;
using MetricBreak.Api.Investigations;
using MetricBreak.Api.Persistence;
namespace MetricBreak.Api.Workspaces;

public sealed class ProjectWorkspaceStore
{
    private const int _manifestSchemaVersion = 2;
    private const int _minimumManifestSchemaVersion = 1;

    private static readonly JsonSerializerOptions _jsonOptions =
        new()
        {
            WriteIndented = true
        };

    private readonly ConcurrentDictionary<Guid, string> _projectRoots = new();

    private readonly string _workspaceStatePath;
    private readonly Action? _beforeManifestCommit;

    public ProjectWorkspaceStore(
        string? workspaceStatePath = null,
        Action? beforeManifestCommit = null)
    {
        _workspaceStatePath = Path.GetFullPath(
            workspaceStatePath ?? Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "MetricBreak",
                "workspace-state.json"));

        _beforeManifestCommit = beforeManifestCommit;
    }

    public bool TryRestoreLastOpenedProject(
        out ProjectSummaryResponse? project)
    {
        project = null;

        if (!File.Exists(_workspaceStatePath))
            return false;

        try
        {
            var json = File.ReadAllText(_workspaceStatePath);
            var state = JsonSerializer.Deserialize<WorkspaceState>(json, _jsonOptions);

            if (state is null || string.IsNullOrWhiteSpace(state.LastProjectRootPath))
                return false;

            return TryOpenProject(
                state.LastProjectRootPath,
                out project,
                out _);
        }
        catch
        {
            return false;
        }
    }

    public IReadOnlyList<ProjectSummaryResponse> GetOpenProjects()
    {
        var projects = new List<ProjectSummaryResponse>();

        foreach (var pair in _projectRoots)
        {
            if (!TryLoadManifest(
                    pair.Value,
                    out var manifest,
                    out _)
                || manifest is null)
            {
                continue;
            }

            projects.Add(new ProjectSummaryResponse(
                manifest.ProjectId,
                manifest.ProjectName,
                pair.Value,
                manifest.Investigations.Count));
        }

        return projects;
    }

    public bool TryCloseProject(
        Guid projectId,
        out ProjectStoreError? error)
    {
        error = null;

        if (projectId == Guid.Empty)
        {
            error = new ProjectStoreError(
                "InvalidProjectId",
                "Project ID cannot be empty.");
            return false;
        }

        if (!_projectRoots.TryRemove(projectId, out _))
        {
            error = new ProjectStoreError(
                "ProjectNotOpen",
                "Project is not open in the current workspace.");
            return false;
        }

        if (_projectRoots.IsEmpty)
        {
            TryClearWorkspaceState();
        }

        return true;
    }

    public bool TryCreateProject(
        string projectName,
        string rootPath,
        out ProjectSummaryResponse? project,
        out ProjectStoreError? error)
    {
        project = null;
        error = null;

        if (string.IsNullOrWhiteSpace(projectName))
        {
            error = new ProjectStoreError(
                "InvalidProjectName",
                "Project name is required.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(rootPath))
        {
            error = new ProjectStoreError(
                "InvalidRootPath",
                "Project root path is required.");
            return false;
        }

        var fullRootPath = Path.GetFullPath(rootPath);
        var manifestPath = GetManifestPath(fullRootPath);

        if (File.Exists(manifestPath))
        {
            error = new ProjectStoreError(
                "ProjectAlreadyExists",
                "A MetricBreak project already exists at this path.");
            return false;
        }

        Directory.CreateDirectory(GetProjectDirectory(fullRootPath));
        Directory.CreateDirectory(GetInvestigationsDirectory(fullRootPath));
        Directory.CreateDirectory(GetRecordingsDirectory(fullRootPath));

        var now = DateTimeOffset.UtcNow;

        var manifest = new ProjectManifest(
            SchemaVersion: _manifestSchemaVersion,
            ProjectId: Guid.NewGuid(),
            ProjectName: projectName,
            CreatedAtUtc: now,
            UpdatedAtUtc: now,
            Investigations: Array.Empty<ProjectInvestigationIndexItem>(),
            Recordings: Array.Empty<ProjectRecordingIndexItem>());

        SaveManifest(fullRootPath, manifest);

        _projectRoots[manifest.ProjectId] = fullRootPath;
        PersistLastOpenedProject(fullRootPath);

        project = new ProjectSummaryResponse(
            manifest.ProjectId,
            manifest.ProjectName,
            fullRootPath,
            manifest.Investigations.Count);

        return true;
    }

    public bool TryOpenProject(
        string rootPath,
        out ProjectSummaryResponse? project,
        out ProjectStoreError? error)
    {
        project = null;
        error = null;

        if (string.IsNullOrWhiteSpace(rootPath))
        {
            error = new ProjectStoreError(
                "InvalidRootPath",
                "Project root path is required.");
            return false;
        }

        var fullRootPath = Path.GetFullPath(rootPath);

        if (!TryLoadManifest(
                fullRootPath,
                out var manifest,
                out error)
            || manifest is null)
        {
            error ??= new ProjectStoreError(
                "ProjectNotFound",
                "No MetricBreak project was found at the provided path.");

            return false;
        }

        _projectRoots[manifest.ProjectId] = fullRootPath;
        PersistLastOpenedProject(fullRootPath);

        project = new ProjectSummaryResponse(
            manifest.ProjectId,
            manifest.ProjectName,
            fullRootPath,
            manifest.Investigations.Count);

        return true;
    }


    public bool TrySaveInvestigation(
        Guid projectId,
        ExportedInvestigation investigation,
        out string? filePath,
        out ProjectStoreError? error)
    {
        filePath = null;
        error = null;

        ArgumentNullException.ThrowIfNull(investigation);

        if (!_projectRoots.TryGetValue(projectId, out var rootPath))
        {
            error = new ProjectStoreError(
                "ProjectNotOpen",
                "Project is not open in the current workspace.");
            return false;
        }

        if (!TryLoadManifest(
                rootPath,
                out var manifest,
                out error)
            || manifest is null)
        {
            error ??= new ProjectStoreError(
                "ManifestLoadFailed",
                "Project manifest could not be loaded.");
            return false;
        }

        // Each save creates a new immutable revision.
        var relativePath = CreateInvestigationRevisionPath(
            investigation.InvestigationId);

        var fullPath = Path.Combine(
            GetProjectDirectory(rootPath),
            relativePath);

        var json = JsonSerializer.Serialize(
            investigation,
            _jsonOptions);

        // Step 1: Publish the new investigation revision.
        // No existing revision is overwritten.
        try
        {
            AtomicJsonFile.Write(fullPath, json);
        }
        catch (IOException)
        {
            error = new ProjectStoreError(
                "InvestigationWriteFailed",
                "Investigation revision could not be written.");
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            error = new ProjectStoreError(
                "InvestigationWriteFailed",
                "Access to investigation storage was denied.");
            return false;
        }

        // Step 2: Prepare the updated manifest.
        var updatedItems = manifest.Investigations
            .Where(item =>
                item.InvestigationId != investigation.InvestigationId)
            .ToList();

        updatedItems.Add(new ProjectInvestigationIndexItem(
            InvestigationId: investigation.InvestigationId,
            RelativePath: relativePath.Replace('\\', '/'),
            RecordingName: investigation.Recording.RecordingName,
            SourceContentHash: investigation.Recording.SourceContentHash,
            SourceSizeInBytes: investigation.Recording.SourceSizeInBytes,
            ExportedAtUtc: investigation.ExportedAtUtc));

        var updatedManifest = manifest with
        {
            SchemaVersion = _manifestSchemaVersion,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            Investigations = updatedItems,
        };

        // Step 3: Commit by publishing the manifest.
        try
        {
            // Allows tests to simulate a failure before
            // the manifest is committed.
            _beforeManifestCommit?.Invoke();

            SaveManifest(rootPath, updatedManifest);
        }
        catch (IOException)
        {
            error = new ProjectStoreError(
                "ManifestWriteFailed",
                "Investigation revision was written, but the " +
                "project manifest could not be updated.");

            return false;
        }
        catch (UnauthorizedAccessException)
        {
            error = new ProjectStoreError(
                "ManifestWriteFailed",
                "Access to project manifest storage was denied.");

            return false;
        }

        // Only report success after manifest publication.
        filePath = fullPath;
        return true;
    }


    public bool TryLoadInvestigation(
        Guid projectId,
        Guid investigationId,
        out ExportedInvestigation? investigation,
        out ProjectStoreError? error)
    {
        investigation = null;
        error = null;

        if (!_projectRoots.TryGetValue(projectId, out var rootPath))
        {
            error = new ProjectStoreError(
                "ProjectNotOpen",
                "Project is not open in the current workspace.");
            return false;
        }

        if (!TryLoadManifest(
                rootPath,
                out var manifest,
                out error)
            || manifest is null)
        {
            error ??= new ProjectStoreError(
                "ManifestLoadFailed",
                "Project manifest could not be loaded.");
            return false;
        }

        var indexItem = manifest.Investigations
            .FirstOrDefault(item => item.InvestigationId == investigationId);

        if (indexItem is null)
        {
            error = new ProjectStoreError(
                "InvestigationNotFound",
                "Investigation was not found in the project.");
            return false;
        }

        var fullPath = Path.Combine(
            GetProjectDirectory(rootPath),
            indexItem.RelativePath.Replace('/', Path.DirectorySeparatorChar));

        if (!File.Exists(fullPath))
        {
            error = new ProjectStoreError(
                "InvestigationFileMissing",
                "Investigation file is missing from project storage.");
            return false;
        }

        try
        {
            var json = File.ReadAllText(fullPath);

            investigation = JsonSerializer.Deserialize<ExportedInvestigation>(
                json,
                _jsonOptions);

            if (investigation is null)
            {
                error = new ProjectStoreError(
                    "InvestigationParseFailed",
                    "Investigation file could not be parsed.");
                return false;
            }

            investigation = InvestigationDocumentNormalizer.Normalize(investigation);
            return true;
        }
        catch (JsonException)
        {
            error = new ProjectStoreError(
                "InvestigationParseFailed",
                "Investigation file contains invalid JSON.");
            return false;
        }
    }

    public bool TryEnsureRecordingArchive(
        Guid projectId,
        Guid recordingId,
        string sourceContentHash,
        long sourceSizeInBytes,
        string sourceName,
        string workspaceArchivePath,
        out ProjectStoreError? error)
    {
        error = null;

        if (recordingId == Guid.Empty)
        {
            error = new ProjectStoreError(
                "InvalidRecordingId",
                "Recording ID cannot be empty.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(workspaceArchivePath) || !File.Exists(workspaceArchivePath))
        {
            error = new ProjectStoreError(
                "RecordingArchiveMissing",
                "Workspace recording archive was not found.");
            return false;
        }

        if (!_projectRoots.TryGetValue(projectId, out var rootPath))
        {
            error = new ProjectStoreError(
                "ProjectNotOpen",
                "Project is not open in the current workspace.");
            return false;
        }

        if (!TryLoadManifest(
                rootPath,
                out var manifest,
                out error)
            || manifest is null)
        {
            error ??= new ProjectStoreError(
                "ManifestLoadFailed",
                "Project manifest could not be loaded.");
            return false;
        }

        var relativePath = CreateRecordingRelativePath(recordingId);
        var destinationPath = Path.Combine(
            GetProjectDirectory(rootPath),
            relativePath.Replace('/', Path.DirectorySeparatorChar));

        var existing = manifest.Recordings
            .FirstOrDefault(item => item.RecordingId == recordingId);

        if (existing is not null
            && File.Exists(destinationPath)
            && string.Equals(
                existing.SourceContentHash,
                sourceContentHash,
                StringComparison.OrdinalIgnoreCase)
            && existing.SourceSizeInBytes == sourceSizeInBytes)
        {
            return true;
        }

        Directory.CreateDirectory(GetRecordingsDirectory(rootPath));

        try
        {
            File.Copy(workspaceArchivePath, destinationPath, overwrite: true);
        }
        catch (IOException)
        {
            error = new ProjectStoreError(
                "RecordingArchiveFailed",
                "Recording could not be copied into the project.");
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            error = new ProjectStoreError(
                "RecordingArchiveFailed",
                "Access to project recording storage was denied.");
            return false;
        }

        var updatedRecordings = manifest.Recordings
            .Where(item => item.RecordingId != recordingId)
            .ToList();

        updatedRecordings.Add(new ProjectRecordingIndexItem(
            RecordingId: recordingId,
            RelativePath: relativePath,
            SourceName: string.IsNullOrWhiteSpace(sourceName)
                ? Path.GetFileName(workspaceArchivePath)
                : sourceName,
            SourceContentHash: sourceContentHash,
            SourceSizeInBytes: sourceSizeInBytes,
            ArchivedAtUtc: DateTimeOffset.UtcNow));

        var updatedManifest = manifest with
        {
            SchemaVersion = _manifestSchemaVersion,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            Recordings = updatedRecordings,
        };

        try
        {
            SaveManifest(rootPath, updatedManifest);
        }
        catch (IOException)
        {
            error = new ProjectStoreError(
                "ManifestWriteFailed",
                "Recording was archived, but the project manifest could not be updated.");
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            error = new ProjectStoreError(
                "ManifestWriteFailed",
                "Access to project manifest storage was denied.");
            return false;
        }

        return true;
    }

    public bool TryGetRecordingArchivePath(
        Guid projectId,
        ExportedInvestigation investigation,
        out string? archivePath,
        out ProjectStoreError? error)
    {
        archivePath = null;
        error = null;

        if (!_projectRoots.TryGetValue(projectId, out var rootPath))
        {
            error = new ProjectStoreError(
                "ProjectNotOpen",
                "Project is not open in the current workspace.");
            return false;
        }

        if (!TryLoadManifest(
                rootPath,
                out var manifest,
                out error)
            || manifest is null)
        {
            error ??= new ProjectStoreError(
                "ManifestLoadFailed",
                "Project manifest could not be loaded.");
            return false;
        }

        var recording = investigation.Recording;
        var indexItem = manifest.Recordings.FirstOrDefault(item =>
                item.RecordingId == recording.RecordingId
                && recording.RecordingId != Guid.Empty)
            ?? manifest.Recordings.FirstOrDefault(item =>
                string.Equals(
                    item.SourceContentHash,
                    recording.SourceContentHash,
                    StringComparison.OrdinalIgnoreCase)
                && item.SourceSizeInBytes == recording.SourceSizeInBytes);

        if (indexItem is null)
        {
            error = new ProjectStoreError(
                "ProjectRecordingNotFound",
                "Recording is not archived in this project.");
            return false;
        }

        var fullPath = Path.Combine(
            GetProjectDirectory(rootPath),
            indexItem.RelativePath.Replace('/', Path.DirectorySeparatorChar));

        if (!File.Exists(fullPath))
        {
            error = new ProjectStoreError(
                "ProjectRecordingMissing",
                "Project recording file is missing.");
            return false;
        }

        archivePath = fullPath;
        return true;
    }

    public bool TryListInvestigations(
        Guid projectId,
        out IReadOnlyList<ProjectInvestigationItemResponse>? investigations,
        out ProjectStoreError? error)
    {
        investigations = null;
        error = null;

        if (!_projectRoots.TryGetValue(projectId, out var rootPath))
        {
            error = new ProjectStoreError(
                "ProjectNotOpen",
                "Project is not open in the current workspace.");
            return false;
        }

        if (!TryLoadManifest(
                rootPath,
                out var manifest,
                out error)
            || manifest is null)
        {
            error ??= new ProjectStoreError(
                "ManifestLoadFailed",
                "Project manifest could not be loaded.");
            return false;
        }

        investigations = manifest.Investigations
            .Select(item => new ProjectInvestigationItemResponse(
                item.InvestigationId,
                item.RecordingName,
                item.RelativePath,
                item.ExportedAtUtc,
                item.SourceContentHash,
                item.SourceSizeInBytes))
            .ToArray();

        return true;
    }

    public bool TryDeleteInvestigation(
        Guid projectId,
        Guid investigationId,
        out ProjectStoreError? error)
    {
        error = null;

        if (!_projectRoots.TryGetValue(projectId, out var rootPath))
        {
            error = new ProjectStoreError(
                "ProjectNotOpen",
                "Project is not open in the current workspace.");
            return false;
        }

        if (!TryLoadManifest(
                rootPath,
                out var manifest,
                out error)
            || manifest is null)
        {
            error ??= new ProjectStoreError(
                "ManifestLoadFailed",
                "Project manifest could not be loaded.");
            return false;
        }

        var indexItem = manifest.Investigations
            .FirstOrDefault(item => item.InvestigationId == investigationId);

        if (indexItem is null)
        {
            error = new ProjectStoreError(
                "InvestigationNotFound",
                "Investigation was not found in the project.");
            return false;
        }

        var fullPath = Path.Combine(
            GetProjectDirectory(rootPath),
            indexItem.RelativePath.Replace('/', Path.DirectorySeparatorChar));

        if (File.Exists(fullPath))
        {
            try
            {
                File.Delete(fullPath);
            }
            catch (IOException)
            {
                error = new ProjectStoreError(
                    "InvestigationDeleteFailed",
                    "Investigation file could not be deleted.");
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                error = new ProjectStoreError(
                    "InvestigationDeleteFailed",
                    "Access to investigation file was denied.");
                return false;
            }
        }

        var updatedItems = manifest.Investigations
            .Where(item => item.InvestigationId != investigationId)
            .ToArray();

        var updatedManifest = manifest with
        {
            SchemaVersion = _manifestSchemaVersion,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            Investigations = updatedItems,
        };

        try
        {
            SaveManifest(rootPath, updatedManifest);
        }
        catch (IOException)
        {
            error = new ProjectStoreError(
                "ManifestWriteFailed",
                "Investigation was removed, but the project manifest could not be updated.");
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            error = new ProjectStoreError(
                "ManifestWriteFailed",
                "Access to project manifest storage was denied.");
            return false;
        }

        return true;
    }

    private bool TryLoadManifest(
        string rootPath,
        out ProjectManifest? manifest,
        out ProjectStoreError? error)
    {
        manifest = null;
        error = null;

        var manifestPath = GetManifestPath(rootPath);

        if (!File.Exists(manifestPath))
        {
            error = new ProjectStoreError(
                "ProjectNotFound",
                "No MetricBreak project was found at the provided path.");
            return false;
        }

        var json = File.ReadAllText(manifestPath);

        try
        {
            var storage = JsonSerializer.Deserialize<ProjectManifestStorage>(
                json,
                _jsonOptions);

            if (storage is null)
            {
                error = new ProjectStoreError(
                    "ManifestCorrupt",
                    "Project manifest could not be parsed.");
                return false;
            }

            manifest = new ProjectManifest(
                storage.SchemaVersion,
                storage.ProjectId,
                storage.ProjectName,
                storage.CreatedAtUtc,
                storage.UpdatedAtUtc,
                storage.Investigations ?? [],
                storage.Recordings ?? []);
        }
        catch (JsonException)
        {
            error = new ProjectStoreError(
                "ManifestCorrupt",
                "Project manifest contains invalid JSON.");
            return false;
        }

        if (manifest is null)
        {
            error = new ProjectStoreError(
                "ManifestCorrupt",
                "Project manifest could not be parsed.");
            return false;
        }

        if (manifest.SchemaVersion < _minimumManifestSchemaVersion
            || manifest.SchemaVersion > _manifestSchemaVersion)
        {
            error = new ProjectStoreError(
                "UnsupportedManifestSchema",
                $"Project manifest schema version '{manifest.SchemaVersion}' is not supported.");
            return false;
        }

        return true;
    }

    private static void SaveManifest(
        string rootPath,
        ProjectManifest manifest)
    {
        var manifestPath = GetManifestPath(rootPath);
        var json = JsonSerializer.Serialize(manifest, _jsonOptions);
        AtomicJsonFile.Write(manifestPath, json);
    }

    private static string GetProjectDirectory(string rootPath)
    {
        return Path.Combine(rootPath, ".metricbreak");
    }

    private static string GetInvestigationsDirectory(string rootPath)
    {
        return Path.Combine(GetProjectDirectory(rootPath), "investigations");
    }

    private static string GetRecordingsDirectory(string rootPath)
    {
        return Path.Combine(GetProjectDirectory(rootPath), "recordings");
    }

    private static string CreateRecordingRelativePath(Guid recordingId)
    {
        return Path.Combine("recordings", $"{recordingId:N}.csv")
            .Replace('\\', '/');
    }

    private static string GetManifestPath(string rootPath)
    {
        return Path.Combine(GetProjectDirectory(rootPath), "project.mbproj.json");
    }

    private void PersistLastOpenedProject(string rootPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_workspaceStatePath)!);

        var state = new WorkspaceState(rootPath);
        var json = JsonSerializer.Serialize(state, _jsonOptions);

        AtomicJsonFile.Write(_workspaceStatePath, json);
    }

    private void TryClearWorkspaceState()
    {
        if (File.Exists(_workspaceStatePath))
        {
            File.Delete(_workspaceStatePath);
        }
    }


    private static string CreateInvestigationRevisionPath(
        Guid investigationId)
    {
        var revisionId = Guid.NewGuid();

        return Path.Combine(
            "investigations",
            $"{investigationId:N}-{revisionId:N}.json");
    }


    private sealed record ProjectManifestStorage(
        int SchemaVersion,
        Guid ProjectId,
        string ProjectName,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset UpdatedAtUtc,
        IReadOnlyList<ProjectInvestigationIndexItem>? Investigations,
        IReadOnlyList<ProjectRecordingIndexItem>? Recordings);

    private sealed record WorkspaceState(string LastProjectRootPath);
}
