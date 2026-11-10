namespace MetricBreak.Api.Sessions;

public sealed record ProjectManifest(
    int SchemaVersion,
    Guid ProjectId,
    string ProjectName,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<ProjectInvestigationIndexItem> Investigations);

public sealed record ProjectInvestigationIndexItem(
    Guid InvestigationId,
    string RelativePath,
    string RecordingName,
    string SourceContentHash,
    long SourceSizeInBytes,
    DateTimeOffset ExportedAtUtc);
