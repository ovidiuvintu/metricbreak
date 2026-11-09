
using System.Globalization;
using System.Text;
using MetricBreak.Domain.Recordings;

namespace MetricBreak.Ingestion.Csv;

public sealed class IndexedCsvTelemetryParser
{
    public IndexedCsvTelemetryResult Parse(TextReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        var header = ReadRecord(reader);

        if (header is null)
            throw new FormatException("CSV is empty.");

        if (header.Count < 2 ||
            header[0] != "Timestamp")
        {
            throw new FormatException(
                "First column must be Timestamp, followed by telemetry channels.");
        }

        var names = header.Skip(1).ToArray();

        if (names.Any(string.IsNullOrWhiteSpace) ||
            names.Distinct(StringComparer.Ordinal).Count() != names.Length)
        {
            throw new FormatException(
                "Channel names must be nonempty and unique.");
        }

        var channels = names
            .Select(name => new ChannelDefinition(
                Guid.NewGuid(), name, null))
            .ToArray();

        var channelIndex = new ChannelIndex(
            channels.Select(channel => channel.Id));

        var samples = new List<TelemetrySample>();

        List<string>? fields;

        while ((fields = ReadRecord(reader)) is not null)
        {
            if (fields.Count != header.Count)
            {
                throw new FormatException(
                    $"CSV record {samples.Count + 2} has an unexpected number of columns.");
            }

            if (!DateTimeOffset.TryParseExact(
                    fields[0],
                    "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var timestamp) ||
                timestamp.Offset != TimeSpan.Zero)
            {
                throw new FormatException(
                    $"Invalid UTC timestamp at record {samples.Count + 2}.");
            }

            var values = new double?[channels.Length];

            for (var i = 0; i < channels.Length; i++)
            {
                var field = fields[i + 1];

                if (field.Length == 0)
                {
                    values[i] = null;
                    continue;
                }

                if (!double.TryParse(
                        field,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out var value) ||
                    !double.IsFinite(value))
                {
                    throw new FormatException(
                        $"Invalid numeric value at record {samples.Count + 2}, column {i + 2}.");
                }

                values[i] = value;
            }

            samples.Add(new TelemetrySample(
                samples.Count,
                TelemetryTimestamp.FromDateTimeOffset(timestamp),
                channelIndex,
                values));
        }

        if (samples.Count == 0)
            throw new FormatException("CSV contains no telemetry samples.");

        return new IndexedCsvTelemetryResult(channels, samples);
    }

    private static List<string>? ReadRecord(TextReader reader)
    {
        var fields = new List<string>();
        var field = new StringBuilder();

        var inQuotes = false;
        var quotedFieldClosed = false;
        var atFieldStart = true;
        var sawInput = false;

        while (true)
        {
            var next = reader.Read();

            if (next == -1)
            {
                if (inQuotes)
                    throw new FormatException("Unterminated quoted CSV field.");

                if (!sawInput)
                    return null;

                fields.Add(field.ToString());
                return fields;
            }

            sawInput = true;
            var c = (char)next;

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (reader.Peek() == '"')
                    {
                        reader.Read();
                        field.Append('"');
                    }
                    else
                    {
                        inQuotes = false;
                        quotedFieldClosed = true;
                    }
                }
                else
                {
                    field.Append(c);
                }

                continue;
            }

            if (c == ',')
            {
                fields.Add(field.ToString());
                field.Clear();
                atFieldStart = true;
                quotedFieldClosed = false;
                continue;
            }

            if (c == '\r' || c == '\n')
            {
                if (c == '\r' && reader.Peek() == '\n')
                    reader.Read();

                fields.Add(field.ToString());
                return fields;
            }

            if (quotedFieldClosed)
            {
                throw new FormatException(
                    "Unexpected character after closing CSV quote.");
            }

            if (c == '"')
            {
                if (!atFieldStart)
                {
                    throw new FormatException(
                        "Unexpected quote inside unquoted CSV field.");
                }

                inQuotes = true;
                atFieldStart = false;
                continue;
            }

            field.Append(c);
            atFieldStart = false;
        }
    }
}
