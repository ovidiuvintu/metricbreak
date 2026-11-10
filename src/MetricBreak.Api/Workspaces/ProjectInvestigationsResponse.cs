namespace MetricBreak.Api.Workspaces;

public sealed record ProjectInvestigationsResponse(
    IReadOnlyList<ProjectInvestigationItemResponse> Investigations);

public sealed record ProjectInvestigationItemResponse(
    Guid InvestigationId,
    string RecordingName,
    string RelativePath,
    DateTimeOffset ExportedAtUtc,
    string SourceContentHash,
    long SourceSizeInBytes);
