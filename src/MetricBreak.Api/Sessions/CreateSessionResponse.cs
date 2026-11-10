namespace MetricBreak.Api.Sessions;

public sealed record CreateSessionResponse(
    Guid SessionId,
    Guid RecordingId,
    string RecordingName,
    long SampleCount,
    string Status);
