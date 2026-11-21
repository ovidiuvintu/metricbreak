namespace MetricBreak.Api.Breakpoints;

public sealed record BreakpointHitOccurrenceResponse(
    Guid BreakpointId,
    long SampleSequence);

public sealed record BreakpointHitsResponse(
    IReadOnlyList<BreakpointHitOccurrenceResponse> Hits,
    bool Truncated);