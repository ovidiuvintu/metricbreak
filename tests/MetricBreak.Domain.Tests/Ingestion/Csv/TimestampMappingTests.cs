using System.Globalization;
using MetricBreak.Domain.Recordings;
using MetricBreak.Ingestion.Csv;

namespace MetricBreak.Domain.Tests.Ingestion.Csv;

public sealed class TimestampMappingTests
{
    [Theory]
    [InlineData("2026-11-08T10:00:00Z")]
    [InlineData("2026-11-08T10:00:00+00:00")]
    [InlineData("2026-11-08T10:00:00+0000")]
    [InlineData("2026-11-08t10:00:00Z")]
    [InlineData("2026-11-08 10:00:00Z")]
    public void Iso8601_UtcForms_MatchTheDomainInstant(string text)
    {
        var timestamp = TimestampParser.Parse(text, TimestampFormat.Iso8601, 2);
        var expected = TelemetryTimestamp.FromDateTimeOffset(
            new DateTimeOffset(2026, 11, 8, 10, 0, 0, TimeSpan.Zero));

        Assert.Equal(expected, timestamp);
    }

    [Fact]
    public void Iso8601_ExplicitOffset_NormalizesToUtc()
    {
        var timestamp = TimestampParser.Parse(
            "2026-11-08T10:00:00-06:00",
            TimestampFormat.Iso8601,
            2);

        var expected = TelemetryTimestamp.FromDateTimeOffset(
            new DateTimeOffset(2026, 11, 8, 16, 0, 0, TimeSpan.Zero));

        Assert.Equal(expected, timestamp);
        Assert.Equal(
            "2026-11-08T16:00:00Z",
            TimestampFormatter.Format(timestamp, TimestampFormat.Iso8601));
    }

    [Fact]
    public void Iso8601_FractionalMinutesOffset_NormalizesToUtc()
    {
        var timestamp = TimestampParser.Parse(
            "2026-11-08T10:00:00.5+05:30",
            TimestampFormat.Iso8601,
            2);

        var expected = TelemetryTimestamp.FromDateTimeOffset(
            new DateTimeOffset(2026, 11, 8, 4, 30, 0, TimeSpan.Zero));
        expected = new TelemetryTimestamp(expected.UnixNanoseconds + 500_000_000L);

        Assert.Equal(expected, timestamp);
    }

    [Fact]
    public void Iso8601_SevenFractionalDigits_MatchDateTimeOffsetTicks()
    {
        const string csv = """
            Timestamp,Pressure
            2026-11-08T10:00:00.1234567Z,8500
            """;

        var indexed = new IndexedCsvTelemetryParser().Parse(new StringReader(csv));
        var original = new CsvTelemetryParser().Parse(new StringReader(csv));

        Assert.Equal(original.Samples[0].Timestamp, indexed.Samples[0].Timestamp);
        Assert.Equal(TimestampOrigin.Unix, indexed.TimestampOrigin);
    }

    [Fact]
    public void Iso8601_NineFractionalDigits_PreserveNanoseconds()
    {
        var timestamp = TimestampParser.Parse(
            "2026-11-08T10:00:00.123456789Z",
            TimestampFormat.Iso8601,
            2);

        var whole = TelemetryTimestamp.FromDateTimeOffset(
            new DateTimeOffset(2026, 11, 8, 10, 0, 0, TimeSpan.Zero));

        Assert.Equal(whole.UnixNanoseconds + 123_456_789L, timestamp.UnixNanoseconds);
        Assert.Equal(
            "2026-11-08T10:00:00.123456789Z",
            TimestampFormatter.Format(timestamp, TimestampFormat.Iso8601));
    }

