
using MetricBreak.Domain.Breakpoints;
using MetricBreak.Domain.Recordings;
using MetricBreak.Domain.Replay;
using MetricBreak.Domain.Tests.TestDoubles;

namespace MetricBreak.Domain.Tests.Replay;

public sealed class ReplaySessionBreakpointTests
{
    private static readonly Guid _pressureChannelId = Guid.NewGuid();

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
                        [_pressureChannelId] = pressure
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
            _pressureChannelId,
            ComparisonOperator.GreaterThan,
            threshold);
    }

    [Fact]
    public void FindBreakpointHitsInRange_ListsHitsWithoutMovingCursor()
    {
        var session = CreateSession(
            8500, 9200, 8800, 9500);

        var breakpoint = PressureAbove(9000);
        var initial = session.Position.SequenceNumber;

        var hits = session.FindBreakpointHitsInRange(
            new IBreakpoint[] { breakpoint },
            fromSequenceInclusive: 0,
            toSequenceExclusive: 4);

        Assert.Equal(initial, session.Position.SequenceNumber);
        Assert.Equal(2, hits.Count);
        Assert.Contains(hits, h => h.SampleSequence == 1);
        Assert.Contains(hits, h => h.SampleSequence == 3);
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
            session.CurrentSample.GetValue(_pressureChannelId));
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

    [Fact]
    public void RunBackwardToBreakpoints_StopsAtFirstMatchingSample()
    {
        var session = CreateSession(
            8500, 8800, 9100, 9300, 8900);

        session.Seek(4);

        var breakpoints = new BreakpointCollection();
        breakpoints.Add(new ConditionalBreakpoint(
            Guid.NewGuid(),
            _pressureChannelId,
            ComparisonOperator.GreaterThan,
            9000));

        var stop = session.RunBackwardToBreakpoints(breakpoints);

        Assert.NotNull(stop);
        Assert.Equal(3, session.Position.SequenceNumber);
        Assert.Equal(9300d,
            session.CurrentSample.GetValue(_pressureChannelId));
    }

    [Fact]
    public void RunBackwardToBreakpoints_WhenNoMatch_ReachesStart()
    {
        var session = CreateSession(
            8500, 8600, 8700);

        session.Seek(2);

        var breakpoints = new BreakpointCollection();
        breakpoints.Add(new ConditionalBreakpoint(
            Guid.NewGuid(),
            _pressureChannelId,
            ComparisonOperator.GreaterThan,
            9000));

        var stop = session.RunBackwardToBreakpoints(breakpoints);

        Assert.Null(stop);
        Assert.Equal(0, session.Position.SequenceNumber);
        Assert.Equal(
            ReplayPauseReason.StartOfRecording,
            session.ExecutionState.PauseReason);
    }

    [Fact]
    public void RisingEdge_StopsOnlyAtActualCrossings_InBothDirections()
    {
        var session = CreateSession(90, 100, 105, 110, 98, 107, 108);
        var breakpoint = new ConditionalBreakpoint(Guid.NewGuid(), _pressureChannelId,
            ComparisonOperator.RisingEdge, 100);
        var collection = new BreakpointCollection();
        collection.Add(breakpoint);
        Assert.Equal(2, session.RunToBreakpoints(collection)!.Position.SequenceNumber);
        Assert.Equal(5, session.RunToBreakpoints(collection)!.Position.SequenceNumber);
        session.Seek(6);
        Assert.Equal(5, session.RunBackwardToBreakpoints(collection)!.Position.SequenceNumber);
        Assert.Equal(2, session.RunBackwardToBreakpoints(collection)!.Position.SequenceNumber);
    }

    [Fact]
    public void RisingEdge_DoesNotBridgeMissingValues()
    {
        var session = CreateSession(90, null, 110, 90, 101);
        var breakpoint = new ConditionalBreakpoint(Guid.NewGuid(), _pressureChannelId,
            ComparisonOperator.RisingEdge, 100);
        Assert.True(session.RunToBreakpoint(breakpoint));
        Assert.Equal(4, session.Position.SequenceNumber);
    }

    [Fact]
    public void RisingEdge_WorksAcrossChartPageBoundary()
    {
        var samples = Enumerable.Repeat<double?>(90, 5000).Concat(new double?[] { 110 }).ToArray();
        var session = CreateSession(samples);
        var breakpoint = new ConditionalBreakpoint(Guid.NewGuid(), _pressureChannelId,
            ComparisonOperator.RisingEdge, 100);
        Assert.True(session.RunToBreakpoint(breakpoint));
        Assert.Equal(5000, session.Position.SequenceNumber);
    }
}
