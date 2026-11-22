using MetricBreak.Domain.Breakpoints;
using MetricBreak.Domain.Recordings;
using MetricBreak.Domain.Replay;
using MetricBreak.Domain.Tests.TestDoubles;

namespace MetricBreak.Domain.Tests.Breakpoints;

public sealed class MissingDataBreakpointTests
{
    private readonly Guid _channelId = Guid.NewGuid();

    [Fact]
    public void TimeGap_StopsWhenClockJumpExceedsDuration()
    {
        var samples = new[]
        {
            Sample(0, 0, 10),
            Sample(1, 1, 10),
            Sample(2, 7, 10),
            Sample(3, 8, 10),
        };
        var breakpoint = new MissingDataBreakpoint(Guid.NewGuid(), 5);

        Assert.Null(Hit(breakpoint, samples, 1));
        var hit = Hit(breakpoint, samples, 2);
        Assert.NotNull(hit);
        Assert.True(hit.IsMissingData);
        Assert.Equal(6d, hit.ObservedValue);
        Assert.Equal(5d, hit.Threshold);
        Assert.Null(Hit(breakpoint, samples, 3));
    }

    [Fact]
    public void TimeGap_ExactDurationDoesNotStop()
    {
        var samples = new[]
        {
            Sample(0, 0, 10),
            Sample(1, 5, 10),
        };
        var breakpoint = new MissingDataBreakpoint(Guid.NewGuid(), 5);

        Assert.Null(Hit(breakpoint, samples, 1));
    }

    [Fact]
    public void ChannelMissing_StopsOnceWhenRunExceedsDuration()
    {
        var samples = new[]
        {
            Sample(0, 0, 10),
            Sample(1, 1, null),
            Sample(2, 2, null),
            Sample(3, 3, null),
            Sample(4, 4, null),
            Sample(5, 5, null),
            Sample(6, 6, null),
            Sample(7, 7, null),
            Sample(8, 8, 10),
        };
        var breakpoint = new MissingDataBreakpoint(Guid.NewGuid(), 5, _channelId);

        Assert.Null(Hit(breakpoint, samples, 5));
        Assert.NotNull(Hit(breakpoint, samples, 6));
        Assert.Null(Hit(breakpoint, samples, 7));
        Assert.Null(Hit(breakpoint, samples, 8));
    }

    [Fact]
    public void RunToBreakpoint_StopsOnTimeGapThenContinuesPastIt()
    {
        var session = CreateSession(
            Sample(0, 0, 10),
            Sample(1, 1, 10),
            Sample(2, 7, 10),
            Sample(3, 8, 10),
            Sample(4, 15, 10));
        var breakpoint = new MissingDataBreakpoint(Guid.NewGuid(), 5);

        var first = session.RunToBreakpoint(breakpoint, forward: true);
        Assert.NotNull(first);
        Assert.Equal(2, session.Position.SequenceNumber);

        var second = session.RunToBreakpoint(breakpoint, forward: true);
        Assert.NotNull(second);
        Assert.Equal(4, session.Position.SequenceNumber);
    }

    [Fact]
    public void RunToBreakpoint_ChannelMissingFiresOnTheCrossingSampleOnly()
    {
        var session = CreateSession(
            Sample(0, 0, 10),
            Sample(1, 1, null),
            Sample(2, 2, null),
            Sample(3, 3, null),
            Sample(4, 4, null),
            Sample(5, 5, null),
            Sample(6, 6, null),
            Sample(7, 7, null),
            Sample(8, 8, 10));
        var breakpoint = new MissingDataBreakpoint(Guid.NewGuid(), 5, _channelId);

        var stop = session.RunToBreakpoint(breakpoint, forward: true);
        Assert.NotNull(stop);
        Assert.Equal(6, session.Position.SequenceNumber);

        var next = session.RunToBreakpoint(breakpoint, forward: true);
        Assert.Null(next);
        Assert.Equal(8, session.Position.SequenceNumber);
    }

    [Fact]
    public void FindBreakpointHitsInRange_ListsOnlyTheCrossingSample()
    {
        var session = CreateSession(
            Sample(0, 0, 10),
            Sample(1, 1, null),
            Sample(2, 2, null),
            Sample(3, 3, null),
            Sample(4, 4, null),
            Sample(5, 5, null),
            Sample(6, 6, null),
            Sample(7, 7, null));
        var breakpoint = new MissingDataBreakpoint(Guid.NewGuid(), 5, _channelId);

        var hits = session.FindBreakpointHitsInRange(
            new IBreakpoint[] { breakpoint },
            0,
            session.SampleCount);

        var hit = Assert.Single(hits);
        Assert.Equal(breakpoint.Id, hit.BreakpointId);
        Assert.Equal(6, hit.SampleSequence);
    }

    [Fact]
    public void Add_IncludesMissingDataBreakpointInActiveSet()
    {
        var collection = new BreakpointCollection();
        var breakpoint = new MissingDataBreakpoint(Guid.NewGuid(), 5);

        collection.Add(breakpoint);

        Assert.Equal(breakpoint, Assert.Single(collection.GetAllActive()));
        Assert.Empty(collection.GetRegistered());
    }

    [Fact]
    public void Constructor_RejectsNonPositiveDuration()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new MissingDataBreakpoint(Guid.NewGuid(), 0));
    }

    private BreakpointHit? Hit(
        MissingDataBreakpoint breakpoint,
        TelemetrySample[] samples,
        long sequence)
    {
        var recordingId = Guid.NewGuid();
        return BreakpointEvaluator.EvaluateHit(
            breakpoint,
            new SamplePosition(recordingId, sequence),
            samples[sequence],
            sequence > 0 ? samples[sequence - 1] : null,
            index => samples[index]);
    }

    private TelemetrySample Sample(long sequence, long seconds, double? value)
    {
        return new TelemetrySample(
            sequence,
            new TelemetryTimestamp(seconds * 1_000_000_000L),
            new Dictionary<Guid, double?>
            {
                [_channelId] = value,
            });
    }

    private ReplaySession CreateSession(params TelemetrySample[] samples)
    {
        var recordingId = Guid.NewGuid();
        var recording = new Recording(
            recordingId,
            "Missing data breakpoint",
            new RecordingSource("test.csv", "sha256:test", 100),
            DateTimeOffset.UtcNow);
        var reader = new InMemoryTelemetryReader(recordingId, samples);
        return new ReplaySession(recording, reader);
    }
}
