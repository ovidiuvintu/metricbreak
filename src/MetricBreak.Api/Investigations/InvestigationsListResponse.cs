namespace MetricBreak.Api.Investigations;

public sealed record InvestigationsListResponse(
    IReadOnlyList<WorkspaceInvestigationItemResponse> Investigations);

public sealed record WorkspaceInvestigationItemResponse(
    Guid InvestigationId,
    string RecordingName,
    string RelativePath,
    DateTimeOffset ExportedAtUtc,
    string SourceContentHash,
    long SourceSizeInBytes);