using MetricBreak.Domain.Breakpoints;
using MetricBreak.Domain.Recordings;
using MetricBreak.Domain.Replay;
using MetricBreak.Domain.Tests.TestDoubles;

namespace MetricBreak.Domain.Tests.Breakpoints;

public sealed class TimelineBreakpointTests
{
    private readonly Guid _channelId = Guid.NewGuid();

    [Fact]
    public void Matches_OnlyTheAnnotatedSamples()
    {
        var breakpoint = EventBreakpoint();

        Assert.False(breakpoint.Matches(0));
        Assert.False(breakpoint.Matches(1));
        Assert.True(breakpoint.Matches(2));
        Assert.False(breakpoint.Matches(3));
        Assert.True(breakpoint.Matches(4));
        Assert.Equal("Breakdown", breakpoint.Name);
    }

    [Fact]
    public void RunToBreakpoint_StopsOnEachNamedEventAndSkipsTheSamplesBetween()
    {
        var session = CreateSession(0, 1, 2, 3, 4);
        var breakpoint = EventBreakpoint();

        var first = session.RunToBreakpoint(breakpoint, forward: true);
        Assert.NotNull(first);
        Assert.Equal(2, first.Position.SequenceNumber);
        var firstHit = Assert.Single(first.Hits);
        Assert.True(firstHit.IsTimeline);
        Assert.Equal(TimelineMarkerKind.Event, firstHit.MarkerKind);
        Assert.Equal("Breakdown", firstHit.MarkerName);

        var second = session.RunToBreakpoint(breakpoint, forward: true);
        Assert.NotNull(second);
        Assert.Equal(4, second.Position.SequenceNumber);

        Assert.Null(session.RunToBreakpoint(breakpoint, forward: true));
    }

    [Fact]
    public void StageBreakpoint_StopsOnTheStageStartOnly()
    {
        var session = CreateSession(0, 1, 2, 3, 4);
        var breakpoint = new TimelineBreakpoint(
            Guid.NewGuid(),
            TimelineMarkerKind.Stage,
            "Pad",
            new Dictionary<long, string> { [1] = "Pad" });

        var hit = session.RunToBreakpoint(breakpoint, forward: true);
        Assert.NotNull(hit);
        Assert.Equal(1, hit.Position.SequenceNumber);
        Assert.Equal("Pad", Assert.Single(hit.Hits).MarkerName);
        Assert.Null(session.RunToBreakpoint(breakpoint, forward: true));
    }

    [Fact]
    public void Constructor_RejectsAnEmptyMarkerSet()
    {
        Assert.Throws<ArgumentException>(() => new TimelineBreakpoint(
            Guid.NewGuid(),
            TimelineMarkerKind.Event,
            null,
            new Dictionary<long, string>()));
    }

    [Fact]
    public void Collection_RegistersATimelineBreakpointAsActive()
    {
        var collection = new BreakpointCollection();
        var breakpoint = EventBreakpoint();

        collection.Add(breakpoint);

        Assert.Equal(breakpoint, Assert.Single(collection.GetAllActive()));
    }

    private TimelineBreakpoint EventBreakpoint() =>
        new(
            Guid.NewGuid(),
            TimelineMarkerKind.Event,
            "Breakdown",
            new Dictionary<long, string>
            {
                [2] = "Breakdown",
                [4] = "Breakdown",
            });

    private ReplaySession CreateSession(params long[] sequences)
    {
        var samples = sequences
            .Select(sequence => new TelemetrySample(
                sequence,
                new TelemetryTimestamp(sequence * 1_000_000_000L),
                new Dictionary<Guid, double?> { [_channelId] = 1 }))
            .ToArray();
        var recordingId = Guid.NewGuid();
        var recording = new Recording(
            recordingId,
            "Timeline breakpoint",
            new RecordingSource("test.csv", "sha256:test", 100),
            DateTimeOffset.UtcNow);
        return new ReplaySession(
            recording,
            new InMemoryTelemetryReader(recordingId, samples));
    }
}
