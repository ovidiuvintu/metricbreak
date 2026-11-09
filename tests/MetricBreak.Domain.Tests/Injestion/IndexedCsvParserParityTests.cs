
using MetricBreak.Ingestion.Csv;

namespace MetricBreak.Domain.Tests.Ingestion;

public sealed class IndexedCsvParserParityTests
{
    [Theory]
    [MemberData(nameof(ValidCsvCases))]
    public void Parsers_ProduceEquivalentTelemetry(string csv)
    {
        var original = new CsvTelemetryParser()
            .Parse(new StringReader(csv));

        var indexed = new IndexedCsvTelemetryParser()
            .Parse(new StringReader(csv));

        Assert.Equal(
            original.Channels.Count,
            indexed.Channels.Count);

        Assert.Equal(
            original.Samples.Count,
            indexed.Samples.Count);

        // Channel IDs are generated independently.
        // Compare channel definitions by position and metadata.
        for (var channel = 0; channel < original.Channels.Count; channel++)
        {
            Assert.Equal(
                original.Channels[channel].Name,
                indexed.Channels[channel].Name);

            Assert.Equal(
                original.Channels[channel].Unit,
                indexed.Channels[channel].Unit);
        }

        for (var sequence = 0; sequence < original.Samples.Count; sequence++)
        {
            var expected = original.Samples[sequence];
            var actual = indexed.Samples[sequence];

            Assert.Equal(
                expected.SequenceNumber,
                actual.SequenceNumber);

            Assert.Equal(
                expected.Timestamp,
                actual.Timestamp);

            for (var channel = 0; channel < original.Channels.Count; channel++)
            {
                var expectedChannelId = original.Channels[channel].Id;
                var actualChannelId = indexed.Channels[channel].Id;

                Assert.Equal(
                    expected.GetValue(expectedChannelId),
                    actual.GetValue(actualChannelId));
            }
        }
    }

    [Theory]
    [MemberData(nameof(InvalidCsvCases))]
    public void Parsers_RejectSameInvalidInput(string csv)
    {
        Assert.Throws<FormatException>(() =>
            new CsvTelemetryParser().Parse(new StringReader(csv)));

        Assert.Throws<FormatException>(() =>
            new IndexedCsvTelemetryParser().Parse(new StringReader(csv)));
    }

    public static TheoryData<string> ValidCsvCases => new()
    {
        // Normal telemetry
        {
            """
            Timestamp,Pressure,Rate
            2026-11-08T10:00:00Z,8500,42.5
            2026-11-08T10:00:01Z,8700,43.2
            2026-11-08T10:00:02Z,9500,44.0
            """
        },

        // Missing values
        {
            """
            Timestamp,Pressure,Rate
            2026-11-08T10:00:00Z,,42.5
            2026-11-08T10:00:01Z,8700,
            2026-11-08T10:00:02Z,,
            """
        },

        // Duplicate timestamps and out-of-order timestamps
        {
            """
            Timestamp,Pressure
            2026-11-08T10:00:02Z,8500
            2026-11-08T10:00:02Z,8700
            2026-11-08T10:00:01Z,9500
            """
        },

        // Quoted channel names and escaped quotes
        {
            "Timestamp,\"Treating, Pressure\",\"Rate \"\"Measured\"\"\"\n" +
            "2026-11-08T10:00:00Z,8500,42.5\n"
        },

        // Embedded newline in a quoted channel name
        {
            "Timestamp,\"Treating\nPressure\",Rate\n" +
            "2026-11-08T10:00:00Z,8500,42.5\n"
        },

        // CRLF record endings
        {
            "Timestamp,Pressure,Rate\r\n" +
            "2026-11-08T10:00:00Z,8500,42.5\r\n" +
            "2026-11-08T10:00:01Z,8700,43.2\r\n"
        },

        // UTC timestamp with fractional seconds
        {
            """
            Timestamp,Pressure
            2026-11-08T10:00:00.1234567Z,8500
            """
        },

        // Explicit zero UTC offset
        {
            """
            Timestamp,Pressure
            2026-11-08T10:00:00+00:00,8500
            """
        },

        // Quoted numeric field
        {
            """
            Timestamp,Pressure
            2026-11-08T10:00:00Z,"8500"
            """
        }
    };

    public static TheoryData<string> InvalidCsvCases => new()
    {
        // Empty CSV
        { "" },

        // Missing timestamp header
        {
            """
            Time,Pressure
            2026-11-08T10:00:00Z,8500
            """
        },

        // No measurement channels
        {
            """
            Timestamp
            2026-11-08T10:00:00Z
            """
        },

        // Duplicate channel names
        {
            """
            Timestamp,Pressure,Pressure
            2026-11-08T10:00:00Z,8500,8700
            """
        },

        // Empty channel name
        {
            """
            Timestamp,Pressure,
            2026-11-08T10:00:00Z,8500,8700
            """
        },

        // Header only, no telemetry samples
        { "Timestamp,Pressure\n" },

        // Missing column
        {
            """
            Timestamp,Pressure,Rate
            2026-11-08T10:00:00Z,8500
            """
        },

        // Extra column
        {
            """
            Timestamp,Pressure
            2026-11-08T10:00:00Z,8500,42.5
            """
        },

        // Invalid numeric value
        {
            """
            Timestamp,Pressure
            2026-11-08T10:00:00Z,not-a-number
            """
        },

        // NaN
        {
            """
            Timestamp,Pressure
            2026-11-08T10:00:00Z,NaN
            """
        },

        // Infinity
        {
            """
            Timestamp,Pressure
            2026-11-08T10:00:00Z,Infinity
            """
        },

        // Non-UTC offset
        {
            """
            Timestamp,Pressure
            2026-11-08T10:00:00+02:00,8500
            """
        },

        // Invalid timestamp
        {
            """
            Timestamp,Pressure
            invalid-timestamp,8500
            """
        },

        // Too many fractional digits
        {
            """
            Timestamp,Pressure
            2026-11-08T10:00:00.12345678Z,8500
            """
        },

        // Unterminated quoted field
        {
            "Timestamp,Pressure\n" +
            "2026-11-08T10:00:00Z,\"8500"
        },

        // Unexpected quote in unquoted field
        {
            "Timestamp,Pressure\n" +
            "2026-11-08T10:00:00Z,85\"00\n"
        },

        // Characters after closing quote
        {
            "Timestamp,Pressure\n" +
            "2026-11-08T10:00:00Z,\"8500\"extra\n"
        }
    };
}
