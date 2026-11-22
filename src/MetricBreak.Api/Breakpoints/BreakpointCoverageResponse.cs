namespace MetricBreak.Api.Breakpoints;

public sealed record BreakpointCoverageItemResponse(
    Guid BreakpointId,
    int? Crossings,
    int MatchingSamples,
    long MatchingNanoseconds,
    int LongestRunSamples,
    long LongestRunNanoseconds,
    string Relation);

public sealed record BreakpointCoverageResponse(
    IReadOnlyList<BreakpointCoverageItemResponse> Coverages);
