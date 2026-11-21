namespace MetricBreak.Domain.Replay;

public readonly record struct BreakpointHitOccurrence(
    Guid BreakpointId,
    long SampleSequence);