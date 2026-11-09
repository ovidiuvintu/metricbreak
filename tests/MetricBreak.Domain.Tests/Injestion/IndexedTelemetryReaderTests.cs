
using MetricBreak.Domain.Recordings;
using MetricBreak.Ingestion;

namespace MetricBreak.Domain.Tests.Ingestion;

public sealed class IndexedTelemetryReaderTests
{
    [Fact]
    public void Constructor_PreservesRecordingId()
    {
        var recordingId = Guid.NewGuid();

        var reader = new IndexedTelemetryReader(
            recordingId,
            CreateSamples());

        Assert.Equal(recordingId, reader.RecordingId);
    }

    [Fact]
    public void Constructor_ReportsSampleCount()
    {
        var reader = new IndexedTelemetryReader(
            Guid.NewGuid(),
            CreateSamples());

        Assert.Equal(3, reader.SampleCount);
    }

    [Fact]
    public void Read_ReturnsCorrectSample()
    {
        var reader = new IndexedTelemetryReader(
            Guid.NewGuid(),
            CreateSamples());

        var sample = reader.Read(1);

        Assert.Equal(1, sample.SequenceNumber);
        Assert.Equal(
            new TelemetryTimestamp(200),
            sample.Timestamp);
    }

    [Fact]
    public void Read_ReturnsSameSampleInstance()
    {
        var reader = new IndexedTelemetryReader(
            Guid.NewGuid(),
            CreateSamples());

        var first = reader.Read(1);
        var second = reader.Read(1);

        Assert.Same(first, second);
    }

    [Fact]
    public void Read_PreservesIndexedChannelValues()
    {
        var channelId = Guid.NewGuid();
        var index = new ChannelIndex([channelId]);

        var samples = new[]
        {
            new TelemetrySample(
                0,
                new TelemetryTimestamp(100),
                index,
                new double?[] { 8500 }),

            new TelemetrySample(
                1,
                new TelemetryTimestamp(200),
                index,
                new double?[] { null })
        };

        var reader = new IndexedTelemetryReader(
            Guid.NewGuid(),
            samples);

        Assert.Equal(8500.0, reader.Read(0).GetValue(channelId));
        Assert.Null(reader.Read(1).GetValue(channelId));
        Assert.True(reader.Read(1).HasChannel(channelId));
    }

    [Fact]
    public void Read_NegativeSequence_Throws()
    {
        var reader = new IndexedTelemetryReader(
            Guid.NewGuid(),
            CreateSamples());

        Assert.Throws<ArgumentOutOfRangeException>(
            () => reader.Read(-1));
    }

    [Fact]
    public void Read_SequenceBeyondEnd_Throws()
    {
        var reader = new IndexedTelemetryReader(
            Guid.NewGuid(),
            CreateSamples());

        Assert.Throws<ArgumentOutOfRangeException>(
            () => reader.Read(3));
    }

    [Fact]
    public void Constructor_EmptyRecordingId_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new IndexedTelemetryReader(
                Guid.Empty,
                CreateSamples()));
    }

    [Fact]
    public void Constructor_NullSamples_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new IndexedTelemetryReader(
                Guid.NewGuid(),
                null!));
    }

    [Fact]
    public void Constructor_NonContiguousSequences_Throws()
    {
        var index = new ChannelIndex([Guid.NewGuid()]);

        var samples = new[]
        {
            new TelemetrySample(
                0,
                new TelemetryTimestamp(100),
                index,
                new double?[] { 10 }),

            new TelemetrySample(
                2,
                new TelemetryTimestamp(200),
                index,
                new double?[] { 20 })
        };

        Assert.Throws<ArgumentException>(() =>
            new IndexedTelemetryReader(
                Guid.NewGuid(),
                samples));
    }

    [Fact]
    public void Constructor_PreservesDuplicateTimestamps()
    {
        var reader = new IndexedTelemetryReader(
            Guid.NewGuid(),
            CreateSamples());

        Assert.Equal(
            reader.Read(1).Timestamp,
            reader.Read(2).Timestamp);

        Assert.Equal(1, reader.Read(1).SequenceNumber);
        Assert.Equal(2, reader.Read(2).SequenceNumber);
    }

    [Fact]
    public void Constructor_PreservesSourceOrder()
    {
        var channelId = Guid.NewGuid();
        var index = new ChannelIndex([channelId]);

        var samples = new[]
        {
            new TelemetrySample(
                0,
                new TelemetryTimestamp(300),
                index,
                new double?[] { 30 }),

            new TelemetrySample(
                1,
                new TelemetryTimestamp(100),
                index,
                new double?[] { 10 }),

            new TelemetrySample(
                2,
                new TelemetryTimestamp(200),
                index,
                new double?[] { 20 })
        };

        var reader = new IndexedTelemetryReader(
            Guid.NewGuid(),
            samples);

        Assert.Equal(
            new TelemetryTimestamp(300),
            reader.Read(0).Timestamp);

        Assert.Equal(
            new TelemetryTimestamp(100),
            reader.Read(1).Timestamp);

        Assert.Equal(
            new TelemetryTimestamp(200),
            reader.Read(2).Timestamp);
    }

    [Fact]
    public void Constructor_SnapshotsInputCollection()
    {
        var samples = CreateSamples().ToList();

        var reader = new IndexedTelemetryReader(
            Guid.NewGuid(),
            samples);

        samples.Clear();

        Assert.Equal(3, reader.SampleCount);
        Assert.Equal(2, reader.Read(2).SequenceNumber);
    }

    [Fact]
    public void Constructor_EmptySamples_IsAllowed()
    {
        var reader = new IndexedTelemetryReader(
            Guid.NewGuid(),
            Array.Empty<TelemetrySample>());

        Assert.Equal(0, reader.SampleCount);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => reader.Read(0));
    }

    private static TelemetrySample[] CreateSamples()
    {
        var channelId = Guid.NewGuid();
        var index = new ChannelIndex([channelId]);

        return
        [
            new TelemetrySample(
                0,
                new TelemetryTimestamp(100),
                index,
                new double?[] { 10 }),

            new TelemetrySample(
                1,
                new TelemetryTimestamp(200),
                index,
                new double?[] { 20 }),

            new TelemetrySample(
                2,
                new TelemetryTimestamp(200),
                index,
                new double?[] { 30 })
        ];
    }
}
