namespace MetricBreak.Api.Sessions;

public sealed record SessionStateResponse(
    Guid SessionId,
    Guid RecordingId,
    string RecordingName,
    long SampleCount,
    string ExecutionState,
    string PauseReason,
    CurrentSampleResponse CurrentSample);

public sealed record CurrentSampleResponse(
    long Sequence,
    long TimestampNanoseconds,
    IReadOnlyDictionary<Guid, double?> Channels);
