
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


    [Fact]
    public void ManifestCommitFailure_SuccessfulRetry_PublishesNewRevision()
    {
        var rootPath = Path.Combine(
            Path.GetTempPath(),
            "metricbreak-consistency-tests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(rootPath);

        var workspaceStatePath = Path.Combine(
            rootPath,
            "workspace-state.json");

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

            // Create project.
            Assert.True(
                store.TryCreateProject(
                    "Retry Test",
                    rootPath,
                    out var project,
                    out var createError),
                createError?.Message);

            Assert.NotNull(project);

            var projectId = project.ProjectId;
            var investigationId = Guid.NewGuid();

            // Save the original revision.
            var original = CreateInvestigation(
                investigationId,
                "original.csv");

            Assert.True(
                store.TrySaveInvestigation(
                    projectId,
                    original,
                    out var originalPath,
                    out var originalError),
                originalError?.Message);

            Assert.NotNull(originalPath);
            Assert.True(File.Exists(originalPath));

            // Prepare replacement revision.
            var replacement = CreateInvestigation(
                investigationId,
                "replacement.csv");

            // Simulate manifest commit failure.
            failNextCommit = true;

            var failed = store.TrySaveInvestigation(
                projectId,
                replacement,
                out var failedPath,
                out var failedError);

            Assert.False(failed);
            Assert.Null(failedPath);
            Assert.NotNull(failedError);

            Assert.Equal(
                "ManifestWriteFailed",
                failedError.Code);

            // Original revision must remain authoritative.
            Assert.True(
                store.TryLoadInvestigation(
                    projectId,
                    investigationId,
                    out var beforeRetry,
                    out var beforeRetryError),
                beforeRetryError?.Message);

            Assert.NotNull(beforeRetry);

            Assert.Equal(
                "original.csv",
                beforeRetry.Recording.RecordingName);

            // Retry without injected failure.
            Assert.True(
                store.TrySaveInvestigation(
                    projectId,
                    replacement,
                    out var retryPath,
                    out var retryError),
                retryError?.Message);

            Assert.NotNull(retryPath);
            Assert.True(File.Exists(retryPath));

            // Retry creates a different immutable revision.
            Assert.NotEqual(originalPath, retryPath);

            // The new revision must now be authoritative.
            Assert.True(
                store.TryLoadInvestigation(
                    projectId,
                    investigationId,
                    out var afterRetry,
                    out var afterRetryError),
                afterRetryError?.Message);

            Assert.NotNull(afterRetry);

            Assert.Equal(
                "replacement.csv",
                afterRetry.Recording.RecordingName);

            // Only the latest revision appears in the index.
            Assert.True(
                store.TryListInvestigations(
                    projectId,
                    out var investigations,
                    out var listError),
                listError?.Message);

            Assert.NotNull(investigations);

            var indexed = Assert.Single(investigations);

            Assert.Equal(investigationId, indexed.InvestigationId);
            Assert.Equal("replacement.csv", indexed.RecordingName);

            // Previous revision remains on disk until cleanup.
            Assert.True(File.Exists(originalPath));
        }
        finally
        {
            if (Directory.Exists(rootPath))
            {
                Directory.Delete(rootPath, recursive: true);
            }
        }
    }



    [Fact]
    public void FindOrphanRevisions_IdentifiesUncommittedAndSupersededFiles()
    {
        var rootPath = Path.Combine(
            Path.GetTempPath(),
            "metricbreak-consistency-tests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(rootPath);

        var workspaceStatePath = Path.Combine(
            rootPath,
            "workspace-state.json");

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

            Assert.True(
                store.TryCreateProject(
                    "Orphan Detection Test",
                    rootPath,
                    out var project,
                    out var createError),
                createError?.Message);

            Assert.NotNull(project);

            var investigationId = Guid.NewGuid();

            Assert.True(
                store.TrySaveInvestigation(
                    project.ProjectId,
                    CreateInvestigation(investigationId, "first.csv"),
                    out var firstPath,
                    out var firstError),
                firstError?.Message);

            Assert.NotNull(firstPath);

            // This save writes a revision but fails to commit it.
            failNextCommit = true;

            Assert.False(
                store.TrySaveInvestigation(
                    project.ProjectId,
                    CreateInvestigation(investigationId, "failed.csv"),
                    out _,
                    out var failedError));

            Assert.NotNull(failedError);
            Assert.Equal("ManifestWriteFailed", failedError.Code);

            // Commit a replacement successfully.
            Assert.True(
                store.TrySaveInvestigation(
                    project.ProjectId,
                    CreateInvestigation(investigationId, "latest.csv"),
                    out var latestPath,
                    out var latestError),
                latestError?.Message);

            Assert.NotNull(latestPath);

            var orphans =
                InvestigationRevisionInspector.FindOrphanRevisions(
                    rootPath);

            // The superseded first revision is unreferenced.
            Assert.Contains(firstPath, orphans);

            // The failed revision is also unreferenced.
            // Exactly two orphan revisions should exist.
            Assert.Equal(2, orphans.Count);

            // The committed latest revision is never an orphan.
            Assert.DoesNotContain(latestPath, orphans);

            // Detection must not delete anything.
            Assert.True(File.Exists(firstPath));
            Assert.True(File.Exists(latestPath));
            Assert.All(orphans, path => Assert.True(File.Exists(path)));
        }
        finally
        {
            if (Directory.Exists(rootPath))
            {
                Directory.Delete(rootPath, recursive: true);
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
