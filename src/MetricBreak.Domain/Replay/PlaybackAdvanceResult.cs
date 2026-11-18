using MetricBreak.Domain.Breakpoints;

namespace MetricBreak.Domain.Replay;

public sealed record PlaybackAdvanceResult(
    int SamplesAdvanced,
    bool EndOfRecording,
    BreakpointStop? BreakpointStop);
