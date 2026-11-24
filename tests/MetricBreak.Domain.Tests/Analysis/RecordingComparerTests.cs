using MetricBreak.Domain.Analysis;
using MetricBreak.Domain.Recordings;
using MetricBreak.Domain.Tests.TestDoubles;

namespace MetricBreak.Domain.Tests.Analysis;

public sealed class RecordingComparerTests
{
    [Fact]
    public void Compare_ReportsTheFirstMismatchAndChannelsMissingFromEitherSide()
    {
        var subjectPressure = new ChannelDefinition(Guid.NewGuid(), "Pressure", "psi");
        var subjectOnly = new ChannelDefinition(Guid.NewGuid(), "Rate", null);
        var referencePressure = new ChannelDefinition(Guid.NewGuid(), "Pressure", "psi");
        var referenceOnly = new ChannelDefinition(Guid.NewGuid(), "Stage", null, ChannelValueType.Categorical);

        var subject = new InMemoryTelemetryReader(Guid.NewGuid(),
        [
            Numeric(0, subjectPressure.Id, 1d, subjectOnly.Id, 10d),
            Numeric(1, subjectPressure.Id, 2d, subjectOnly.Id, 10d),
            Numeric(2, subjectPressure.Id, 3d, subjectOnly.Id, 10d),
        ]);
        var reference = new InMemoryTelemetryReader(Guid.NewGuid(),
        [
            Mixed(0, referencePressure.Id, 1, referenceOnly.Id, "Pad"),
            Mixed(1, referencePressure.Id, 9, referenceOnly.Id, "Pad"),
        ]);

        var comparison = RecordingComparer.Compare(
            subject,
            [subjectPressure, subjectOnly],
            reference,
            [referencePressure, referenceOnly]);

        Assert.Equal(3, comparison.SubjectSampleCount);
        Assert.Equal(2, comparison.ReferenceSampleCount);
        Assert.Equal(2, comparison.ComparedSampleCount);
        Assert.Equal(["Rate"], comparison.OnlyInSubject);
        Assert.Equal(["Stage"], comparison.OnlyInReference);

        var pressure = Assert.Single(comparison.Channels);
        Assert.Equal(subjectPressure.Id, pressure.ChannelId);
        Assert.Equal(1, pressure.MismatchCount);
        Assert.Equal(1, pressure.FirstMismatchSequence);
        Assert.Equal(7, pressure.MaxAbsoluteDelta);
        Assert.Equal(1, pressure.MaxAbsoluteDeltaSequence);
    }

    [Fact]
    public void Compare_MatchingRecordingsHaveNoMismatches()
    {
        var left = new ChannelDefinition(Guid.NewGuid(), "Pressure", null);
        var right = new ChannelDefinition(Guid.NewGuid(), "Pressure", null);
        var subject = new InMemoryTelemetryReader(Guid.NewGuid(),
        [
            Numeric(0, left.Id, 4d),
            Numeric(1, left.Id, 5d),
        ]);
        var reference = new InMemoryTelemetryReader(Guid.NewGuid(),
        [
            Numeric(0, right.Id, 4d),
            Numeric(1, right.Id, 5d),
        ]);

        var comparison = RecordingComparer.Compare(subject, [left], reference, [right]);
        var channel = Assert.Single(comparison.Channels);

        Assert.Equal(0, channel.MismatchCount);
        Assert.Null(channel.FirstMismatchSequence);
        Assert.Empty(comparison.OnlyInSubject);
        Assert.Empty(comparison.OnlyInReference);
    }

    private static TelemetrySample Numeric(long sequence, params object[] pairs)
    {
        var values = new Dictionary<Guid, double?>();
        for (var index = 0; index < pairs.Length; index += 2)
            values[(Guid)pairs[index]] = (double)pairs[index + 1];

        return new TelemetrySample(sequence, new TelemetryTimestamp(sequence), values);
    }

    private static TelemetrySample Mixed(
        long sequence,
        Guid pressureId,
        double pressure,
        Guid stageId,
        string stage)
    {
        return TelemetrySample.FromTypedValues(
            sequence,
            new TelemetryTimestamp(sequence),
            new Dictionary<Guid, ChannelValue?>
            {
                [pressureId] = new ChannelValue.Numeric(pressure),
                [stageId] = new ChannelValue.Categorical(stage),
            });
    }
}
