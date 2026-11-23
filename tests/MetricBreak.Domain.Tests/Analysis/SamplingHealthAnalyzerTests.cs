using MetricBreak.Domain.Analysis;
using MetricBreak.Domain.Recordings;
using MetricBreak.Domain.Tests.TestDoubles;

namespace MetricBreak.Domain.Tests.Analysis;

public sealed class SamplingHealthAnalyzerTests
{
    private const long Second = 1_000_000_000;

    [Fact]
    public void UniformCadence_CountsMissingTimestampsInsideALongGap()
    {
        var pressure = new ChannelDefinition(Guid.NewGuid(), "Pressure", "psi");
        var reader = Reader(pressure.Id,
            (0, 0, 1),
            (Second, 1, 1),
            (2 * Second, 2, 1),
            (3 * Second, 3, 1),
            (4 * Second, 4, 1),
            (5 * Second, 5, 1),
            (11 * Second, 6, null));

        var health = SamplingHealthAnalyzer.Analyze(reader, [pressure], null);

        Assert.Equal("confirmed", health.CadenceSource);
        Assert.Equal(Second, health.CadenceNanoseconds);
        Assert.Equal(7, health.SampleCount);
        Assert.Equal(1, health.GapCount);
        Assert.Equal(5, health.MissingTimestampCount);
        var gap = Assert.Single(health.Gaps);
        Assert.Equal(5, gap.BeforeSequence);
        Assert.Equal(6, gap.AfterSequence);
        Assert.Equal(5, gap.MissingTimestamps);
        var missing = Assert.Single(health.MissingChannels);
        Assert.Equal(1, missing.MissingCount);
        Assert.Equal(6, missing.FirstSequence);
        Assert.Equal(0, health.DuplicateGroupCount);
    }

    [Fact]
    public void IrregularIntervals_KeepGapsClickableWithoutCountingMissingTimestamps()
    {
        var pressure = new ChannelDefinition(Guid.NewGuid(), "Pressure", "psi");
        var reader = Reader(pressure.Id,
            (0, 0, 1),
            (Second, 1, 1),
            (3 * Second, 2, 1),
            (4 * Second, 3, 1),
            (10 * Second, 4, 1));

        var health = SamplingHealthAnalyzer.Analyze(reader, [pressure], null);

        Assert.Null(health.CadenceSource);
        Assert.Null(health.MissingTimestampCount);
        Assert.Contains(health.Gaps, gap => gap.BeforeSequence == 3 && gap.AfterSequence == 4);
        Assert.All(health.Gaps, gap => Assert.Null(gap.MissingTimestamps));
    }

    [Fact]
    public void DeclaredCadence_CountsMissingTimestampsWhenTheIntervalsDoNotAgree()
    {
        var pressure = new ChannelDefinition(Guid.NewGuid(), "Pressure", "psi");
        var reader = Reader(pressure.Id,
            (0, 0, 1),
            (Second, 1, 1),
            (3 * Second, 2, 1),
            (4 * Second, 3, 1),
            (10 * Second, 4, 1));

        var health = SamplingHealthAnalyzer.Analyze(reader, [pressure], Second);

        Assert.Equal("declared", health.CadenceSource);
        Assert.Equal(6, health.MissingTimestampCount);
        Assert.Equal(2, health.Gaps.Count);
        Assert.Equal(1, health.Gaps[0].MissingTimestamps);
        Assert.Equal(5, health.Gaps[1].MissingTimestamps);
    }

    [Fact]
    public void DuplicateTimestamps_PreserveEverySample()
    {
        var pressure = new ChannelDefinition(Guid.NewGuid(), "Pressure", "psi");
        var reader = Reader(pressure.Id,
            (0, 0, 10),
            (0, 1, 11),
            (0, 2, 12),
            (Second, 3, 13));

        var health = SamplingHealthAnalyzer.Analyze(reader, [pressure], null);

        Assert.Equal(4, health.SampleCount);
        Assert.Equal(1, health.DuplicateGroupCount);
        Assert.Equal(3, health.DuplicateSampleCount);
        var group = Assert.Single(health.Duplicates);
        Assert.Equal([0L, 1L, 2L], group.Sequences);
        Assert.Equal(0, health.GapCount);
    }

    [Fact]
    public void BackwardClock_StaysInspectableAndDoesNotInventMissingTimestamps()
    {
        var pressure = new ChannelDefinition(Guid.NewGuid(), "Pressure", "psi");
        var reader = Reader(pressure.Id,
            (0, 0, 1),
            (2 * Second, 1, 1),
            (Second, 2, 1));

        var health = SamplingHealthAnalyzer.Analyze(reader, [pressure], null);

        var backward = Assert.Single(health.Gaps, gap => gap.IntervalNanoseconds < 0);
        Assert.Equal(1, backward.BeforeSequence);
        Assert.Equal(2, backward.AfterSequence);
        Assert.Null(backward.MissingTimestamps);
    }

    private static InMemoryTelemetryReader Reader(
        Guid channelId,
        params (long Timestamp, long Sequence, double? Value)[] rows)
    {
        return new InMemoryTelemetryReader(
            Guid.NewGuid(),
            rows.Select(row => new TelemetrySample(
                row.Sequence,
                new TelemetryTimestamp(row.Timestamp),
                new Dictionary<Guid, double?> { [channelId] = row.Value })));
    }
}
