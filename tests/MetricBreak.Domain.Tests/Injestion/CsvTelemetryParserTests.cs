
using MetricBreak.Ingestion.Csv;

namespace MetricBreak.Domain.Tests.Ingestion.Csv;

public sealed class CsvTelemetryParserTests
{
    private readonly CsvTelemetryParser _parser = new();

    [Fact]
    public void Parse_ValidCsv_CreatesChannelsAndSamples()
    {
        const string csv = """
            Timestamp,Pressure,Rate
            2026-11-08T10:00:00Z,8500,72
            2026-11-08T10:00:01Z,8800,70
            2026-11-08T10:00:02Z,9100,68
            """;

        var result = Parse(csv);

        Assert.Equal(2, result.Channels.Count);
        Assert.Equal(3, result.Samples.Count);

        Assert.Equal("Pressure", result.Channels[0].Name);
        Assert.Equal("Rate", result.Channels[1].Name);

        var pressureId = result.Channels[0].Id;
        var rateId = result.Channels[1].Id;

        Assert.Equal(8500d, result.Samples[0].GetValue(pressureId));
        Assert.Equal(72d, result.Samples[0].GetValue(rateId));

        Assert.Equal(9100d, result.Samples[2].GetValue(pressureId));
        Assert.Equal(68d, result.Samples[2].GetValue(rateId));
    }

    [Fact]
    public void Parse_AssignsZeroBasedSequenceNumbers()
    {
        const string csv = """
            Timestamp,Pressure
            2026-11-08T10:00:00Z,100
            2026-11-08T10:00:01Z,200
            2026-11-08T10:00:02Z,300
            """;

        var result = Parse(csv);

        Assert.Equal(
            new long[] { 0, 1, 2 },
            result.Samples.Select(s => s.SequenceNumber));
    }

    [Fact]
    public void Parse_DuplicateTimestamps_PreservesBothSamples()
    {
        const string csv = """
            Timestamp,Pressure
            2026-11-08T10:00:00Z,100
            2026-11-08T10:00:00Z,200
            """;

        var result = Parse(csv);

        Assert.Equal(2, result.Samples.Count);

        Assert.Equal(
            result.Samples[0].Timestamp,
            result.Samples[1].Timestamp);

        Assert.Equal(0, result.Samples[0].SequenceNumber);
        Assert.Equal(1, result.Samples[1].SequenceNumber);

        var channelId = result.Channels[0].Id;

        Assert.Equal(100d, result.Samples[0].GetValue(channelId));
        Assert.Equal(200d, result.Samples[1].GetValue(channelId));
    }

    [Fact]
    public void Parse_OutOfOrderTimestamps_PreservesSourceOrder()
    {
        const string csv = """
            Timestamp,Pressure
            2026-11-08T10:00:02Z,300
            2026-11-08T10:00:00Z,100
            2026-11-08T10:00:01Z,200
            """;

        var result = Parse(csv);

        var channelId = result.Channels[0].Id;

        Assert.Equal(
            new double?[] { 300, 100, 200 },
            result.Samples.Select(s => s.GetValue(channelId)));

        Assert.True(
            result.Samples[0].Timestamp.CompareTo(
                result.Samples[1].Timestamp) > 0);
    }

    [Fact]
    public void Parse_EmptyMeasurement_ProducesNull()
    {
        const string csv = """
            Timestamp,Pressure,Rate
            2026-11-08T10:00:00Z,,72
            2026-11-08T10:00:01Z,8500,
            """;

        var result = Parse(csv);

        var pressureId = result.Channels[0].Id;
        var rateId = result.Channels[1].Id;

        Assert.Null(result.Samples[0].GetValue(pressureId));
        Assert.Equal(72d, result.Samples[0].GetValue(rateId));

        Assert.Equal(8500d, result.Samples[1].GetValue(pressureId));
        Assert.Null(result.Samples[1].GetValue(rateId));
    }

    [Fact]
    public void Parse_QuotedChannelNames_HandlesCommas()
    {
        const string csv = """
            Timestamp,"Pressure, Surface",Rate
            2026-11-08T10:00:00Z,8500,72
            """;

        var result = Parse(csv);

        Assert.Equal("Pressure, Surface", result.Channels[0].Name);
        Assert.Equal("Rate", result.Channels[1].Name);
    }


    [Fact]
    public void Parse_EscapedQuotes_ParsesChannelName()
    {
        const string csv =
            "Timestamp,\"Pressure \"\"Main\"\"\",Rate\n" +
            "2026-11-08T10:00:00Z,8500,72";

        var result = Parse(csv);

        Assert.Equal("Pressure \"Main\"", result.Channels[0].Name);
        Assert.Equal("Rate", result.Channels[1].Name);
    }


