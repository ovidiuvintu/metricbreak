namespace MetricBreak.Api.Workspaces;

public sealed record ProjectSummaryResponse(
    Guid ProjectId,
    string ProjectName,
    string RootPath,
    int InvestigationCount);
