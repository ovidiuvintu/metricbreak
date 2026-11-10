namespace MetricBreak.Api.Sessions;

public sealed record ExportSessionResponse(
    Guid InvestigationId,
    string FilePath);
