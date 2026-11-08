
using MetricBreak.Domain.Recordings;
using MetricBreak.Ingestion.Csv;

namespace MetricBreak.Domain.Tests.Ingestion.Csv;

public sealed class CsvTelemetryReaderTests
{
    private readonly CsvTelemetryParser _parser = new();

    [Fact]
    public void Constructor_ValidResult_ExposesRecordingIdentityAndCount()
    {
        var recordingId = Guid.NewGuid();
        var result = ParseValidCsv();

        var reader = new CsvTelemetryReader(recordingId, result);

        Assert.Equal(recordingId, reader.RecordingId);
        Assert.Equal(3, reader.SampleCount);
    }

    [Fact]
    public void Read_ValidSequenceNumber_ReturnsExpectedSample()
    {
        var result = ParseValidCsv();
        var reader = new CsvTelemetryReader(Guid.NewGuid(), result);

        var pressureId = result.Channels[0].Id;

        var sample = reader.Read(1);

        Assert.Equal(1, sample.SequenceNumber);
        Assert.Equal(8800d, sample.GetValue(pressureId));
    }

    [Fact]
    public void Read_DuplicateTimestamps_PreservesSeparateSamples()
    {
        const string csv = """
            Timestamp,Pressure
            2026-11-08T10:00:00Z,100
            2026-11-08T10:00:00Z,200
            """;

        var result = Parse(csv);
        var reader = new CsvTelemetryReader(Guid.NewGuid(), result);

        var first = reader.Read(0);
        var second = reader.Read(1);

        Assert.Equal(first.Timestamp, second.Timestamp);
        Assert.Equal(0, first.SequenceNumber);
        Assert.Equal(1, second.SequenceNumber);
    }

    [Fact]
    public void Read_OutOfOrderTimestamps_PreservesSourceOrder()
    {
        const string csv = """
            Timestamp,Pressure
            2026-11-08T10:00:02Z,300
            2026-11-08T10:00:00Z,100
            """;

        var result = Parse(csv);
        var reader = new CsvTelemetryReader(Guid.NewGuid(), result);

        Assert.True(
            reader.Read(0).Timestamp.CompareTo(
                reader.Read(1).Timestamp) > 0);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    [InlineData(long.MaxValue)]
    public void Read_InvalidSequenceNumber_Throws(long sequenceNumber)
    {
        var reader = new CsvTelemetryReader(
            Guid.NewGuid(),
            ParseValidCsv());

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            reader.Read(sequenceNumber));
    }

    [Fact]
    public void Constructor_EmptyRecordingId_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new CsvTelemetryReader(Guid.Empty, ParseValidCsv()));
    }

    [Fact]
    public void Constructor_NullResult_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new CsvTelemetryReader(Guid.NewGuid(), null!));
    }

    [Fact]
    public void Constructor_EmptySamples_Throws()
    {
        var result = new CsvTelemetryResult(
            Array.Empty<ChannelDefinition>(),
            Array.Empty<TelemetrySample>());

        Assert.Throws<ArgumentException>(() =>
            new CsvTelemetryReader(Guid.NewGuid(), result));
    }

    [Fact]
    public void Constructor_NonContiguousSequences_Throws()
    {
        var result = ParseValidCsv();

        var samples = new[]
        {
            result.Samples[0],
            result.Samples[2]
        };

        var invalid = new CsvTelemetryResult(
            result.Channels,
            samples);

        Assert.Throws<ArgumentException>(() =>
            new CsvTelemetryReader(Guid.NewGuid(), invalid));
    }

    private CsvTelemetryResult ParseValidCsv()
    {
        const string csv = """
            Timestamp,Pressure,Rate
            2026-11-08T10:00:00Z,8500,72
            2026-11-08T10:00:01Z,8800,70
            2026-11-08T10:00:02Z,9100,68
            """;

        return Parse(csv);
    }

    private CsvTelemetryResult Parse(string csv)
    {
        using var textReader = new StringReader(csv);
        return _parser.Parse(textReader);
    }
}
