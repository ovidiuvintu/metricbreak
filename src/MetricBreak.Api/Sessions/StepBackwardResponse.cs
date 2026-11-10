namespace MetricBreak.Api.Sessions;

public sealed record StepBackwardResponse(
    bool MovedBackward,
    SessionStateResponse Session);
