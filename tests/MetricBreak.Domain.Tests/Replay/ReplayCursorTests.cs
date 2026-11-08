
using MetricBreak.Domain.Replay;

namespace MetricBreak.Domain.Tests.Replay;

public sealed class ReplayCursorTests
{
    [Fact]
    public void Constructor_StartsAtFirstSample()
    {
        var recordingId = Guid.NewGuid();

        var cursor = new ReplayCursor(recordingId, 100);

        Assert.Equal(recordingId, cursor.RecordingId);
        Assert.Equal(100, cursor.SampleCount);
        Assert.Equal(0, cursor.CurrentSequenceNumber);
        Assert.Equal(recordingId, cursor.Position.RecordingId);
        Assert.Equal(0, cursor.Position.SequenceNumber);
    }

    [Fact]
    public void Constructor_WithEmptyRecordingId_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new ReplayCursor(Guid.Empty, 10));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WithInvalidSampleCount_Throws(
        long sampleCount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ReplayCursor(Guid.NewGuid(), sampleCount));
    }

    [Fact]
    public void StepForward_AdvancesOneSample()
    {
        var cursor = new ReplayCursor(Guid.NewGuid(), 10);

        Assert.True(cursor.StepForward());
        Assert.Equal(1, cursor.CurrentSequenceNumber);
    }

    [Fact]
    public void StepBackward_MovesBackOneSample()
    {
        var cursor = new ReplayCursor(Guid.NewGuid(), 10);

        cursor.Seek(5);

        Assert.True(cursor.StepBackward());
        Assert.Equal(4, cursor.CurrentSequenceNumber);
    }

    [Fact]
    public void StepForward_AtEnd_ReturnsFalseAndDoesNotMove()
    {
        var cursor = new ReplayCursor(Guid.NewGuid(), 3);

        cursor.Seek(2);

        Assert.False(cursor.StepForward());
        Assert.Equal(2, cursor.CurrentSequenceNumber);
        Assert.False(cursor.CanStepForward);
    }

    [Fact]
    public void StepBackward_AtBeginning_ReturnsFalseAndDoesNotMove()
    {
        var cursor = new ReplayCursor(Guid.NewGuid(), 3);

        Assert.False(cursor.StepBackward());
        Assert.Equal(0, cursor.CurrentSequenceNumber);
        Assert.False(cursor.CanStepBackward);
    }

    [Fact]
    public void Seek_MovesToRequestedSample()
    {
        var cursor = new ReplayCursor(Guid.NewGuid(), 100);

        cursor.Seek(75);

        Assert.Equal(75, cursor.CurrentSequenceNumber);
        Assert.Equal(75, cursor.Position.SequenceNumber);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(10)]
    [InlineData(100)]
    public void Seek_OutsideRecording_Throws(long sequenceNumber)
    {
        var cursor = new ReplayCursor(Guid.NewGuid(), 10);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            cursor.Seek(sequenceNumber));
    }

    [Fact]
    public void Seek_WhenInvalid_PreservesCurrentPosition()
    {
        var cursor = new ReplayCursor(Guid.NewGuid(), 10);

        cursor.Seek(5);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            cursor.Seek(10));

        Assert.Equal(5, cursor.CurrentSequenceNumber);
    }

    [Fact]
    public void Reset_ReturnsToFirstSample()
    {
        var cursor = new ReplayCursor(Guid.NewGuid(), 10);

        cursor.Seek(8);
        cursor.Reset();

        Assert.Equal(0, cursor.CurrentSequenceNumber);
    }

    [Fact]
    public void ForwardThenBackward_ReturnsToOriginalPosition()
    {
        var cursor = new ReplayCursor(Guid.NewGuid(), 10);

        cursor.Seek(5);

        Assert.True(cursor.StepForward());
        Assert.True(cursor.StepBackward());

        Assert.Equal(5, cursor.CurrentSequenceNumber);
    }

    [Fact]
    public void SingleSampleRecording_CannotStep()
    {
        var cursor = new ReplayCursor(Guid.NewGuid(), 1);

        Assert.False(cursor.CanStepForward);
        Assert.False(cursor.CanStepBackward);
        Assert.False(cursor.StepForward());
        Assert.False(cursor.StepBackward());
        Assert.Equal(0, cursor.CurrentSequenceNumber);
    }

    [Fact]
    public void SeekAndStep_AreIndependentOfTimestamps()
    {
        // Samples 1 and 2 could have identical timestamps.
        // The cursor navigates by sequence number only.
        var cursor = new ReplayCursor(Guid.NewGuid(), 4);

        cursor.Seek(2);
        cursor.StepBackward();

        Assert.Equal(1, cursor.CurrentSequenceNumber);
    }
}
