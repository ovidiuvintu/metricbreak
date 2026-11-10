namespace MetricBreak.Api.Workspaces;

public sealed record CreateProjectRequest(
    string ProjectName,
    string RootPath);
