
using MetricBreak.Domain.Recordings;
using MetricBreak.Domain.Replay;
using MetricBreak.Domain.Breakpoints;
using MetricBreak.Ingestion;

namespace MetricBreak.Domain.Tests.Replay;

public sealed class ReplayExecutionControllerTests
{
    private static readonly Guid _channelId = Guid.NewGuid();

    [Fact]
    public void Constructor_RejectsNullSession()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ReplayExecutionController(null!));
    }

    [Fact]
    public void NewController_ExposesReadyState()
    {
        var controller = CreateController();

        Assert.Equal(
            ReplayExecutionStatus.Ready,
            controller.State.Status);

        Assert.Equal(0, controller.Position.SequenceNumber);
    }

    [Fact]
    public void StepForward_AdvancesSession()
    {
        var controller = CreateController();

        Assert.True(controller.StepForward());

        Assert.Equal(1, controller.Position.SequenceNumber);

        Assert.Equal(
            ReplayExecutionStatus.Paused,
            controller.State.Status);
    }

    [Fact]
    public void StepBackward_MovesToPreviousSample()
    {
        var controller = CreateController();

        controller.Seek(2);

        Assert.True(controller.StepBackward());

        Assert.Equal(1, controller.Position.SequenceNumber);
    }

    [Fact]
    public void Seek_MovesToRequestedSequence()
    {
        var controller = CreateController();

        controller.Seek(2);

        Assert.Equal(2, controller.Position.SequenceNumber);

        Assert.Equal(
            ReplayPauseReason.User,
            controller.State.PauseReason);
    }

    [Fact]
    public void CurrentSample_ReflectsCurrentPosition()
    {
        var controller = CreateController();

        controller.Seek(2);

        Assert.Equal(
            9500,
            controller.CurrentSample.GetValue(_channelId));
    }

    [Fact]
    public void Continue_StopsAtMatchingBreakpoint()
    {
        var controller = CreateController();

        var breakpoints = new BreakpointCollection();

        breakpoints.Add(
            new ConditionalBreakpoint(
                Guid.NewGuid(),
                _channelId,
                ComparisonOperator.GreaterThan,
                9000));

        var stop = controller.Continue(breakpoints);

        Assert.NotNull(stop);

        Assert.Equal(2, controller.Position.SequenceNumber);

        Assert.Equal(
            ReplayPauseReason.Breakpoint,
            controller.State.PauseReason);
    }

    [Fact]
    public void Continue_WithoutActiveBreakpoints_DoesNotAdvance()
    {
        var controller = CreateController();

        var stop = controller.Continue(
            new BreakpointCollection());

        Assert.Null(stop);

        Assert.Equal(0, controller.Position.SequenceNumber);

        Assert.Equal(
            ReplayExecutionStatus.Ready,
            controller.State.Status);
    }

    [Fact]
    public void Reset_RestoresInitialState()
    {
        var controller = CreateController();

        controller.Seek(2);

        controller.Reset();

        Assert.Equal(0, controller.Position.SequenceNumber);

        Assert.Equal(
            ReplayExecutionStatus.Ready,
            controller.State.Status);
    }

    private static ReplayExecutionController CreateController()
    {
        var recordingId = Guid.NewGuid();

        var recording = new Recording(
            recordingId,
            "Controller Test",
            new RecordingSource(
                "controller-test.csv",
                "test-hash",
                100),
            DateTimeOffset.UtcNow);

        var channelIndex = new ChannelIndex([_channelId]);

        var samples = new[]
        {
            new TelemetrySample(
                0,
                new TelemetryTimestamp(
                    1_700_000_000_000_000_000),
                channelIndex,
                new double?[] { 8500 }),

            new TelemetrySample(
                1,
                new TelemetryTimestamp(
                    1_700_000_001_000_000_000),
                channelIndex,
                new double?[] { 8700 }),

            new TelemetrySample(
                2,
                new TelemetryTimestamp(
                    1_700_000_002_000_000_000),
                channelIndex,
                new double?[] { 9500 })
        };

        var reader = new IndexedTelemetryReader(
            recordingId,
            samples);

        var session = new ReplaySession(
            recording,
            reader);

        return new ReplayExecutionController(session);
    }
}