    [Fact]
    public void Parse_ValidFractionalTimestamp_PreservesPrecision()
    {
        const string csv = """
            Timestamp,Pressure
            2026-11-08T10:00:00.1234567Z,8500
            """;

        var result = Parse(csv);

        var expected = new DateTimeOffset(
            2026, 11, 8, 10, 0, 0, TimeSpan.Zero)
            .AddTicks(1234567);

        Assert.Equal(
            expected,
            result.Samples[0].Timestamp.ToDateTimeOffset());
    }

    [Fact]
    public void Parse_EmptyCsv_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => Parse(""));
    }

    [Fact]
    public void Parse_HeaderWithoutChannels_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() =>
            Parse("Timestamp\n"));
    }

    [Fact]
    public void Parse_InvalidFirstColumn_ThrowsFormatException()
    {
        const string csv = """
            Time,Pressure
            2026-11-08T10:00:00Z,8500
            """;

        Assert.Throws<FormatException>(() => Parse(csv));
    }

    [Fact]
    public void Parse_DuplicateChannelNames_ThrowsFormatException()
    {
        const string csv = """
            Timestamp,Pressure,Pressure
            2026-11-08T10:00:00Z,8500,9000
            """;

        Assert.Throws<FormatException>(() => Parse(csv));
    }

    [Fact]
    public void Parse_EmptyChannelName_ThrowsFormatException()
    {
        const string csv = """
            Timestamp,,Rate
            2026-11-08T10:00:00Z,8500,72
            """;

        Assert.Throws<FormatException>(() => Parse(csv));
    }

    [Fact]
    public void Parse_HeaderOnly_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() =>
            Parse("Timestamp,Pressure\n"));
    }

    [Fact]
    public void Parse_InvalidTimestamp_ThrowsFormatException()
    {
        const string csv = """
            Timestamp,Pressure
            invalid-timestamp,8500
            """;

        Assert.Throws<FormatException>(() => Parse(csv));
    }

    [Fact]
    public void Parse_NonUtcTimestamp_ThrowsFormatException()
    {
        const string csv = """
            Timestamp,Pressure
            2026-11-08T10:00:00-05:00,8500
            """;

        Assert.Throws<FormatException>(() => Parse(csv));
    }

    [Fact]
    public void Parse_NanosecondTimestamp_RejectsUnsupportedPrecision()
    {
        const string csv = """
            Timestamp,Pressure
            2026-11-08T10:00:00.123456789Z,8500
            """;

        Assert.Throws<FormatException>(() => Parse(csv));
    }

    [Fact]
    public void Parse_InvalidNumericValue_ThrowsFormatException()
    {
        const string csv = """
            Timestamp,Pressure
            2026-11-08T10:00:00Z,invalid
            """;

        Assert.Throws<FormatException>(() => Parse(csv));
    }

    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("-Infinity")]
    [InlineData("1e999")]
    public void Parse_NonFiniteNumericValue_ThrowsFormatException(
        string value)
    {
        var csv =
            "Timestamp,Pressure\n" +
            $"2026-11-08T10:00:00Z,{value}\n";

        Assert.Throws<FormatException>(() => Parse(csv));
    }

    [Fact]
    public void Parse_InconsistentColumnCount_ThrowsFormatException()
    {
        const string csv = """
            Timestamp,Pressure,Rate
            2026-11-08T10:00:00Z,8500
            """;

        Assert.Throws<FormatException>(() => Parse(csv));
    }

    [Fact]
    public void Parse_ExtraColumn_ThrowsFormatException()
    {
        const string csv = """
            Timestamp,Pressure
            2026-11-08T10:00:00Z,8500,72
            """;

        Assert.Throws<FormatException>(() => Parse(csv));
    }

    [Fact]
    public void Parse_UnterminatedQuotedField_ThrowsFormatException()
    {
        const string csv = """
            Timestamp,"Pressure
            2026-11-08T10:00:00Z,8500
            """;

        Assert.Throws<FormatException>(() => Parse(csv));
    }

    [Fact]
    public void Parse_UnexpectedQuote_ThrowsFormatException()
    {
        const string csv = """
            Timestamp,Pres"sure
            2026-11-08T10:00:00Z,8500
            """;

        Assert.Throws<FormatException>(() => Parse(csv));
    }

    [Fact]
    public void Parse_UnexpectedCharacterAfterClosingQuote_ThrowsFormatException()
    {
        const string csv = """
            Timestamp,"Pressure"x
            2026-11-08T10:00:00Z,8500
            """;

        Assert.Throws<FormatException>(() => Parse(csv));
    }

    [Fact]
    public void Parse_NullReader_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            _parser.Parse(null!));
    }

    private CsvTelemetryResult Parse(string csv)
    {
        using var reader = new StringReader(csv);
        return _parser.Parse(reader);
    }
}
