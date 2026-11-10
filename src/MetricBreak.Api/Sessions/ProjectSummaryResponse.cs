namespace MetricBreak.Api.Sessions;

public sealed record ProjectSummaryResponse(
    Guid ProjectId,
    string ProjectName,
    string RootPath,
    int InvestigationCount);
