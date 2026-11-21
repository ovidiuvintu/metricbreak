namespace MetricBreak.Api.Investigations;

public sealed record InvestigationsListResponse(
    IReadOnlyList<WorkspaceInvestigationItemResponse> Investigations);

public sealed record WorkspaceInvestigationItemResponse(
    Guid InvestigationId,
    string InvestigationName,
    string RecordingName,
    string RelativePath,
    DateTimeOffset ExportedAtUtc,
    DateTimeOffset LastOpenedAtUtc,
    string SourceContentHash,
    long SourceSizeInBytes,
    Guid RecordingId);