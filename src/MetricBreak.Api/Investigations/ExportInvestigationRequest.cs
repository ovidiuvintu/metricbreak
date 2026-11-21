namespace MetricBreak.Api.Investigations;

public sealed record ExportInvestigationRequest(
    Guid? InvestigationId = null,
    string? Name = null,
    string? Description = null,
    Guid? ProjectId = null,
    InvestigationUiStateSnapshot? UiState = null);