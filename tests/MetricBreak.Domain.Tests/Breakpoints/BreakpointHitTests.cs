
using MetricBreak.Domain.Breakpoints;
using MetricBreak.Domain.Recordings;
using MetricBreak.Domain.Replay;
using MetricBreak.Domain.Tests.TestDoubles;

namespace MetricBreak.Domain.Tests.Breakpoints;

public sealed class BreakpointHitTests
{
    private readonly Guid _recordingId = Guid.NewGuid();
    private readonly Guid _channelId = Guid.NewGuid();

    private ConditionalBreakpoint CreateBreakpoint() =>
        new(
            Guid.NewGuid(),
            _channelId,
            ComparisonOperator.GreaterThan,
            9000);

    private TelemetrySample CreateSample(
        long sequence,
        double? pressure) =>
        new(
            sequence,
            new TelemetryTimestamp(sequence * 1_000_000_000),
            new Dictionary<Guid, double?>
            {
                [_channelId] = pressure
            });

    private ReplaySession CreateSession(params double?[] pressures)
    {
        var recording = new Recording(
            _recordingId,
            "Test Recording",
            new RecordingSource("test.csv", "test-hash", 100),
            DateTimeOffset.UtcNow);

        var samples = pressures
            .Select((value, index) =>
                CreateSample(index, value))
            .ToArray();

        var reader = new InMemoryTelemetryReader(
            _recordingId,
            samples);

        return new ReplaySession(recording, reader);
    }

    [Fact]
    public void EvaluateHit_WhenMatched_CapturesEvidence()
    {
        var breakpoint = CreateBreakpoint();
        var sample = CreateSample(2, 9100);
        var position = new SamplePosition(_recordingId, 2);

        var hit = BreakpointEvaluator.EvaluateHit(
            breakpoint, position, sample);

        Assert.NotNull(hit);
        Assert.Equal(breakpoint.Id, hit.BreakpointId);
        Assert.Equal(position, hit.Position);
        Assert.Equal(sample.Timestamp, hit.Timestamp);
        Assert.Equal(_channelId, hit.ChannelId);
        Assert.Equal(9100d, hit.ObservedValue);
        Assert.Equal(9000d, hit.Threshold);
        Assert.Equal(
            ComparisonOperator.GreaterThan,
            hit.Operator);
    }

    [Fact]
    public void EvaluateHit_WhenNotMatched_ReturnsNull()
    {
        var hit = BreakpointEvaluator.EvaluateHit(
            CreateBreakpoint(),
            new SamplePosition(_recordingId, 0),
            CreateSample(0, 8500));

        Assert.Null(hit);
    }

    [Fact]
    public void EvaluateHit_WithNullMeasurement_ReturnsNull()
    {
        var hit = BreakpointEvaluator.EvaluateHit(
            CreateBreakpoint(),
            new SamplePosition(_recordingId, 0),
            CreateSample(0, null));

        Assert.Null(hit);
    }

    [Fact]
    public void EvaluateHit_WithMismatchedSequence_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            BreakpointEvaluator.EvaluateHit(
                CreateBreakpoint(),
                new SamplePosition(_recordingId, 5),
                CreateSample(2, 9100)));
    }

    [Fact]
    public void RunToBreakpointHit_ReturnsFirstMatch()
    {
        var session = CreateSession(8500, 8800, 9100, 9300);
        var breakpoint = CreateBreakpoint();

        var hit = session.RunToBreakpointHit(breakpoint);

        Assert.NotNull(hit);
        Assert.Equal(2, hit.Position.SequenceNumber);
        Assert.Equal(9100d, hit.ObservedValue);
        Assert.Equal(2, session.Position.SequenceNumber);
    }

    [Fact]
    public void RunToBreakpointHit_ContinueFindsNextMatch()
    {
        var session = CreateSession(8500, 9100, 9300);
        var breakpoint = CreateBreakpoint();

        var first = session.RunToBreakpointHit(breakpoint);
        var second = session.RunToBreakpointHit(breakpoint);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(1, first.Position.SequenceNumber);
        Assert.Equal(2, second.Position.SequenceNumber);
    }

    [Fact]
    public void RunToBreakpointHit_NoMatch_ReturnsNullAtEnd()
    {
        var session = CreateSession(8500, 8600, 8700);

        var hit = session.RunToBreakpointHit(
            CreateBreakpoint());

        Assert.Null(hit);
        Assert.Equal(2, session.Position.SequenceNumber);
    }

    [Fact]
    public void BreakpointHit_WithSameEvidence_HasValueEquality()
    {
        var breakpointId = Guid.NewGuid();
        var position = new SamplePosition(_recordingId, 2);
        var timestamp = new TelemetryTimestamp(200);

        var first = new BreakpointHit(
            breakpointId, position, timestamp,
            _channelId, 9100,
            ComparisonOperator.GreaterThan, 9000);

        var second = new BreakpointHit(
            breakpointId, position, timestamp,
            _channelId, 9100,
            ComparisonOperator.GreaterThan, 9000);

        Assert.Equal(first, second);
    }

    [Fact]
    public void RunToBreakpointHit_AfterReset_IsReproducible()
    {
        var session = CreateSession(8500, 9100, 9300);
        var breakpoint = CreateBreakpoint();

        var first = session.RunToBreakpointHit(breakpoint);

        session.Reset();

        var second = session.RunToBreakpointHit(breakpoint);

        Assert.NotNull(first);
        Assert.Equal(first, second);
    }
}
