
using MetricBreak.Domain.Recordings;
using MetricBreak.Domain.Replay;
using MetricBreak.Domain.Tests.TestDoubles;

namespace MetricBreak.Domain.Tests.Replay;

public sealed class ReplaySessionTests
{
    private static Recording CreateRecording(Guid id)
    {
        return new Recording(
            id,
            "Test Recording",
            new RecordingSource("test.csv", "sha256:test", 100),
            DateTimeOffset.UtcNow);
    }

    private static ReplaySession CreateSession(
        int sampleCount = 3)
    {
        var recording = CreateRecording(Guid.NewGuid());
        var channelId = Guid.NewGuid();

        var samples = Enumerable.Range(0, sampleCount)
            .Select(i => new TelemetrySample(
                i,
                new TelemetryTimestamp(i * 1_000_000_000L),
                new Dictionary<Guid, double?>
                {
                    [channelId] = 8500 + i * 100
                }))
            .ToArray();

        var reader = new InMemoryTelemetryReader(
            recording.Id, samples);

        return new ReplaySession(recording, reader);
    }

    [Fact]
    public void Constructor_StartsAtFirstSample()
    {
        var session = CreateSession();

        Assert.Equal(0, session.Position.SequenceNumber);
        Assert.Equal(0, session.CurrentSample.SequenceNumber);
        Assert.Equal(3, session.SampleCount);
    }

    [Fact]
    public void Constructor_WithNullRecording_Throws()
    {
        var reader = new InMemoryTelemetryReader(
            Guid.NewGuid(),
            Array.Empty<TelemetrySample>());

        Assert.Throws<ArgumentNullException>(() =>
            new ReplaySession(null!, reader));
    }

    [Fact]
    public void Constructor_WithNullReader_Throws()
    {
        var recording = CreateRecording(Guid.NewGuid());

        Assert.Throws<ArgumentNullException>(() =>
            new ReplaySession(recording, null!));
    }

    [Fact]
    public void Constructor_WithMismatchedRecording_Throws()
    {
        var recording = CreateRecording(Guid.NewGuid());

        var reader = new InMemoryTelemetryReader(
            Guid.NewGuid(),
            Array.Empty<TelemetrySample>());

        Assert.Throws<ArgumentException>(() =>
            new ReplaySession(recording, reader));
    }

    [Fact]
    public void Constructor_WithEmptyReader_Throws()
    {
        var recording = CreateRecording(Guid.NewGuid());

        var reader = new InMemoryTelemetryReader(
            recording.Id,
            Array.Empty<TelemetrySample>());

        Assert.Throws<ArgumentException>(() =>
            new ReplaySession(recording, reader));
    }

    [Fact]
    public void StepForward_AdvancesAndReadsNextSample()
    {
        var session = CreateSession();

        Assert.True(session.StepForward());

        Assert.Equal(1, session.Position.SequenceNumber);
        Assert.Equal(1, session.CurrentSample.SequenceNumber);
    }

    [Fact]
    public void StepBackward_ReturnsToPreviousSample()
    {
        var session = CreateSession();

        session.Seek(2);

        Assert.True(session.StepBackward());

        Assert.Equal(1, session.Position.SequenceNumber);
        Assert.Equal(1, session.CurrentSample.SequenceNumber);
    }

    [Fact]
    public void Seek_ReadsRequestedSample()
    {
        var session = CreateSession();

        session.Seek(2);

        Assert.Equal(2, session.CurrentSample.SequenceNumber);
        Assert.Equal(2, session.Position.SequenceNumber);
    }

    [Fact]
    public void Seek_InvalidPosition_PreservesCurrentSample()
    {
        var session = CreateSession();

        session.Seek(1);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            session.Seek(10));

        Assert.Equal(1, session.CurrentSample.SequenceNumber);
    }

    [Fact]
    public void Reset_ReturnsToFirstSample()
    {
        var session = CreateSession();

        session.Seek(2);
        session.Reset();

        Assert.Equal(0, session.Position.SequenceNumber);
        Assert.Equal(0, session.CurrentSample.SequenceNumber);
    }

    [Fact]
    public void StepForward_AtEnd_ReturnsFalse()
    {
        var session = CreateSession();

        session.Seek(2);

        Assert.False(session.StepForward());
        Assert.Equal(2, session.CurrentSample.SequenceNumber);
    }

    [Fact]
    public void StepBackward_AtBeginning_ReturnsFalse()
    {
        var session = CreateSession();

        Assert.False(session.StepBackward());
        Assert.Equal(0, session.CurrentSample.SequenceNumber);
    }

    [Fact]
    public void Position_ContainsRecordingIdentity()
    {
        var session = CreateSession();

        Assert.Equal(
            session.RecordingId,
            session.Position.RecordingId);
    }

    [Fact]
    public void SingleSampleRecording_CanReadButCannotStep()
    {
        var session = CreateSession(1);

        Assert.Equal(0, session.CurrentSample.SequenceNumber);
        Assert.False(session.CanStepForward);
        Assert.False(session.CanStepBackward);
    }
}
