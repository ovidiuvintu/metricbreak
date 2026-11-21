namespace MetricBreak.Api.Investigations;

internal static class InvestigationDocumentNormalizer
{
    public static ExportedInvestigation Normalize(ExportedInvestigation investigation)
    {
        ArgumentNullException.ThrowIfNull(investigation);

        investigation = InvestigationBookmarkMutations.Normalize(investigation);

        if (investigation.SchemaVersion >= InvestigationSchema.CurrentVersion)
            return EnsureTimeline(EnsureMetadata(investigation));

        var name = investigation.Metadata?.Name
            ?? investigation.Recording.RecordingName;

        var metadata = new InvestigationMetadataSnapshot(
            name,
            investigation.Metadata?.Description,
            investigation.Metadata?.ProjectId,
            investigation.Metadata?.LastOpenedAtUtc == default
                ? investigation.ExportedAtUtc
                : investigation.Metadata!.LastOpenedAtUtc);

        return EnsureTimeline(investigation with
        {
            SchemaVersion = InvestigationSchema.CurrentVersion,
            Metadata = metadata,
            UiState = investigation.UiState ?? EmptyUiState(),
        });
    }

    private static ExportedInvestigation EnsureMetadata(ExportedInvestigation investigation)
    {
        if (investigation.Metadata is not null)
            return investigation;

        return EnsureTimeline(investigation with
        {
            Metadata = new InvestigationMetadataSnapshot(
                investigation.Recording.RecordingName,
                null,
                null,
                investigation.ExportedAtUtc),
            UiState = investigation.UiState ?? EmptyUiState(),
        });
    }

    private static ExportedInvestigation EnsureTimeline(ExportedInvestigation investigation) =>
        investigation with
        {
            Timeline = investigation.Timeline ?? RecordingTimelineSnapshot.Empty,
        };

    private static InvestigationUiStateSnapshot EmptyUiState() =>
        new(Array.Empty<string>(), Array.Empty<string>(), null);
}