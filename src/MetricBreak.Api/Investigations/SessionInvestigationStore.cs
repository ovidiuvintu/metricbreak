using System.Text.Json;

namespace MetricBreak.Api.Investigations;

public sealed class SessionInvestigationStore
{
    private static readonly JsonSerializerOptions _jsonOptions =
        new()
        {
            WriteIndented = true
        };

    private readonly string _storageDirectory;

    public SessionInvestigationStore()
    {
        _storageDirectory = Path.Combine(
            Path.GetTempPath(),
            "metricbreak",
            "investigations");

        Directory.CreateDirectory(_storageDirectory);
    }

    public string Save(ExportedInvestigation investigation)
    {
        ArgumentNullException.ThrowIfNull(investigation);

        var filePath = GetFilePath(investigation.InvestigationId);

        var json = JsonSerializer.Serialize(
            investigation,
            _jsonOptions);

        File.WriteAllText(filePath, json);

        return filePath;
    }

    public bool TryLoad(
        Guid investigationId,
        out ExportedInvestigation? investigation)
    {
        investigation = null;

        if (investigationId == Guid.Empty)
            return false;

        var filePath = GetFilePath(investigationId);

        if (!File.Exists(filePath))
            return false;

        investigation = TryReadFile(filePath);
        return investigation is not null;
    }

    public IReadOnlyList<WorkspaceInvestigationItemResponse> List(
        string? recordingName = null)
    {
        if (!Directory.Exists(_storageDirectory))
            return Array.Empty<WorkspaceInvestigationItemResponse>();

        var filter = recordingName?.Trim();
        var results = new List<WorkspaceInvestigationItemResponse>();

        foreach (var filePath in Directory.EnumerateFiles(
                     _storageDirectory,
                     "*.json",
                     SearchOption.TopDirectoryOnly))
        {
            var investigation = TryReadFile(filePath);
            if (investigation is null)
                continue;

            if (!string.IsNullOrEmpty(filter) &&
                !string.Equals(
                    investigation.Recording.RecordingName,
                    filter,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            results.Add(new WorkspaceInvestigationItemResponse(
                investigation.InvestigationId,
                investigation.Recording.RecordingName,
                Path.GetFileName(filePath),
                investigation.ExportedAtUtc,
                investigation.Recording.SourceContentHash,
                investigation.Recording.SourceSizeInBytes));
        }

        return results
            .OrderByDescending(item => item.ExportedAtUtc)
            .ToArray();
    }

    private ExportedInvestigation? TryReadFile(string filePath)
    {
        try
        {
            var json = File.ReadAllText(filePath);
            var investigation = JsonSerializer.Deserialize<ExportedInvestigation>(
                json,
                _jsonOptions);

            if (investigation is null)
                return null;

            return InvestigationBookmarkMutations.Normalize(investigation);
        }
        catch
        {
            return null;
        }
    }

    private string GetFilePath(Guid investigationId)
    {
        return Path.Combine(
            _storageDirectory,
            $"{investigationId:N}.json");
    }
}
