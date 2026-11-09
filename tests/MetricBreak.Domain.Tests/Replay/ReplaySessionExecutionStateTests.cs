
using MetricBreak.Domain.Breakpoints;
using MetricBreak.Domain.Recordings;
using MetricBreak.Domain.Replay;
using MetricBreak.Ingestion;

namespace MetricBreak.Domain.Tests.Replay;

public sealed class ReplaySessionExecutionStateTests
{
    private static readonly Guid _channelId = Guid.NewGuid();

    private static readonly TelemetryTimestamp[] _timestamps =
    [
        new(1_700_000_000_000_000_000),
        new(1_700_000_001_000_000_000),
        new(1_700_000_002_000_000_000)
    ];

    [Fact]
    public void NewSession_StartsReady()
    {
        var session = CreateSession();

        Assert.Equal(
            ReplayExecutionStatus.Ready,
            session.ExecutionState.Status);

        Assert.Equal(
            ReplayPauseReason.None,
            session.ExecutionState.PauseReason);

        Assert.Null(session.ExecutionState.Position);
        Assert.Null(session.ExecutionState.Timestamp);
    }

    [Fact]
    public void StepForward_PausesWithUserReason()
    {
        var session = CreateSession();

        Assert.True(session.StepForward());

        AssertState(
            session,
            ReplayExecutionStatus.Paused,
            ReplayPauseReason.User,
            1);
    }

    [Fact]
    public void StepBackward_UpdatesPositionAndTimestamp()
    {
        var session = CreateSession();

        session.Seek(2);

        Assert.True(session.StepBackward());

        AssertState(
            session,
            ReplayExecutionStatus.Paused,
            ReplayPauseReason.User,
            1);
    }

    [Fact]
    public void Seek_UpdatesExecutionState()
    {
        var session = CreateSession();

        session.Seek(2);

        AssertState(
            session,
            ReplayExecutionStatus.Paused,
            ReplayPauseReason.User,
            2);
    }

    [Fact]
    public void Reset_RestoresReadyState()
    {
        var session = CreateSession();

        session.Seek(2);
        session.Reset();

        Assert.Equal(0, session.Position.SequenceNumber);

        Assert.Equal(
            ReplayExecutionStatus.Ready,
            session.ExecutionState.Status);

        Assert.Equal(
            ReplayPauseReason.None,
            session.ExecutionState.PauseReason);

        Assert.Null(session.ExecutionState.Position);
        Assert.Null(session.ExecutionState.Timestamp);
    }

    [Fact]
    public void BreakpointHit_PausesWithBreakpointReason()
    {
        var session = CreateSession();

        var breakpoint = CreateBreakpoint(
            ComparisonOperator.GreaterThan,
            9000);

        var hit = session.RunToBreakpointHit(breakpoint);

        Assert.NotNull(hit);

        AssertState(
            session,
            ReplayExecutionStatus.Paused,
            ReplayPauseReason.Breakpoint,
            2);
    }

    [Fact]
    public void BreakpointScanWithoutHit_CompletesReplay()
    {
        var session = CreateSession();

        var breakpoint = CreateBreakpoint(
            ComparisonOperator.GreaterThan,
            10000);

        var hit = session.RunToBreakpointHit(breakpoint);

        Assert.Null(hit);

        AssertState(
            session,
            ReplayExecutionStatus.Completed,
            ReplayPauseReason.EndOfRecording,
            2);
    }

    [Fact]
    public void FailedStepAtBoundary_PreservesExecutionState()
    {
        var session = CreateSession();

        session.Seek(2);

        var previousState = session.ExecutionState;

        Assert.False(session.StepForward());

        Assert.Same(
            previousState,
            session.ExecutionState);
    }

    [Fact]
    public void NoActiveBreakpoints_PreservesExecutionState()
    {
        var session = CreateSession();

        var breakpoints = new BreakpointCollection();

        var previousState = session.ExecutionState;

        var stop = session.RunToBreakpoints(breakpoints);

        Assert.Null(stop);

        Assert.Same(
            previousState,
            session.ExecutionState);

        Assert.Equal(
            0,
            session.Position.SequenceNumber);
    }

    private static ReplaySession CreateSession()
    {
        var recordingId = Guid.NewGuid();

        var recording = new Recording(
            recordingId,
            "Execution State Test",
            new RecordingSource(
                "test.csv",
                "test-hash",
                100),
            DateTimeOffset.UtcNow);

        var channelIndex = new ChannelIndex([_channelId]);

        var samples = new[]
        {
            new TelemetrySample(
                0,
                _timestamps[0],
                channelIndex,
                new double?[] { 8500 }),

            new TelemetrySample(
                1,
                _timestamps[1],
                channelIndex,
                new double?[] { 8700 }),

            new TelemetrySample(
                2,
                _timestamps[2],
                channelIndex,
                new double?[] { 9500 })
        };

        var reader = new IndexedTelemetryReader(
            recordingId,
            samples);

        return new ReplaySession(recording, reader);
    }

    private static ConditionalBreakpoint CreateBreakpoint(
        ComparisonOperator comparison,
        double threshold)
    {
        return new ConditionalBreakpoint(
            Guid.NewGuid(),
            _channelId,
            comparison,
            threshold);
    }

    private static void AssertState(
        ReplaySession session,
        ReplayExecutionStatus expectedStatus,
        ReplayPauseReason expectedReason,
        long expectedSequence)
    {
        var state = session.ExecutionState;

        Assert.Equal(expectedStatus, state.Status);
        Assert.Equal(expectedReason, state.PauseReason);

        Assert.True(state.Position.HasValue);
        Assert.True(state.Timestamp.HasValue);

        Assert.Equal(
            session.RecordingId,
            state.Position.Value.RecordingId);

        Assert.Equal(
            expectedSequence,
            state.Position.Value.SequenceNumber);

        Assert.Equal(
            _timestamps[(int)expectedSequence],
            state.Timestamp.Value);
    }
}
