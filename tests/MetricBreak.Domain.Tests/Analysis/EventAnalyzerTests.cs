using MetricBreak.Domain.Analysis;
using MetricBreak.Domain.Breakpoints;
using MetricBreak.Domain.Recordings;
using MetricBreak.Domain.Tests.TestDoubles;

namespace MetricBreak.Domain.Tests.Analysis;

public sealed class EventAnalyzerTests
{
    [Fact]
    public void Event_ReportsImmediateChangesFirstMovesAndChannelState()
    {
        var pressure = new ChannelDefinition(Guid.NewGuid(), "Pressure", "psi");
        var rate = new ChannelDefinition(Guid.NewGuid(), "Rate", "bbl/min");
        var reader = new InMemoryTelemetryReader(Guid.NewGuid(),
        [
            Row(0, 0, (pressure.Id, 100), (rate.Id, 1)),
            Row(1, 1_000_000_000, (pressure.Id, 100), (rate.Id, 5)),
            Row(2, 2_000_000_000, (pressure.Id, 200), (rate.Id, 5)),
            Row(3, 3_000_000_000, (pressure.Id, 210), (rate.Id, 5)),
            Row(4, 4_000_000_000, (pressure.Id, 50), (rate.Id, 5)),
        ]);

        var analysis = EventAnalyzer.Analyze(reader, [pressure, rate], eventSequence: 2, lookbackSamples: 2, []);

        Assert.Equal(2, analysis.EventSequence);
        Assert.Equal(0, analysis.LookbackStartSequence);
        Assert.Equal(1, analysis.PrecedingSequence);

        var change = Assert.Single(analysis.ImmediateChanges);
        Assert.Equal(pressure.Id, change.ChannelId);
        Assert.Equal(100, change.Previous.NumericValue);
        Assert.Equal(200, change.Current.NumericValue);

        Assert.Equal(2, analysis.FirstMoves.Count);
        Assert.Equal(rate.Id, analysis.FirstMoves[0].ChannelId);
        Assert.Equal(1, analysis.FirstMoves[0].FirstChangeSequence);
        Assert.Equal(1, analysis.FirstMoves[0].Baseline.NumericValue);
        Assert.Equal(5, analysis.FirstMoves[0].Value.NumericValue);
        Assert.Equal(pressure.Id, analysis.FirstMoves[1].ChannelId);
        Assert.Equal(2, analysis.FirstMoves[1].FirstChangeSequence);

        var state = analysis.States.Single(channel => channel.ChannelId == pressure.Id);
        Assert.Equal(200, state.Value.NumericValue);
        Assert.Equal(2, state.Sequence);
    }

    [Fact]
    public void LevelCondition_ReportsTheRunThatContainsTheEvent()
    {
        var pressure = new ChannelDefinition(Guid.NewGuid(), "Pressure", "psi");
        var reader = Reader(pressure, 100, 100, 200, 210, 50);
        var breakpoint = new ConditionalBreakpoint(
            Guid.NewGuid(),
            pressure.Id,
            ComparisonOperator.GreaterThan,
            150);

        var analysis = EventAnalyzer.Analyze(
            reader,
            [pressure],
            eventSequence: 2,
            lookbackSamples: 10,
            [new RegisteredBreakpointSnapshot(breakpoint, true)]);

        var run = Assert.Single(analysis.ConditionRuns);
        Assert.True(run.MatchesAtEvent);
        Assert.Equal(2, run.StartSequence);
        Assert.Equal(3, run.EndSequence);
        Assert.Equal(2, run.SampleCount);
        Assert.Equal(1_000_000_000, run.DurationNanoseconds);
    }

    [Fact]
    public void RisingEdge_IsASingleSample()
    {
        var pressure = new ChannelDefinition(Guid.NewGuid(), "Pressure", "psi");
        var reader = Reader(pressure, 100, 200, 210);
        var breakpoint = new ConditionalBreakpoint(
            Guid.NewGuid(),
            pressure.Id,
            ComparisonOperator.RisingEdge,
            150);

        var analysis = EventAnalyzer.Analyze(
            reader,
            [pressure],
            eventSequence: 1,
            lookbackSamples: 1,
            [new RegisteredBreakpointSnapshot(breakpoint, true)]);

        var run = Assert.Single(analysis.ConditionRuns);
        Assert.True(run.MatchesAtEvent);
        Assert.Equal(1, run.StartSequence);
        Assert.Equal(1, run.EndSequence);
        Assert.Equal(1, run.SampleCount);
        Assert.Equal(0, run.DurationNanoseconds);

        var later = EventAnalyzer.Analyze(
            reader,
            [pressure],
            eventSequence: 2,
            lookbackSamples: 1,
            [new RegisteredBreakpointSnapshot(breakpoint, true)]);
        Assert.False(Assert.Single(later.ConditionRuns).MatchesAtEvent);
    }

    [Fact]
    public void FirstSample_HasNoPrecedingChange()
    {
        var pressure = new ChannelDefinition(Guid.NewGuid(), "Pressure", "psi");
        var reader = Reader(pressure, 100, 200);
        var analysis = EventAnalyzer.Analyze(reader, [pressure], 0, 10, []);

        Assert.Null(analysis.PrecedingSequence);
        Assert.Empty(analysis.ImmediateChanges);
        Assert.Empty(analysis.FirstMoves);
        Assert.Equal(100, Assert.Single(analysis.States).Value.NumericValue);
    }

    private static InMemoryTelemetryReader Reader(ChannelDefinition channel, params double[] values)
    {
        var samples = values.Select((value, index) =>
            Row(index, index * 1_000_000_000L, (channel.Id, value))).ToArray();
        return new InMemoryTelemetryReader(Guid.NewGuid(), samples);
    }

    private static TelemetrySample Row(
        long sequence,
        long timestamp,
        params (Guid Id, double Value)[] values)
    {
        return new TelemetrySample(
            sequence,
            new TelemetryTimestamp(timestamp),
            values.ToDictionary(item => item.Id, item => (double?)item.Value));
    }
}
