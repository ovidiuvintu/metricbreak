using System.Globalization;
using MetricBreak.Domain.Recordings;

namespace MetricBreak.Ingestion.Csv;

public sealed class IndexedCsvTelemetryParser
{
    public IndexedCsvTelemetryResult Parse(TextReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        var header = CsvRecordReader.ReadRecord(reader);
        if (header is null)
            throw new FormatException("CSV is empty.");

        if (header.Count < 2 || header[0] != "Timestamp")
            throw new FormatException("First column must be Timestamp, followed by telemetry channels.");

        var names = header.Skip(1).ToArray();
        if (names.Any(string.IsNullOrWhiteSpace) ||
            names.Distinct(StringComparer.Ordinal).Count() != names.Length)
            throw new FormatException("Channel names must be nonempty and unique.");

        // Store raw values until every column's type can be inferred. No guessing
        // from the first sample, and no industry-specific interpretation.
        var rows = new List<(TelemetryTimestamp Timestamp, string[] Values)>();
        List<string>? fields;
        while ((fields = CsvRecordReader.ReadRecord(reader)) is not null)
        {
            var recordNumber = rows.Count + 2;
            if (fields.Count != header.Count)
                throw new FormatException($"CSV record {recordNumber} has an unexpected number of columns.");

            if (!DateTimeOffset.TryParseExact(
                    fields[0], "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK",
                    CultureInfo.InvariantCulture, DateTimeStyles.None,
                    out var timestamp) || timestamp.Offset != TimeSpan.Zero)
                throw new FormatException($"Invalid UTC timestamp at record {recordNumber}.");

            var values = fields.Skip(1).ToArray();
            for (var i = 0; i < values.Length; i++)
            {
                // Reject non-finite tokens even if the column would otherwise
                // be classified as categorical. Preserve the prior import policy.
                if (IsNonFiniteToken(values[i]))
                    throw new FormatException($"Invalid numeric value at record {recordNumber}, column {i + 2}.");
            }

            rows.Add((TelemetryTimestamp.FromDateTimeOffset(timestamp), values));
        }

        if (rows.Count == 0)
            throw new FormatException("CSV contains no telemetry samples.");

        var types = new ChannelValueType[names.Length];
        for (var column = 0; column < names.Length; column++)
        {
            var nonEmpty = rows.Select(row => row.Values[column])
                .Where(value => value.Length != 0).ToArray();

            // An entirely empty channel has no evidence of its type.
            if (nonEmpty.Length == 0)
                throw new FormatException($"Cannot infer type for empty channel '{names[column]}'.");

            if (nonEmpty.All(IsFiniteNumeric))
                types[column] = ChannelValueType.Numeric;
            else if (nonEmpty.All(IsBoolean))
                types[column] = ChannelValueType.Boolean;
            else
                types[column] = ChannelValueType.Categorical;
        }

        var channels = names.Select((name, i) =>
            new ChannelDefinition(Guid.NewGuid(), name, null, types[i])).ToArray();
        var channelIndex = new ChannelIndex(channels.Select(channel => channel.Id));
        var samples = new List<TelemetrySample>(rows.Count);

        foreach (var row in rows)
        {
            var values = new ChannelValue?[channels.Length];
            for (var column = 0; column < channels.Length; column++)
            {
                var raw = row.Values[column];
                if (raw.Length == 0)
                    continue;

                values[column] = types[column] switch
                {
                    ChannelValueType.Numeric => new ChannelValue.Numeric(
                        double.Parse(raw, NumberStyles.Float, CultureInfo.InvariantCulture)),
                    ChannelValueType.Boolean => new ChannelValue.Boolean(
                        bool.Parse(raw)),
                    ChannelValueType.Categorical => new ChannelValue.Categorical(raw),
                    _ => throw new InvalidOperationException("Unsupported channel type.")
                };
            }

            samples.Add(TelemetrySample.FromIndexedTypedValues(
                samples.Count, row.Timestamp, channelIndex, values));
        }

        return new IndexedCsvTelemetryResult(channels, samples);
    }

    private static bool IsFiniteNumeric(string value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture,
            out var number) && double.IsFinite(number);

    private static bool IsBoolean(string value) =>
        bool.TryParse(value, out _);

    private static bool IsNonFiniteToken(string value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture,
            out var number) && !double.IsFinite(number);
}
