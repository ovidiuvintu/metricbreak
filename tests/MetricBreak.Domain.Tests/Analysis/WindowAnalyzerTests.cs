using MetricBreak.Domain.Analysis;
using MetricBreak.Domain.Recordings;
using MetricBreak.Domain.Tests.TestDoubles;

namespace MetricBreak.Domain.Tests.Analysis;

public sealed class WindowAnalyzerTests
{
    [Fact]
    public void NumericWindow_ReportsExtremesMeanAndIgnoresMissingValues()
    {
        var pressure = new ChannelDefinition(Guid.NewGuid(), "Pressure", "psi");
        var reader = new InMemoryTelemetryReader(Guid.NewGuid(),
        [
            Sample(0, 1_000, pressure.Id, 10),
            Sample(1, 2_000, pressure.Id, null),
            Sample(2, 3_000, pressure.Id, 30),
            Sample(3, 4_000, pressure.Id, 20),
        ]);

        var analysis = WindowAnalyzer.Analyze(reader, [pressure], 0, 2);

        var channel = Assert.Single(analysis.Channels);
        Assert.Equal(3, analysis.SampleCount);
        Assert.Equal(2, channel.PresentCount);
        Assert.Equal(1, channel.MissingCount);
        Assert.Equal(10, channel.Minimum);
        Assert.Equal(0, channel.MinimumSequence);
        Assert.Equal(30, channel.Maximum);
        Assert.Equal(2, channel.MaximumSequence);
        Assert.Equal(20, channel.Mean);
        Assert.Equal(20, channel.Median);
        Assert.Equal(Math.Sqrt(200), channel.StandardDeviation!.Value, precision: 9);
        Assert.Empty(analysis.Correlations);
    }

    [Fact]
    public void NumericWindow_KeepsTheEarliestTieAndStaysInsideTheWindow()
    {
        var pressure = new ChannelDefinition(Guid.NewGuid(), "Pressure", "psi");
        var reader = new InMemoryTelemetryReader(Guid.NewGuid(),
        [
            Sample(0, 1_000, pressure.Id, 5),
            Sample(1, 2_000, pressure.Id, 5),
            Sample(2, 3_000, pressure.Id, 9),
            Sample(3, 4_000, pressure.Id, 1),
        ]);

        var analysis = WindowAnalyzer.Analyze(reader, [pressure], 0, 2);
        var channel = Assert.Single(analysis.Channels);

        Assert.Equal(0, channel.MinimumSequence);
        Assert.Equal(2, channel.MaximumSequence);
        Assert.Equal(5, channel.Minimum);
        Assert.Equal(9, channel.Maximum);
    }

    [Fact]
    public void BooleanAndCategorical_CountStatesChangesAndMode()
    {
        var valve = new ChannelDefinition(Guid.NewGuid(), "Valve", null, ChannelValueType.Boolean);
        var stage = new ChannelDefinition(Guid.NewGuid(), "Stage", null, ChannelValueType.Categorical);
        var reader = new InMemoryTelemetryReader(Guid.NewGuid(),
        [
            Typed(0, valve.Id, new ChannelValue.Boolean(false), stage.Id, new ChannelValue.Categorical("Pad")),
            Typed(1, valve.Id, new ChannelValue.Boolean(true), stage.Id, new ChannelValue.Categorical("Pad")),
            Typed(2, valve.Id, new ChannelValue.Boolean(true), stage.Id, new ChannelValue.Categorical("Flush")),
            Typed(3, valve.Id, null, stage.Id, new ChannelValue.Categorical("Pad")),
        ]);

        var analysis = WindowAnalyzer.Analyze(reader, [valve, stage], 0, 3);
        var valveSummary = analysis.Channels[0];
        var stageSummary = analysis.Channels[1];

        Assert.Equal(1, valveSummary.FalseCount);
        Assert.Equal(2, valveSummary.TrueCount);
        Assert.Equal(1, valveSummary.MissingCount);
        Assert.Equal(1, valveSummary.ChangeCount);
        Assert.Equal(2, stageSummary.DistinctCount);
        Assert.Equal("Pad", stageSummary.Mode);
        Assert.Equal(3, stageSummary.ModeCount);
        Assert.Equal(2, stageSummary.ChangeCount);
    }

    [Fact]
    public void Correlation_IsOneForMatchingSeriesAndNegativeForOpposites()
    {
        var left = new ChannelDefinition(Guid.NewGuid(), "Left", null);
        var right = new ChannelDefinition(Guid.NewGuid(), "Right", null);
        var opposite = new ChannelDefinition(Guid.NewGuid(), "Opposite", null);
        var reader = new InMemoryTelemetryReader(Guid.NewGuid(),
        [
            Row(0, (left.Id, 1), (right.Id, 1), (opposite.Id, 3)),
            Row(1, (left.Id, 2), (right.Id, 2), (opposite.Id, 2)),
            Row(2, (left.Id, 3), (right.Id, 3), (opposite.Id, 1)),
        ]);

        var analysis = WindowAnalyzer.Analyze(reader, [left, right, opposite], 0, 2);

        var matching = analysis.Correlations.Single(pair =>
            pair.LeftChannelId == left.Id && pair.RightChannelId == right.Id);
        var inverted = analysis.Correlations.Single(pair =>
            pair.LeftChannelId == left.Id && pair.RightChannelId == opposite.Id);

        Assert.Equal(1, matching.Correlation);
        Assert.Equal(3, matching.OverlapCount);
        Assert.Equal(-1, inverted.Correlation);
    }

    private static TelemetrySample Sample(
        long sequence,
        long timestamp,
        Guid channelId,
        double? value)
    {
        return new TelemetrySample(
            sequence,
            new TelemetryTimestamp(timestamp),
            new Dictionary<Guid, double?> { [channelId] = value });
    }

    private static TelemetrySample Typed(
        long sequence,
        Guid valveId,
        ChannelValue? valve,
        Guid stageId,
        ChannelValue? stage)
    {
        return TelemetrySample.FromTypedValues(
            sequence,
            new TelemetryTimestamp(sequence),
            new Dictionary<Guid, ChannelValue?>
            {
                [valveId] = valve,
                [stageId] = stage,
            });
    }

    private static TelemetrySample Row(
        long sequence,
        params (Guid Id, double Value)[] values)
    {
        return new TelemetrySample(
            sequence,
            new TelemetryTimestamp(sequence),
            values.ToDictionary(item => item.Id, item => (double?)item.Value));
    }
}
