namespace MetricBreak.Api.Investigations;

public sealed record ExportSessionResponse(
    Guid InvestigationId,
    string FilePath);
