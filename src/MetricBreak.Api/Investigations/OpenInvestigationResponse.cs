namespace MetricBreak.Api.Investigations;

public sealed record OpenInvestigationResponse(
    Guid SessionId,
    Guid InvestigationId,
    Guid RecordingId,
    string RecordingName,
    string? InvestigationName,
    long SampleCount,
    long SequenceNumber,
    string Status);