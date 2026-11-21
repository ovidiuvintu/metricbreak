namespace MetricBreak.Api.Investigations;

internal static class InvestigationDocumentNormalizer
{
    public static ExportedInvestigation Normalize(ExportedInvestigation investigation)
    {
        ArgumentNullException.ThrowIfNull(investigation);

        investigation = InvestigationBookmarkMutations.Normalize(investigation);

        if (investigation.SchemaVersion >= InvestigationSchema.CurrentVersion)
            return EnsureMetadata(investigation);

        var name = investigation.Metadata?.Name
            ?? investigation.Recording.RecordingName;

        var metadata = new InvestigationMetadataSnapshot(
            name,
            investigation.Metadata?.Description,
            investigation.Metadata?.ProjectId,
            investigation.Metadata?.LastOpenedAtUtc == default
                ? investigation.ExportedAtUtc
                : investigation.Metadata!.LastOpenedAtUtc);

        return investigation with
        {
            SchemaVersion = InvestigationSchema.CurrentVersion,
            Metadata = metadata,
            UiState = investigation.UiState ?? EmptyUiState(),
        };
    }

    private static ExportedInvestigation EnsureMetadata(ExportedInvestigation investigation)
    {
        if (investigation.Metadata is not null)
            return investigation;

        return investigation with
        {
            Metadata = new InvestigationMetadataSnapshot(
                investigation.Recording.RecordingName,
                null,
                null,
                investigation.ExportedAtUtc),
            UiState = investigation.UiState ?? EmptyUiState(),
        };
    }

    private static InvestigationUiStateSnapshot EmptyUiState() =>
        new(Array.Empty<string>(), Array.Empty<string>(), null);
}