
using MetricBreak.Domain.Recordings;

namespace MetricBreak.Domain.Tests.Recordings;

public sealed class SamplePositionTests
{
    [Fact]
    public void Constructor_WithValidValues_CreatesPosition()
    {
        var recordingId = Guid.NewGuid();

        var position = new SamplePosition(recordingId, 42);

        Assert.Equal(recordingId, position.RecordingId);
        Assert.Equal(42, position.SequenceNumber);
    }

    [Fact]
    public void Constructor_WithEmptyRecordingId_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new SamplePosition(Guid.Empty, 0));
    }

    [Fact]
    public void Constructor_WithNegativeSequenceNumber_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SamplePosition(Guid.NewGuid(), -1));
    }

    [Fact]
    public void Positions_WithSameValues_AreEqual()
    {
        var recordingId = Guid.NewGuid();

        var first = new SamplePosition(recordingId, 10);
        var second = new SamplePosition(recordingId, 10);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Positions_WithDifferentSequenceNumbers_AreNotEqual()
    {
        var recordingId = Guid.NewGuid();

        var first = new SamplePosition(recordingId, 10);
        var second = new SamplePosition(recordingId, 11);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Positions_InDifferentRecordings_AreNotEqual()
    {
        var first = new SamplePosition(Guid.NewGuid(), 10);
        var second = new SamplePosition(Guid.NewGuid(), 10);

        Assert.NotEqual(first, second);
    }
}