    [Fact]
    public void Iso8601_ExtraFractionalDigits_AreOutOfRange()
    {
        var error = Assert.Throws<FormatException>(() =>
            TimestampParser.Parse(
                "2026-11-08T10:00:00.1234567891Z",
                TimestampFormat.Iso8601,
                4));

        Assert.Contains("out of range", error.Message, StringComparison.Ordinal);
        Assert.Contains("record 4", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("2026-11-08T10:00:00")]
    [InlineData("2026-11-08")]
    [InlineData("not-a-time")]
    public void Iso8601_MalformedValues_AreRejected(string text)
    {
        var error = Assert.Throws<FormatException>(() =>
            TimestampParser.Parse(text, TimestampFormat.Iso8601, 2));

        Assert.Contains("Malformed", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UnixEpochSeconds_PreserveFractionalNanoseconds()
    {
        var timestamp = TimestampParser.Parse(
            "1700000000.123456789",
            TimestampFormat.UnixEpochSeconds,
            2);

        Assert.Equal(1_700_000_000_123_456_789L, timestamp.UnixNanoseconds);
    }

    [Fact]
    public void UnixEpochSeconds_Boundary_Is2001()
    {
        var timestamp = TimestampParser.Parse(
            "1000000000",
            TimestampFormat.UnixEpochSeconds,
            2);

        Assert.Equal(
            TelemetryTimestamp.FromDateTimeOffset(
                DateTimeOffset.Parse(
                    "2001-09-09T01:46:40Z",
                    CultureInfo.InvariantCulture)),
            timestamp);
    }

    [Fact]
    public void UnixEpochSeconds_ZeroAndNegative_AreAbsolute()
    {
        Assert.Equal(
            0L,
            TimestampParser.Parse("0", TimestampFormat.UnixEpochSeconds, 2).UnixNanoseconds);
        Assert.Equal(
            -1_500_000_000L,
            TimestampParser.Parse("-1.5", TimestampFormat.UnixEpochSeconds, 2).UnixNanoseconds);
        Assert.Equal(
            "1969-12-31T23:59:58.5Z",
            TimestampFormatter.Format(
                TimestampParser.Parse("-1.5", TimestampFormat.UnixEpochSeconds, 2),
                TimestampFormat.UnixEpochSeconds));
    }

    [Fact]
    public void UnixEpochMilliseconds_ConvertExactly()
    {
        var timestamp = TimestampParser.Parse(
            "1700000000123.5",
            TimestampFormat.UnixEpochMilliseconds,
            2);

        Assert.Equal(1_700_000_000_123_500_000L, timestamp.UnixNanoseconds);
    }

    [Fact]
    public void UnixEpochMilliseconds_Boundary_MatchesEpochSeconds()
    {
        var milliseconds = TimestampParser.Parse(
            "1000000000000",
            TimestampFormat.UnixEpochMilliseconds,
            2);
        var seconds = TimestampParser.Parse(
            "1000000000",
            TimestampFormat.UnixEpochSeconds,
            2);

        Assert.Equal(seconds, milliseconds);
    }

    [Fact]
    public void UnixEpochMicroseconds_ConvertExactly()
    {
        var timestamp = TimestampParser.Parse(
            "1700000000000000",
            TimestampFormat.UnixEpochMicroseconds,
            2);

        Assert.Equal(1_700_000_000_000_000_000L, timestamp.UnixNanoseconds);
    }

    [Fact]
    public void UnixEpochMicroseconds_Boundary_MatchesEpochSeconds()
    {
        var microseconds = TimestampParser.Parse(
            "1000000000000000",
            TimestampFormat.UnixEpochMicroseconds,
            2);
        var seconds = TimestampParser.Parse(
            "1000000000",
            TimestampFormat.UnixEpochSeconds,
            2);

        Assert.Equal(seconds, microseconds);
    }

    [Fact]
    public void UnixEpochNanoseconds_AreStoredDirectly()
    {
        var timestamp = TimestampParser.Parse(
            "1700000000000000000",
            TimestampFormat.UnixEpochNanoseconds,
            2);

        Assert.Equal(1_700_000_000_000_000_000L, timestamp.UnixNanoseconds);
    }

    [Fact]
    public void UnixEpochNanoseconds_Boundary_MatchesEpochSeconds()
    {
        var nanoseconds = TimestampParser.Parse(
            "1000000000000000000",
            TimestampFormat.UnixEpochNanoseconds,
            2);
        var seconds = TimestampParser.Parse(
            "1000000000",
            TimestampFormat.UnixEpochSeconds,
            2);

        Assert.Equal(seconds, nanoseconds);
    }

    [Fact]
    public void UnixEpochNanoseconds_RejectFractionalPrecision()
    {
        var error = Assert.Throws<FormatException>(() =>
            TimestampParser.Parse(
                "1700000000000000000.1",
                TimestampFormat.UnixEpochNanoseconds,
                2));

        Assert.Contains("out of range", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UnixEpochNanoseconds_AreDetectedForModernInstants()
    {
        const string csv = """
            time_ns,Pressure
            1700000000000000000,10
            1700000000000000001,11
            """;

        var result = new IndexedCsvTelemetryParser().Parse(new StringReader(csv));

        Assert.Equal(TimestampOrigin.Unix, result.TimestampOrigin);
        Assert.Equal(1_700_000_000_000_000_000L, result.Samples[0].Timestamp.UnixNanoseconds);
        Assert.Equal(1_700_000_000_000_000_001L, result.Samples[1].Timestamp.UnixNanoseconds);
        Assert.Equal(["Pressure"], result.Channels.Select(channel => channel.Name));
    }

    [Fact]
    public void NumericScale_ThatOverflowsNanoseconds_IsOutOfRange()
    {
        var error = Assert.Throws<FormatException>(() =>
            TimestampParser.Parse(
                "100000000000",
                TimestampFormat.UnixEpochSeconds,
                3));

        Assert.Contains("out of range", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RelativeElapsedSeconds_StayAtZero_AndDoNotInventADate()
    {
        var timestamp = TimestampParser.Parse(
            "1.5",
            TimestampFormat.RelativeElapsedSeconds,
            2);

        Assert.Equal(1_500_000_000L, timestamp.UnixNanoseconds);
        Assert.Equal(
            "1.5 s",
            TimestampFormatter.Format(timestamp, TimestampFormat.RelativeElapsedSeconds));
        Assert.DoesNotContain(
            "1970",
            TimestampFormatter.Format(timestamp, TimestampFormat.RelativeElapsedSeconds),
            StringComparison.Ordinal);
    }

    [Fact]
    public void RelativeElapsedSeconds_RejectNegativeValues()
    {
        var error = Assert.Throws<FormatException>(() =>
            TimestampParser.Parse("-0.1", TimestampFormat.RelativeElapsedSeconds, 2));

        Assert.Contains("out of range", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingTimestamp_NamesTheRecord()
    {
        var error = Assert.Throws<FormatException>(() =>
            TimestampParser.Parse("  ", TimestampFormat.Iso8601, 8));

        Assert.Contains("Missing timestamp at record 8", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ClassicIsoImport_MatchesTheLegacyParser_AndKeepsOrder()
    {
        const string csv = """
            Timestamp,Pressure,Rate
            2026-11-08T10:00:02Z,300,1
            2026-11-08T10:00:00Z,100,2
            2026-11-08T10:00:00.5Z,100,3
            """;

        var indexed = new IndexedCsvTelemetryParser().Parse(new StringReader(csv));
        var original = new CsvTelemetryParser().Parse(new StringReader(csv));

        Assert.Equal(
            original.Samples.Select(sample => sample.Timestamp),
            indexed.Samples.Select(sample => sample.Timestamp));
        Assert.Equal(new long[] { 0, 1, 2 }, indexed.Samples.Select(sample => sample.SequenceNumber));
        Assert.Equal(new[] { "Pressure", "Rate" }, indexed.Channels.Select(channel => channel.Name));
        Assert.Equal(TimestampOrigin.Unix, indexed.TimestampOrigin);
    }

    [Fact]
    public void EpochSeconds_NotInTheFirstColumn_PreserveDuplicatesAndSequence()
    {
        const string csv = """
            Pressure,time_s
            10,1700000000
            11,1700000000
            12,1700000001
            """;

        var result = new IndexedCsvTelemetryParser().Parse(new StringReader(csv));

        Assert.Equal(["Pressure"], result.Channels.Select(channel => channel.Name));
        Assert.Equal(0, result.Samples[0].SequenceNumber);
        Assert.Equal(1, result.Samples[1].SequenceNumber);
        Assert.Equal(result.Samples[0].Timestamp, result.Samples[1].Timestamp);
        Assert.Equal(1_700_000_000_000_000_000L, result.Samples[0].Timestamp.UnixNanoseconds);
        Assert.Equal(10d, result.Samples[0].GetValue(result.Channels[0].Id));
        Assert.Equal(11d, result.Samples[1].GetValue(result.Channels[0].Id));
    }

    [Fact]
    public void RelativeColumn_ImportsAsElapsedNanoseconds()
    {
        const string csv = """
            elapsed,Pressure
            0,10
            0.25,11
            1.5,12
            """;

        var result = new IndexedCsvTelemetryParser().Parse(new StringReader(csv));

        Assert.Equal(TimestampOrigin.Elapsed, result.TimestampOrigin);
        Assert.Equal(0L, result.Samples[0].Timestamp.UnixNanoseconds);
        Assert.Equal(250_000_000L, result.Samples[1].Timestamp.UnixNanoseconds);
        Assert.Equal(1_500_000_000L, result.Samples[2].Timestamp.UnixNanoseconds);
    }

    [Fact]
    public void SmallNumericValues_AreAmbiguousUntilTheUserChooses()
    {
        const string csv = """
            time,Pressure
            0,10
            1,11
            2,12
            """;

        var detection = TimestampColumnDetector.Detect(
            ["time", "Pressure"],
            [
                new[] { "0", "10" },
                new[] { "1", "11" },
                new[] { "2", "12" }
            ]);

        Assert.True(detection.Ambiguous);
        Assert.Equal(0, detection.ColumnIndex);
        Assert.Equal(TimestampFormat.RelativeElapsedSeconds, detection.Format);

        var error = Assert.Throws<FormatException>(() =>
            new IndexedCsvTelemetryParser().Parse(new StringReader(csv)));
        Assert.Contains("ambiguous", error.Message, StringComparison.OrdinalIgnoreCase);

        var chosen = new IndexedCsvTelemetryParser().Parse(
            new StringReader(csv),
            new TimestampMapping(0, TimestampFormat.UnixEpochSeconds));

        Assert.Equal(TimestampOrigin.Unix, chosen.TimestampOrigin);
        Assert.Equal(0L, chosen.Samples[0].Timestamp.UnixNanoseconds);
        Assert.Equal(1_000_000_000L, chosen.Samples[1].Timestamp.UnixNanoseconds);
        Assert.Equal(2_000_000_000L, chosen.Samples[2].Timestamp.UnixNanoseconds);
    }

    [Fact]
    public void HeaderNamedEpoch_SelectsEpochSeconds_AtTheEpochBoundary()
    {
        var detection = TimestampColumnDetector.Detect(
            ["epoch", "Pressure"],
            [
                new[] { "0", "1" },
                new[] { "1", "2" }
            ]);

        Assert.False(detection.Ambiguous);
        Assert.Equal(TimestampFormat.UnixEpochSeconds, detection.Format);
        Assert.Equal(TimestampOrigin.Unix, detection.Origin);
    }

    [Fact]
    public void TwoIsoColumns_AreAmbiguous_AndKeepTheFirstSuggestion()
    {
        var detection = TimestampColumnDetector.Detect(
            ["timestamp", "loggedAt", "Pressure"],
            [
                new[] { "2026-11-08T10:00:00Z", "2026-11-08T16:00:00Z", "1" }
            ]);

        Assert.True(detection.Ambiguous);
        Assert.Equal(0, detection.ColumnIndex);
        Assert.Equal(TimestampFormat.Iso8601, detection.Format);
    }

    [Fact]
    public void InconsistentTimestampColumn_IsRejected()
    {
        const string csv = """
            Timestamp,Pressure
            2026-11-08T10:00:00Z,1
            1700000000,2
            """;

        var error = Assert.Throws<FormatException>(() =>
            new IndexedCsvTelemetryParser().Parse(new StringReader(csv)));

        Assert.Contains("do not share one format", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingTimestampCell_IsRejected()
    {
        const string csv = """
            Timestamp,Pressure
            ,1
            """;

        var error = Assert.Throws<FormatException>(() =>
            new IndexedCsvTelemetryParser().Parse(new StringReader(csv)));

        Assert.Contains("empty", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MalformedTimestamp_IsRejected()
    {
        const string csv = """
            Timestamp,Pressure
            not-a-time,1
            """;

        var error = Assert.Throws<FormatException>(() =>
            new IndexedCsvTelemetryParser().Parse(new StringReader(csv)));

        Assert.Contains("not a recognized timestamp", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LaterMalformedRow_IsRejectedAfterDetection()
    {
        var lines = new List<string> { "Timestamp,Pressure" };
        for (var i = 0; i < 40; i++)
        {
            lines.Add($"2026-11-08T10:00:{i % 60:00}Z,{i}");
        }

        lines.Add("bad,41");

        var error = Assert.Throws<FormatException>(() =>
            new IndexedCsvTelemetryParser().Parse(new StringReader(string.Join('\n', lines))));

        Assert.Contains("Malformed", error.Message, StringComparison.Ordinal);
        Assert.Contains("record 42", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Preview_ShowsNormalizedValues_ForAnExplicitOverride()
    {
        const string csv = """
            time,Pressure
            0,10
            1.5,11
            """;

        var inspection = CsvTimestampInspector.Inspect(
            new StringReader(csv),
            new TimestampMapping(0, TimestampFormat.UnixEpochSeconds));

        Assert.Equal(TimestampFormat.UnixEpochSeconds, inspection.Applied.Format);
        Assert.Equal("0", inspection.Preview[0].Raw);
        Assert.Equal("1970-01-01T00:00:00Z", inspection.Preview[0].Normalized);
        Assert.Equal("1970-01-01T00:00:01.5Z", inspection.Preview[1].Normalized);
    }
}
