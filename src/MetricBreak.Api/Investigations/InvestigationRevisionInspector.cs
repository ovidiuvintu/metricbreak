namespace MetricBreak.Api.Investigations;

internal static class InvestigationRevisionInspector
{
    public static IReadOnlyList<string> FindOrphanRevisions(
        string rootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);

        var projectDirectory = Path.Combine(
            Path.GetFullPath(rootPath),
            ".metricbreak");

        var manifestPath = Path.Combine(
            projectDirectory,
            "project.mbproj.json");

        var investigationsDirectory = Path.Combine(
            projectDirectory,
            "investigations");

        if (!File.Exists(manifestPath))
        {
            throw new FileNotFoundException(
                "Project manifest does not exist.",
                manifestPath);
        }

        using var document = System.Text.Json.JsonDocument.Parse(
            File.ReadAllText(manifestPath));

        var referencedPaths = new HashSet<string>(
            OperatingSystem.IsWindows()
                ? StringComparer.OrdinalIgnoreCase
                : StringComparer.Ordinal);

        var investigations = document.RootElement
            .GetProperty("Investigations");

        foreach (var item in investigations.EnumerateArray())
        {
            var relativePath = item
                .GetProperty("RelativePath")
                .GetString();

            if (string.IsNullOrWhiteSpace(relativePath))
            {
                throw new InvalidDataException(
                    "Invalid investigation path in manifest.");
            }

            referencedPaths.Add(
                Path.GetFullPath(
                    Path.Combine(
                        projectDirectory,
                        relativePath.Replace(
                            '/',
                            Path.DirectorySeparatorChar))));
        }

        if (!Directory.Exists(investigationsDirectory))
        {
            return Array.Empty<string>();
        }

        return Directory.EnumerateFiles(
                investigationsDirectory,
                "*.json",
                SearchOption.TopDirectoryOnly)
            .Where(path => IsRevisionFileName(
                Path.GetFileNameWithoutExtension(path)))
            .Select(Path.GetFullPath)
            .Where(path => !referencedPaths.Contains(path))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool IsRevisionFileName(string name)
    {
        if (name.Length != 65 || name[32] != '-')
            return false;

        return Guid.TryParseExact(name[..32], "N", out _)
            && Guid.TryParseExact(name[33..], "N", out _);
    }
}

