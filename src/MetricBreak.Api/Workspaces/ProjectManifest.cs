namespace MetricBreak.Api.Workspaces;

public sealed record ProjectManifest(
    int SchemaVersion,
    Guid ProjectId,
    string ProjectName,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<ProjectInvestigationIndexItem> Investigations,
    IReadOnlyList<ProjectRecordingIndexItem> Recordings);

public sealed record ProjectRecordingIndexItem(
    Guid RecordingId,
    string RelativePath,
    string SourceName,
    string SourceContentHash,
    long SourceSizeInBytes,
    DateTimeOffset ArchivedAtUtc);

public sealed record ProjectInvestigationIndexItem(
    Guid InvestigationId,
    string RelativePath,
    string RecordingName,
    string SourceContentHash,
    long SourceSizeInBytes,
    DateTimeOffset ExportedAtUtc);
