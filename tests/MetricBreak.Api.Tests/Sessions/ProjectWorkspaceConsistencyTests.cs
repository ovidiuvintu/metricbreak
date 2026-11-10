
using System.Text.Json;
using MetricBreak.Api.Sessions;

namespace MetricBreak.Api.Tests.Sessions;

public sealed class ProjectWorkspaceConsistencyTests
{
    [Fact]
    public void ManifestCommitFailure_PreservesPreviousInvestigation()
    {
        var rootPath = Path.Combine(
            Path.GetTempPath(),
            "metricbreak-consistency-tests",
            Guid.NewGuid().ToString("N"));

        var workspaceStatePath = Path.Combine(
            rootPath,
            "workspace-state.json");

        Directory.CreateDirectory(rootPath);

        try
        {
            var failNextCommit = false;

            var store = new ProjectWorkspaceStore(
                workspaceStatePath,
                beforeManifestCommit: () =>
                {
                    if (!failNextCommit)
                        return;

                    failNextCommit = false;

                    throw new IOException(
                        "Simulated manifest commit failure.");
                });

            // Create the project.
            Assert.True(
                store.TryCreateProject(
                    "Consistency Test",
                    rootPath,
                    out var project,
                    out var createError),
                createError?.Message);

            Assert.NotNull(project);

            var projectId = project.ProjectId;
            var investigationId = Guid.NewGuid();

            // Save and commit the initial investigation.
            var original = CreateInvestigation(
                investigationId,
                "original-recording.csv");

            Assert.True(
                store.TrySaveInvestigation(
                    projectId,
                    original,
                    out var originalPath,
                    out var firstSaveError),
                firstSaveError?.Message);

            Assert.NotNull(originalPath);
            Assert.True(File.Exists(originalPath));

            var manifestPath = Path.Combine(
                rootPath,
                ".metricbreak",
                "project.mbproj.json");

            var manifestBeforeFailure =
                File.ReadAllText(manifestPath);

            // Prepare a different revision of the same investigation.
            var replacement = CreateInvestigation(
                investigationId,
                "replacement-recording.csv");

            // Force the next manifest publication to fail.
            failNextCommit = true;

            var saved = store.TrySaveInvestigation(
                projectId,
                replacement,
                out var failedSavePath,
                out var saveError);

            Assert.False(saved);
            Assert.Null(failedSavePath);
            Assert.NotNull(saveError);

            Assert.Equal(
                "ManifestWriteFailed",
                saveError.Code);

            // The committed manifest must remain unchanged.
            var manifestAfterFailure =
                File.ReadAllText(manifestPath);

            Assert.Equal(
                manifestBeforeFailure,
                manifestAfterFailure);

            // The original investigation must remain readable.
            Assert.True(
                store.TryLoadInvestigation(
                    projectId,
                    investigationId,
                    out var loaded,
                    out var loadError),
                loadError?.Message);

            Assert.NotNull(loaded);

            Assert.Equal(
                "original-recording.csv",
                loaded.Recording.RecordingName);

            // The manifest must still reference the original revision.
            using var manifestDocument =
                JsonDocument.Parse(manifestAfterFailure);

            var investigations = manifestDocument.RootElement
                .GetProperty("Investigations")
                .EnumerateArray()
                .ToArray();

            var indexedInvestigation =
                Assert.Single(investigations);

            var relativePath = indexedInvestigation
                .GetProperty("RelativePath")
                .GetString();

            Assert.NotNull(relativePath);

            var committedPath = Path.GetFullPath(
                Path.Combine(
                    rootPath,
                    ".metricbreak",
                    relativePath.Replace(
                        '/',
                        Path.DirectorySeparatorChar)));

            Assert.Equal(
                Path.GetFullPath(originalPath),
                committedPath);

            // The failed revision must not become visible
            // through the investigation index.
            Assert.True(
                store.TryListInvestigations(
                    projectId,
                    out var listed,
                    out var listError),
                listError?.Message);

            Assert.NotNull(listed);

            var indexedItem = Assert.Single(listed);

            Assert.Equal(
                "original-recording.csv",
                indexedItem.RecordingName);
        }
        finally
        {
            if (Directory.Exists(rootPath))
            {
                Directory.Delete(
                    rootPath,
                    recursive: true);
            }
        }
    }

    private static ExportedInvestigation CreateInvestigation(
        Guid investigationId,
        string recordingName)
    {
        return new ExportedInvestigation(
            SchemaVersion: 1,
            InvestigationId: investigationId,
            ExportedAtUtc: DateTimeOffset.UtcNow,
            Recording: new RecordingExportSnapshot(
                SourceName: recordingName,
                SourceContentHash: "test-content-hash",
                SourceSizeInBytes: 1024,
                RecordingName: recordingName,
                SampleCount: 100),
            Session: new SessionExportSnapshot(
                SequenceNumber: 1,
                ExecutionState: "Paused",
                PauseReason: "Test"),
            Breakpoints: Array.Empty<BreakpointExportSnapshot>());
    }
}
