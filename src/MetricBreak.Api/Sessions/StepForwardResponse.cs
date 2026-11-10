namespace MetricBreak.Api.Sessions;

public sealed record StepForwardResponse(
    bool Advanced,
    SessionStateResponse Session);
