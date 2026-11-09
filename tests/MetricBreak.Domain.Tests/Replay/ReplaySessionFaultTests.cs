
using MetricBreak.Domain.Breakpoints;
using MetricBreak.Domain.Recordings;
using MetricBreak.Domain.Replay;

namespace MetricBreak.Domain.Tests.Replay;

public sealed class ReplaySessionFaultTests
{
    private static readonly Guid ChannelId = Guid.NewGuid();

    private static readonly TelemetryTimestamp[] Timestamps =
    [
        new(1_700_000_000_000_000_000),
        new(1_700_000_001_000_000_000),
        new(1_700_000_002_000_000_000)
    ];

    [Fact]
    public void ReaderFailure_DuringSingleBreakpointScan_SetsFaulted()
    {
        var session = CreateSession(failAtSequence: 1);

        var exception = Assert.Throws<InvalidOperationException>(
            () => session.RunToBreakpointHit(CreateBreakpoint()));

        Assert.Equal("Simulated reader failure.", exception.Message);

        AssertFaulted(session);
        Assert.Equal(1, session.Position.SequenceNumber);
    }

    [Fact]
    public void ReaderFailure_DuringMultipleBreakpointScan_SetsFaulted()
    {
        var session = CreateSession(failAtSequence: 1);

        var breakpoints = new[]
        {
            CreateBreakpoint(),
            CreateBreakpoint()
        };

        Assert.Throws<InvalidOperationException>(() =>
            session.RunToBreakpoints(breakpoints));

        AssertFaulted(session);
    }

    [Fact]
    public void ReaderFailure_PreservesOriginalExceptionInstance()
    {
        var expected = new InvalidOperationException(
            "Original telemetry failure.");

        var session = CreateSession(
            failAtSequence: 1,
            failure: expected);

        var actual = Assert.Throws<InvalidOperationException>(
            () => session.RunToBreakpointHit(CreateBreakpoint()));

        Assert.Same(expected, actual);
        AssertFaulted(session);
    }

    [Fact]
    public void ReaderFailure_DuringStepForward_SetsFaulted()
    {
        var session = CreateSession(failAtSequence: 1);

        Assert.Throws<InvalidOperationException>(
            () => session.StepForward());

        AssertFaulted(session);
    }

    [Fact]
    public void ReaderFailure_DuringStepBackward_SetsFaulted()
    {
        var session = CreateSession(failAtSequence: 1);

        session.Seek(2);

        Assert.Throws<InvalidOperationException>(
            () => session.StepBackward());

        AssertFaulted(session);
    }

    [Fact]
    public void ReaderFailure_DuringSeek_SetsFaulted()
    {
        var session = CreateSession(failAtSequence: 2);

        Assert.Throws<InvalidOperationException>(
            () => session.Seek(2));

        AssertFaulted(session);
    }

    [Fact]
    public void Reset_AfterFault_RestoresReady()
    {
        var session = CreateSession(failAtSequence: 1);

        Assert.Throws<InvalidOperationException>(
            () => session.StepForward());

        AssertFaulted(session);

        session.Reset();

        Assert.Equal(
            ReplayExecutionStatus.Ready,
            session.ExecutionState.Status);

        Assert.Equal(
            ReplayPauseReason.None,
            session.ExecutionState.PauseReason);

        Assert.Null(session.ExecutionState.Position);
        Assert.Null(session.ExecutionState.Timestamp);

        Assert.Equal(0, session.Position.SequenceNumber);
    }

    [Fact]
    public void InvalidSeek_DoesNotFaultSession()
    {
        var session = CreateSession();

        var previousState = session.ExecutionState;

        Assert.Throws<ArgumentOutOfRangeException>(
            () => session.Seek(100));

        Assert.Same(previousState, session.ExecutionState);

        Assert.Equal(
            ReplayExecutionStatus.Ready,
            session.ExecutionState.Status);
    }

    [Fact]
    public void FailedBoundaryStep_DoesNotFaultSession()
    {
        var session = CreateSession();

        session.Seek(2);

        var previousState = session.ExecutionState;

        Assert.False(session.StepForward());

        Assert.Same(previousState, session.ExecutionState);

        Assert.Equal(
            ReplayExecutionStatus.Paused,
            session.ExecutionState.Status);
    }

    [Fact]
    public void FaultedState_RejectsPauseReason()
    {
        Assert.Throws<ArgumentException>(() =>
            new ReplayExecutionState(
                ReplayExecutionStatus.Faulted,
                ReplayPauseReason.User,
                null,
                null));
    }

    [Fact]
    public void FaultedState_AllowsNoPosition()
    {
        var state = new ReplayExecutionState(
            ReplayExecutionStatus.Faulted,
            ReplayPauseReason.None,
            null,
            null);

        Assert.Equal(
            ReplayExecutionStatus.Faulted,
            state.Status);

        Assert.Equal(
            ReplayPauseReason.None,
            state.PauseReason);

        Assert.Null(state.Position);
        Assert.Null(state.Timestamp);
    }

    private static ReplaySession CreateSession(
        long? failAtSequence = null,
        Exception? failure = null)
    {
        var recordingId = Guid.NewGuid();

        var recording = new Recording(
            recordingId,
            "Fault Test Recording",
            new RecordingSource(
                "fault-test.csv",
                "test-hash",
                100),
            DateTimeOffset.UtcNow);

        var channelIndex = new ChannelIndex([ChannelId]);

        var samples = new[]
        {
            new TelemetrySample(
                0,
                Timestamps[0],
                channelIndex,
                new double?[] { 8500 }),

            new TelemetrySample(
                1,
                Timestamps[1],
                channelIndex,
                new double?[] { 8700 }),

            new TelemetrySample(
                2,
                Timestamps[2],
                channelIndex,
                new double?[] { 9500 })
        };

        var reader = new FaultInjectingTelemetryReader(
            recordingId,
            samples,
            failAtSequence,
            failure);

        return new ReplaySession(recording, reader);
    }

    private static ConditionalBreakpoint CreateBreakpoint()
    {
        return new ConditionalBreakpoint(
            Guid.NewGuid(),
            ChannelId,
            ComparisonOperator.GreaterThan,
            10000);
    }

    private static void AssertFaulted(ReplaySession session)
    {
        var state = session.ExecutionState;

        Assert.Equal(
            ReplayExecutionStatus.Faulted,
            state.Status);

        Assert.Equal(
            ReplayPauseReason.None,
            state.PauseReason);

        Assert.Null(state.Position);
        Assert.Null(state.Timestamp);
    }

    private sealed class FaultInjectingTelemetryReader
        : ITelemetryReader
    {
        private readonly TelemetrySample[] _samples;
        private readonly long? _failAtSequence;
        private readonly Exception _failure;

        public Guid RecordingId { get; }

        public long SampleCount => _samples.LongLength;

        public FaultInjectingTelemetryReader(
            Guid recordingId,
            TelemetrySample[] samples,
            long? failAtSequence,
            Exception? failure)
        {
            RecordingId = recordingId;
            _samples = samples.ToArray();
            _failAtSequence = failAtSequence;

            _failure = failure ??
                new InvalidOperationException(
                    "Simulated reader failure.");
        }

        public TelemetrySample Read(long sequenceNumber)
        {
            if (sequenceNumber < 0 ||
                sequenceNumber >= SampleCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(sequenceNumber));
            }

            if (sequenceNumber == _failAtSequence)
                throw _failure;

            return _samples[(int)sequenceNumber];
        }
    }
}
