namespace MetricBreak.Ingestion.Csv;

internal sealed class CsvTable
{
    private CsvTable(
        IReadOnlyList<string> headers,
        IReadOnlyList<string[]> rows)
    {
        Headers = headers;
        Rows = rows;
    }

    public IReadOnlyList<string> Headers { get; }

    public IReadOnlyList<string[]> Rows { get; }

    public static CsvTable Read(TextReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        var header = CsvRecordReader.ReadRecord(reader);
        if (header is null)
            throw new FormatException("CSV is empty.");

        if (header.Count < 2)
        {
            throw new FormatException(
                "CSV must contain a timestamp column and at least one telemetry channel.");
        }

        if (header.Any(string.IsNullOrWhiteSpace) ||
            header.Distinct(StringComparer.Ordinal).Count() != header.Count)
        {
            throw new FormatException(
                "Channel names must be nonempty and unique.");
        }

        var rows = new List<string[]>();
        List<string>? fields;
        while ((fields = CsvRecordReader.ReadRecord(reader)) is not null)
        {
            if (IsBlankRow(fields))
                break;

            var recordNumber = rows.Count + 2;
            if (fields.Count != header.Count)
            {
                throw new FormatException(
                    $"CSV record {recordNumber} has an unexpected number of columns.");
            }

            rows.Add(fields.ToArray());
        }

        if (rows.Count == 0)
            throw new FormatException("CSV contains no telemetry samples.");

        return new CsvTable(header, rows);
    }

    private static bool IsBlankRow(IReadOnlyList<string> fields) =>
        fields.All(string.IsNullOrWhiteSpace);
}
