
using MetricBreak.Domain.Breakpoints;
using MetricBreak.Domain.Recordings;
using MetricBreak.Domain.Replay;
using MetricBreak.Domain.Tests.TestDoubles;

namespace MetricBreak.Domain.Tests.Replay;

public sealed class ReplaySessionBreakpointTests
{
    private static readonly Guid PressureChannelId = Guid.NewGuid();

    private static ReplaySession CreateSession(
        params double?[] pressures)
    {
        var recordingId = Guid.NewGuid();

        var recording = new Recording(
            recordingId,
            "Breakpoint Test Recording",
            new RecordingSource(
                "test.csv",
                "sha256:test",
                100),
            DateTimeOffset.UtcNow);

        var samples = pressures
            .Select((pressure, index) =>
                new TelemetrySample(
                    index,
                    new TelemetryTimestamp(
                        index * 1_000_000_000L),
                    new Dictionary<Guid, double?>
                    {
                        [PressureChannelId] = pressure
                    }))
            .ToArray();

        var reader = new InMemoryTelemetryReader(
            recordingId,
            samples);

        return new ReplaySession(recording, reader);
    }

    private static ConditionalBreakpoint PressureAbove(
        double threshold)
    {
        return new ConditionalBreakpoint(
            Guid.NewGuid(),
            PressureChannelId,
            ComparisonOperator.GreaterThan,
            threshold);
    }

    [Fact]
    public void RunToBreakpoint_StopsAtFirstMatchingSample()
    {
        var session = CreateSession(
            8500, 8800, 9100, 9300, 8900);

        bool matched = session.RunToBreakpoint(
            PressureAbove(9000));

        Assert.True(matched);
        Assert.Equal(2, session.Position.SequenceNumber);
        Assert.Equal(9100d,
            session.CurrentSample.GetValue(PressureChannelId));
    }

    [Fact]
    public void RunToBreakpoint_CanContinueToNextMatch()
    {
        var session = CreateSession(
            8500, 8800, 9100, 9300, 8900, 9500);

        var breakpoint = PressureAbove(9000);

        Assert.True(session.RunToBreakpoint(breakpoint));
        Assert.Equal(2, session.Position.SequenceNumber);

        Assert.True(session.RunToBreakpoint(breakpoint));
        Assert.Equal(3, session.Position.SequenceNumber);

        Assert.True(session.RunToBreakpoint(breakpoint));
        Assert.Equal(5, session.Position.SequenceNumber);
    }

    [Fact]
    public void RunToBreakpoint_WhenNoMatch_ReachesEnd()
    {
        var session = CreateSession(
            8500, 8600, 8700);

        bool matched = session.RunToBreakpoint(
            PressureAbove(9000));

        Assert.False(matched);
        Assert.Equal(2, session.Position.SequenceNumber);
    }

    [Fact]
    public void RunToBreakpoint_AtEnd_ReturnsFalse()
    {
        var session = CreateSession(
            8500, 9100);

        session.Seek(1);

        Assert.False(session.RunToBreakpoint(
            PressureAbove(9000)));

        Assert.Equal(1, session.Position.SequenceNumber);
    }

    [Fact]
    public void RunToBreakpoint_DoesNotEvaluateCurrentSample()
    {
        var session = CreateSession(
            9500, 8500, 8600);

        bool matched = session.RunToBreakpoint(
            PressureAbove(9000));

        Assert.False(matched);
        Assert.Equal(2, session.Position.SequenceNumber);
    }

    [Fact]
    public void RunToBreakpoint_AfterSeek_StartsFromNextSample()
    {
        var session = CreateSession(
            8500, 9100, 8500, 9500);

        session.Seek(2);

        Assert.True(session.RunToBreakpoint(
            PressureAbove(9000)));

        Assert.Equal(3, session.Position.SequenceNumber);
    }

    [Fact]
    public void RunToBreakpoint_WithNullMeasurement_SkipsIt()
    {
        var session = CreateSession(
            8500, null, 9200);

        Assert.True(session.RunToBreakpoint(
            PressureAbove(9000)));

        Assert.Equal(2, session.Position.SequenceNumber);
    }

    [Fact]
    public void RunToBreakpoint_WithNullBreakpoint_Throws()
    {
        var session = CreateSession(
            8500, 9100);

        Assert.Throws<ArgumentNullException>(() =>
            session.RunToBreakpoint(null!));

        Assert.Equal(0, session.Position.SequenceNumber);
    }

    [Fact]
    public void RunToBreakpoint_WithSingleSample_ReturnsFalse()
    {
        var session = CreateSession(9500);

        Assert.False(session.RunToBreakpoint(
            PressureAbove(9000)));

        Assert.Equal(0, session.Position.SequenceNumber);
    }

    [Fact]
    public void RunToBreakpoint_AfterReset_FindsSameMatch()
    {
        var session = CreateSession(
            8500, 8800, 9100, 9300);

        var breakpoint = PressureAbove(9000);

        Assert.True(session.RunToBreakpoint(breakpoint));
        Assert.Equal(2, session.Position.SequenceNumber);

        session.Reset();

        Assert.True(session.RunToBreakpoint(breakpoint));
        Assert.Equal(2, session.Position.SequenceNumber);
    }
}
