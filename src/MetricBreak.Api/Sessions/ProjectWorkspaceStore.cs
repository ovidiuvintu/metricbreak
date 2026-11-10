using System.Collections.Concurrent;
using System.Text.Json;

namespace MetricBreak.Api.Sessions;

public sealed class ProjectWorkspaceStore
{
    private static readonly JsonSerializerOptions _jsonOptions =
        new()
        {
            WriteIndented = true
        };

    private readonly ConcurrentDictionary<Guid, string> _projectRoots = new();

    public bool TryCreateProject(
        string projectName,
        string rootPath,
        out ProjectSummaryResponse? project,
        out string? error)
    {
        project = null;
        error = null;

        if (string.IsNullOrWhiteSpace(projectName))
        {
            error = "Project name is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(rootPath))
        {
            error = "Project root path is required.";
            return false;
        }

        var fullRootPath = Path.GetFullPath(rootPath);
        var manifestPath = GetManifestPath(fullRootPath);

        if (File.Exists(manifestPath))
        {
            error = "A MetricBreak project already exists at this path.";
            return false;
        }

        Directory.CreateDirectory(GetProjectDirectory(fullRootPath));
        Directory.CreateDirectory(GetInvestigationsDirectory(fullRootPath));

        var now = DateTimeOffset.UtcNow;

        var manifest = new ProjectManifest(
            SchemaVersion: 1,
            ProjectId: Guid.NewGuid(),
            ProjectName: projectName,
            CreatedAtUtc: now,
            UpdatedAtUtc: now,
            Investigations: Array.Empty<ProjectInvestigationIndexItem>());

        SaveManifest(fullRootPath, manifest);

        _projectRoots[manifest.ProjectId] = fullRootPath;

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
        out string? error)
    {
        project = null;
        error = null;

        if (string.IsNullOrWhiteSpace(rootPath))
        {
            error = "Project root path is required.";
            return false;
        }

        var fullRootPath = Path.GetFullPath(rootPath);

        if (!TryLoadManifest(fullRootPath, out var manifest)
            || manifest is null)
        {
            error = "No MetricBreak project was found at the provided path.";
            return false;
        }

        _projectRoots[manifest.ProjectId] = fullRootPath;

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
        out string? error)
    {
        filePath = null;
        error = null;

        if (!_projectRoots.TryGetValue(projectId, out var rootPath))
        {
            error = "Project is not open in the current workspace.";
            return false;
        }

        if (!TryLoadManifest(rootPath, out var manifest)
            || manifest is null)
        {
            error = "Project manifest could not be loaded.";
            return false;
        }

        var relativePath = Path.Combine(
            "investigations",
            $"{investigation.InvestigationId:N}.json");

        var fullPath = Path.Combine(
            GetProjectDirectory(rootPath),
            relativePath);

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        var json = JsonSerializer.Serialize(investigation, _jsonOptions);
        File.WriteAllText(fullPath, json);

        var updatedItems = manifest.Investigations
            .Where(item => item.InvestigationId != investigation.InvestigationId)
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
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            Investigations = updatedItems
        };

        SaveManifest(rootPath, updatedManifest);

        filePath = fullPath;

        return true;
    }

    public bool TryLoadInvestigation(
        Guid projectId,
        Guid investigationId,
        out ExportedInvestigation? investigation,
        out string? error)
    {
        investigation = null;
        error = null;

        if (!_projectRoots.TryGetValue(projectId, out var rootPath))
        {
            error = "Project is not open in the current workspace.";
            return false;
        }

        if (!TryLoadManifest(rootPath, out var manifest)
            || manifest is null)
        {
            error = "Project manifest could not be loaded.";
            return false;
        }

        var indexItem = manifest.Investigations
            .FirstOrDefault(item => item.InvestigationId == investigationId);

        if (indexItem is null)
        {
            error = "Investigation was not found in the project.";
            return false;
        }

        var fullPath = Path.Combine(
            GetProjectDirectory(rootPath),
            indexItem.RelativePath.Replace('/', Path.DirectorySeparatorChar));

        if (!File.Exists(fullPath))
        {
            error = "Investigation file is missing from project storage.";
            return false;
        }

        var json = File.ReadAllText(fullPath);

        investigation = JsonSerializer.Deserialize<ExportedInvestigation>(
            json,
            _jsonOptions);

        if (investigation is null)
        {
            error = "Investigation file could not be parsed.";
            return false;
        }

        return true;
    }

    public bool TryListInvestigations(
        Guid projectId,
        out IReadOnlyList<ProjectInvestigationItemResponse>? investigations,
        out string? error)
    {
        investigations = null;
        error = null;

        if (!_projectRoots.TryGetValue(projectId, out var rootPath))
        {
            error = "Project is not open in the current workspace.";
            return false;
        }

        if (!TryLoadManifest(rootPath, out var manifest)
            || manifest is null)
        {
            error = "Project manifest could not be loaded.";
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

    private static bool TryLoadManifest(
        string rootPath,
        out ProjectManifest? manifest)
    {
        manifest = null;

        var manifestPath = GetManifestPath(rootPath);

        if (!File.Exists(manifestPath))
            return false;

        var json = File.ReadAllText(manifestPath);

        manifest = JsonSerializer.Deserialize<ProjectManifest>(
            json,
            _jsonOptions);

        return manifest is not null;
    }

    private static void SaveManifest(
        string rootPath,
        ProjectManifest manifest)
    {
        var manifestPath = GetManifestPath(rootPath);
        var json = JsonSerializer.Serialize(manifest, _jsonOptions);
        File.WriteAllText(manifestPath, json);
    }

    private static string GetProjectDirectory(string rootPath)
    {
        return Path.Combine(rootPath, ".metricbreak");
    }

    private static string GetInvestigationsDirectory(string rootPath)
    {
        return Path.Combine(GetProjectDirectory(rootPath), "investigations");
    }

    private static string GetManifestPath(string rootPath)
    {
        return Path.Combine(GetProjectDirectory(rootPath), "project.mbproj.json");
    }
}
