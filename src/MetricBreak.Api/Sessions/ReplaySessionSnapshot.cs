using MetricBreak.Domain.Recordings;
using MetricBreak.Domain.Replay;

namespace MetricBreak.Api.Sessions;

public sealed record ReplaySessionSnapshot(
    Guid SessionId,
    Guid RecordingId,
    string RecordingName,
    long SampleCount,
    ReplayExecutionState ExecutionState,
    SamplePosition Position,
    TelemetrySample CurrentSample);
