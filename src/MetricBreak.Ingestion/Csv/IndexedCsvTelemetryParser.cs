using System.Globalization;
using MetricBreak.Domain.Recordings;

namespace MetricBreak.Ingestion.Csv;

public sealed class IndexedCsvTelemetryParser
{
    public IndexedCsvTelemetryResult Parse(TextReader reader) =>
        Parse(reader, mapping: null);

    public IndexedCsvTelemetryResult Parse(
        TextReader reader,
        TimestampMapping? mapping)
    {
        ArgumentNullException.ThrowIfNull(reader);

        var table = CsvTable.Read(reader);
        var inspection = CsvTimestampInspector.Inspect(table, mapping);
        if (mapping is null && inspection.Detection.Ambiguous)
        {
            throw new FormatException(
                "Timestamp mapping is ambiguous. Choose a timestamp column and format.");
        }

        var applied = inspection.Applied;
        var channelIndexes = Enumerable.Range(0, table.Headers.Count)
            .Where(index => index != applied.ColumnIndex)
            .ToArray();

        if (channelIndexes.Length == 0)
        {
            throw new FormatException(
                "CSV must contain a timestamp column and at least one telemetry channel.");
        }

        var names = channelIndexes
            .Select(index => table.Headers[index])
            .ToArray();

        var stamped = new List<(TelemetryTimestamp Timestamp, string[] Values)>(table.Rows.Count);
        for (var rowIndex = 0; rowIndex < table.Rows.Count; rowIndex++)
        {
            var recordNumber = rowIndex + 2;
            var row = table.Rows[rowIndex];
            var timestamp = TimestampParser.Parse(
                row[applied.ColumnIndex],
                applied.Format,
                recordNumber);

            var values = new string[channelIndexes.Length];
            for (var i = 0; i < channelIndexes.Length; i++)
            {
                values[i] = row[channelIndexes[i]];
                if (IsNonFiniteToken(values[i]))
                {
                    throw new FormatException(
                        $"Invalid numeric value at record {recordNumber}, column {channelIndexes[i] + 1}.");
                }
            }

            stamped.Add((timestamp, values));
        }

        var types = new ChannelValueType[names.Length];
        for (var column = 0; column < names.Length; column++)
        {
            var nonEmpty = stamped
                .Select(row => row.Values[column])
                .Where(value => value.Length != 0)
                .ToArray();

            if (nonEmpty.Length == 0)
            {
                throw new FormatException(
                    $"Cannot infer type for empty channel '{names[column]}'.");
            }

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
        var samples = new List<TelemetrySample>(stamped.Count);

        foreach (var row in stamped)
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

        var timeline = CsvAnnotationSectionReader.TryRead(
            reader,
            applied.Format,
            samples);

        return new IndexedCsvTelemetryResult(
            channels,
            samples,
            applied.Origin,
            timeline,
            applied);
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
