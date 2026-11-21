using MetricBreak.Api.Workspaces;

namespace MetricBreak.Api.Investigations;

internal static class InvestigationPersistence
{
    public static ExportedInvestigation WithLastOpenedUtc(
        ExportedInvestigation investigation,
        DateTimeOffset openedAtUtc)
    {
        var metadata = investigation.Metadata
            ?? new InvestigationMetadataSnapshot(
                investigation.Recording.RecordingName,
                null,
                null,
                openedAtUtc);

        return investigation with
        {
            Metadata = metadata with { LastOpenedAtUtc = openedAtUtc },
        };
    }

    public static ExportedInvestigation MergePriorMetadata(
        ExportedInvestigation investigation,
        ExportedInvestigation? previous)
    {
        if (previous?.Metadata is null)
            return investigation;

        var current = investigation.Metadata
            ?? new InvestigationMetadataSnapshot(
                investigation.Recording.RecordingName,
                null,
                previous.Metadata.ProjectId,
                previous.Metadata.LastOpenedAtUtc);

        return investigation with
        {
            Metadata = current with
            {
                LastOpenedAtUtc = previous.Metadata.LastOpenedAtUtc,
                Description =
                    string.IsNullOrWhiteSpace(current.Description)
                        ? previous.Metadata.Description
                        : current.Description,
            },
        };
    }

    public static bool TrySave(
        ExportedInvestigation investigation,
        Guid? projectId,
        SessionInvestigationStore sessionStore,
        ProjectWorkspaceStore projectStore,
        out ProjectStoreError? error)
    {
        error = null;

        if (projectId is Guid selectedProjectId)
        {
            return projectStore.TrySaveInvestigation(
                selectedProjectId,
                investigation,
                out _,
                out error);
        }

        sessionStore.Save(investigation);
        return true;
    }
}