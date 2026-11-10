namespace MetricBreak.Api.Sessions;

public sealed record ContinueResponse(
    bool Executed,
    bool HitBreakpoint,
    bool ReachedEndOfRecording,
    BreakpointStopResponse? Stop,
    SessionStateResponse Session,
    bool ReachedStartOfRecording = false);

public sealed record BreakpointStopResponse(
    long Sequence,
    long TimestampNanoseconds,
    IReadOnlyList<BreakpointHitResponse> Hits);

public sealed record BreakpointHitResponse(
    Guid BreakpointId,
    Guid ChannelId,
    double ObservedValue,
    string Operator,
    double Threshold);
