
using MetricBreak.Domain.Breakpoints;
using MetricBreak.Domain.Recordings;
using MetricBreak.Domain.Replay;
using MetricBreak.Ingestion;

namespace MetricBreak.Domain.Tests.Ingestion;

public sealed class IndexedReplayIntegrationTests
{
    [Fact]
    public void Replay_StopsAtBreakpoint_UsingIndexedReader()
    {
        var recordingId = Guid.NewGuid();
        var channelId = Guid.NewGuid();

        var index = new ChannelIndex([channelId]);

        var samples = new[]
        {
            new TelemetrySample(
                0,
                new TelemetryTimestamp(100),
                index,
                new double?[] { 8000 }),

            new TelemetrySample(
                1,
                new TelemetryTimestamp(200),
                index,
                new double?[] { 8500 }),

            new TelemetrySample(
                2,
                new TelemetryTimestamp(300),
                index,
                new double?[] { 9500 })
        };

        var reader = new IndexedTelemetryReader(
            recordingId,
            samples);

        var recording = new Recording(
            recordingId,
            "Indexed Test",
            new RecordingSource(
                "test.csv",
                "test-hash",
                100),
            DateTimeOffset.UtcNow);

        var session = new ReplaySession(
            recording,
            reader);

        var breakpoint = new ConditionalBreakpoint(
            Guid.NewGuid(),
            channelId,
            ComparisonOperator.GreaterThan,
            9000);

        var breakpoints = new BreakpointCollection();
        breakpoints.Add(breakpoint);

        var stop = session.RunToBreakpoints(breakpoints);

        Assert.NotNull(stop);
        Assert.Equal(2, stop.Position.SequenceNumber);
        Assert.Equal(9500.0, stop.Hits.Single().ObservedValue);

        // Verify backward replay remains deterministic.
        Assert.True(session.StepBackward());

        var previous = session.CurrentSample;

        Assert.Equal(1, previous.SequenceNumber);
        Assert.Equal(8500.0, previous.GetValue(channelId));

        Assert.True(session.StepForward());

        var forward = session.CurrentSample;

        Assert.Equal(2, forward.SequenceNumber);
        Assert.Equal(9500.0, forward.GetValue(channelId));
    }
}
