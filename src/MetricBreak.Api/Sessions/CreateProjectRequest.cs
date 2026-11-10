namespace MetricBreak.Api.Sessions;

public sealed record CreateProjectRequest(
    string ProjectName,
    string RootPath);
