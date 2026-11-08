
using MetricBreak.Domain.Recordings;
using MetricBreak.Domain.Replay;
using MetricBreak.Domain.Tests.TestDoubles;

namespace MetricBreak.Domain.Tests.Recordings;

public sealed class InMemoryTelemetryReaderTests
{
    private static TelemetrySample CreateSample(
        long sequenceNumber,
        long timestampNanoseconds,
        Guid channelId,
        double? value)
    {
        return new TelemetrySample(
            sequenceNumber,
            new TelemetryTimestamp(timestampNanoseconds),
            new Dictionary<Guid, double?>
            {
                [channelId] = value
            });
    }

    [Fact]
    public void Constructor_WithValidSamples_InitializesReader()
    {
        var recordingId = Guid.NewGuid();
        var channelId = Guid.NewGuid();

        var samples = new[]
        {
            CreateSample(0, 100, channelId, 8500),
            CreateSample(1, 200, channelId, 8600)
        };

        var reader = new InMemoryTelemetryReader(
            recordingId, samples);

        Assert.Equal(recordingId, reader.RecordingId);
        Assert.Equal(2, reader.SampleCount);
    }

    [Fact]
    public void Read_ReturnsCorrectSample()
    {
        var channelId = Guid.NewGuid();

        var reader = new InMemoryTelemetryReader(
            Guid.NewGuid(),
            new[]
            {
                CreateSample(0, 100, channelId, 8500),
                CreateSample(1, 200, channelId, 8600),
                CreateSample(2, 300, channelId, 8700)
            });

        var sample = reader.Read(1);

        Assert.Equal(1, sample.SequenceNumber);
        Assert.Equal(200, sample.Timestamp.UnixNanoseconds);
        Assert.Equal(8600d, sample.GetValue(channelId));
    }

    [Fact]
    public void Read_PreservesDuplicateTimestamps()
    {
        var channelId = Guid.NewGuid();

        var reader = new InMemoryTelemetryReader(
            Guid.NewGuid(),
            new[]
            {
                CreateSample(0, 100, channelId, 8500),
                CreateSample(1, 100, channelId, 8600)
            });

        Assert.Equal(
            reader.Read(0).Timestamp,
            reader.Read(1).Timestamp);

        Assert.NotEqual(
            reader.Read(0).SequenceNumber,
            reader.Read(1).SequenceNumber);
    }

    [Fact]
    public void Read_PreservesOutOfOrderTimestamps()
    {
        var channelId = Guid.NewGuid();

        var reader = new InMemoryTelemetryReader(
            Guid.NewGuid(),
            new[]
            {
                CreateSample(0, 300, channelId, 8500),
                CreateSample(1, 100, channelId, 8600),
                CreateSample(2, 200, channelId, 8700)
            });

        Assert.Equal(300, reader.Read(0).Timestamp.UnixNanoseconds);
        Assert.Equal(100, reader.Read(1).Timestamp.UnixNanoseconds);
        Assert.Equal(200, reader.Read(2).Timestamp.UnixNanoseconds);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    [InlineData(100)]
    public void Read_InvalidSequenceNumber_Throws(
        long sequenceNumber)
    {
        var channelId = Guid.NewGuid();

        var reader = new InMemoryTelemetryReader(
            Guid.NewGuid(),
            new[]
            {
                CreateSample(0, 100, channelId, 8500),
                CreateSample(1, 200, channelId, 8600)
            });

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            reader.Read(sequenceNumber));
    }

    [Fact]
    public void Constructor_WithEmptyRecordingId_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new InMemoryTelemetryReader(
                Guid.Empty,
                Array.Empty<TelemetrySample>()));
    }

    [Fact]
    public void Constructor_WithNullSamples_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new InMemoryTelemetryReader(
                Guid.NewGuid(),
                null!));
    }

    [Fact]
    public void Constructor_WithNonContiguousSequences_Throws()
    {
        var channelId = Guid.NewGuid();

        var samples = new[]
        {
            CreateSample(0, 100, channelId, 8500),
            CreateSample(2, 200, channelId, 8600)
        };

        Assert.Throws<ArgumentException>(() =>
            new InMemoryTelemetryReader(
                Guid.NewGuid(), samples));
    }

    [Fact]
    public void Constructor_WithOutOfOrderSequences_Throws()
    {
        var channelId = Guid.NewGuid();

        var samples = new[]
        {
            CreateSample(1, 100, channelId, 8500),
            CreateSample(0, 200, channelId, 8600)
        };

        Assert.Throws<ArgumentException>(() =>
            new InMemoryTelemetryReader(
                Guid.NewGuid(), samples));
    }

    [Fact]
    public void Constructor_CopiesInputCollection()
    {
        var channelId = Guid.NewGuid();

        var samples = new List<TelemetrySample>
        {
            CreateSample(0, 100, channelId, 8500)
        };

        var reader = new InMemoryTelemetryReader(
            Guid.NewGuid(), samples);

        samples.Add(CreateSample(1, 200, channelId, 8600));

        Assert.Equal(1, reader.SampleCount);
    }

    [Fact]
    public void Read_WithReplayCursor_ReturnsCurrentSample()
    {
        var recordingId = Guid.NewGuid();
        var channelId = Guid.NewGuid();

        var reader = new InMemoryTelemetryReader(
            recordingId,
            new[]
            {
                CreateSample(0, 100, channelId, 8500),
                CreateSample(1, 200, channelId, 8600),
                CreateSample(2, 300, channelId, 8700)
            });

        var cursor = new ReplayCursor(
            reader.RecordingId,
            reader.SampleCount);

        Assert.Equal(
            8500d,
            reader.Read(cursor.CurrentSequenceNumber)
                .GetValue(channelId));

        cursor.StepForward();

        Assert.Equal(
            8600d,
            reader.Read(cursor.CurrentSequenceNumber)
                .GetValue(channelId));

        cursor.StepBackward();

        Assert.Equal(
            8500d,
            reader.Read(cursor.CurrentSequenceNumber)
                .GetValue(channelId));
    }

    [Fact]
    public void Constructor_WithEmptyCollection_AllowsEmptyReader()
    {
        var reader = new InMemoryTelemetryReader(
            Guid.NewGuid(),
            Array.Empty<TelemetrySample>());

        Assert.Equal(0, reader.SampleCount);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            reader.Read(0));
    }
}
