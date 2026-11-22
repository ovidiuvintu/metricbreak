using MetricBreak.Domain.Breakpoints;
using MetricBreak.Domain.Recordings;

namespace MetricBreak.Domain.Tests.Breakpoints;

public sealed class BreakpointCoverageTests
{
    private static readonly Guid ChannelId = Guid.NewGuid();

    [Fact]
    public void GreaterThan_OneCrossing_CountsTheConsecutiveRun()
    {
        var breakpoint = Rule(ComparisonOperator.GreaterThan, 9000);
        var samples = Series(8900, 9100, 9200, 9300, 8000);

        var coverage = Measure(breakpoint, samples);

        Assert.Equal(1, coverage.Crossings);
        Assert.Equal(3, coverage.MatchingSamples);
        Assert.Equal(3_000_000_000L, coverage.MatchingNanoseconds);
        Assert.Equal(3, coverage.LongestRunSamples);
        Assert.Equal(BreakpointCoverageRelation.Above, coverage.Relation);
    }

    [Fact]
    public void RisingEdge_RepeatedCrossings_KeepTheLongestRunSeparate()
    {
        var breakpoint = Rule(ComparisonOperator.RisingEdge, 8000);
        var samples = Series(7990, 8010, 7990, 8500, 8600, 7990, 8010);

        var coverage = Measure(breakpoint, samples);

        Assert.Equal(3, coverage.Crossings);
        Assert.Equal(4, coverage.MatchingSamples);
        Assert.Equal(2, coverage.LongestRunSamples);
        Assert.Equal(2_000_000_000L, coverage.LongestRunNanoseconds);
    }

    [Fact]
    public void MissingValue_BreaksTheRun()
    {
        var breakpoint = Rule(ComparisonOperator.GreaterThan, 9000);
        var samples = Series(9100, 9200, null, 9300);

        var coverage = Measure(breakpoint, samples);

        Assert.Equal(0, coverage.Crossings);
        Assert.Equal(3, coverage.MatchingSamples);
        Assert.Equal(2, coverage.LongestRunSamples);
    }

    private static BreakpointCoverage Measure(
        ConditionalBreakpoint breakpoint,
        IReadOnlyList<TelemetrySample> samples)
    {
        var coverage = BreakpointCoverageAnalyzer.Measure(
            [breakpoint],
            samples.Count,
            sequence => samples[(int)sequence]);

        return Assert.Single(coverage);
    }

    private static ConditionalBreakpoint Rule(ComparisonOperator op, double threshold) =>
        new(Guid.NewGuid(), ChannelId, op, threshold);

    private static IReadOnlyList<TelemetrySample> Series(params double?[] values) =>
        values.Select((value, index) => new TelemetrySample(
            index,
            new TelemetryTimestamp(index * 1_000_000_000L),
            new Dictionary<Guid, double?> { [ChannelId] = value })).ToArray();
}
