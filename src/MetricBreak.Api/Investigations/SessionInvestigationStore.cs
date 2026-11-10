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

        var json = File.ReadAllText(filePath);

        investigation = JsonSerializer.Deserialize<ExportedInvestigation>(
            json,
            _jsonOptions);

        return investigation is not null;
    }

    private string GetFilePath(Guid investigationId)
    {
        return Path.Combine(
            _storageDirectory,
            $"{investigationId:N}.json");
    }
}
