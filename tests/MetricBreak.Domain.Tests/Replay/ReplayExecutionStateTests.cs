
using MetricBreak.Domain.Recordings;
using MetricBreak.Domain.Replay;

namespace MetricBreak.Domain.Tests.Replay;

public sealed class ReplayExecutionStateTests
{
    private static readonly SamplePosition _position =
        new(Guid.NewGuid(), 12);

    private static readonly TelemetryTimestamp _timestamp =
        new(1_700_000_000_000_000_000);

    [Fact]
    public void Ready_HasNoPositionOrPauseReason()
    {
        var state = new ReplayExecutionState(
            ReplayExecutionStatus.Ready,
            ReplayPauseReason.None,
            null,
            null);

        Assert.Equal(ReplayExecutionStatus.Ready, state.Status);
        Assert.Equal(ReplayPauseReason.None, state.PauseReason);
        Assert.Null(state.Position);
        Assert.Null(state.Timestamp);
    }

    [Fact]
    public void Running_CanHaveCurrentPosition()
    {
        var state = new ReplayExecutionState(
            ReplayExecutionStatus.Running,
            ReplayPauseReason.None,
            _position,
            _timestamp);

        Assert.Equal(ReplayExecutionStatus.Running, state.Status);
        Assert.Equal(_position, state.Position);
        Assert.Equal(_timestamp, state.Timestamp);
    }

    [Fact]
    public void Paused_RecordsBreakpointReason()
    {
        var state = new ReplayExecutionState(
            ReplayExecutionStatus.Paused,
            ReplayPauseReason.Breakpoint,
            _position,
            _timestamp);

        Assert.Equal(ReplayExecutionStatus.Paused, state.Status);
        Assert.Equal(ReplayPauseReason.Breakpoint, state.PauseReason);
        Assert.Equal(_position, state.Position);
    }

    [Fact]
    public void Completed_RecordsEndOfRecording()
    {
        var state = new ReplayExecutionState(
            ReplayExecutionStatus.Completed,
            ReplayPauseReason.EndOfRecording,
            _position,
            _timestamp);

        Assert.Equal(ReplayExecutionStatus.Completed, state.Status);
        Assert.Equal(ReplayPauseReason.EndOfRecording, state.PauseReason);
    }

    [Fact]
    public void Completed_RecordsStartOfRecording()
    {
        var state = new ReplayExecutionState(
            ReplayExecutionStatus.Completed,
            ReplayPauseReason.StartOfRecording,
            _position,
            _timestamp);

        Assert.Equal(ReplayExecutionStatus.Completed, state.Status);
        Assert.Equal(ReplayPauseReason.StartOfRecording, state.PauseReason);
    }

    [Fact]
    public void Ready_RejectsPosition()
    {
        Assert.Throws<ArgumentException>(() =>
            new ReplayExecutionState(
                ReplayExecutionStatus.Ready,
                ReplayPauseReason.None,
                _position,
                _timestamp));
    }

    [Fact]
    public void Running_RejectsPauseReason()
    {
        Assert.Throws<ArgumentException>(() =>
            new ReplayExecutionState(
                ReplayExecutionStatus.Running,
                ReplayPauseReason.User,
                _position,
                _timestamp));
    }

    [Fact]
    public void Paused_RequiresPauseReason()
    {
        Assert.Throws<ArgumentException>(() =>
            new ReplayExecutionState(
                ReplayExecutionStatus.Paused,
                ReplayPauseReason.None,
                _position,
                _timestamp));
    }

    [Fact]
    public void Completed_RequiresEndOfRecordingReason()
    {
        Assert.Throws<ArgumentException>(() =>
            new ReplayExecutionState(
                ReplayExecutionStatus.Completed,
                ReplayPauseReason.None,
                _position,
                _timestamp));
    }

    [Fact]
    public void PositionAndTimestamp_MustBeProvidedTogether()
    {
        Assert.Throws<ArgumentException>(() =>
            new ReplayExecutionState(
                ReplayExecutionStatus.Paused,
                ReplayPauseReason.User,
                _position,
                null));
    }

    [Fact]
    public void InvalidStatus_IsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ReplayExecutionState(
                (ReplayExecutionStatus)999,
                ReplayPauseReason.None,
                null,
                null));
    }

    [Fact]
    public void InvalidPauseReason_IsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ReplayExecutionState(
                ReplayExecutionStatus.Ready,
                (ReplayPauseReason)999,
                null,
                null));
    }
}
