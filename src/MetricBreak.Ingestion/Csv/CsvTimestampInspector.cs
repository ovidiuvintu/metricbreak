namespace MetricBreak.Ingestion.Csv;

/// <summary>
/// Reads a CSV and reports the timestamp mapping that import would use.
/// </summary>
public static class CsvTimestampInspector
{
    public static TimestampInspection Inspect(
        TextReader reader,
        TimestampMapping? mapping = null)
    {
        ArgumentNullException.ThrowIfNull(reader);

        var table = CsvTable.Read(reader);
        return Inspect(table, mapping);
    }

    internal static TimestampInspection Inspect(
        CsvTable table,
        TimestampMapping? mapping)
    {
        TimestampDetection detection;
        try
        {
            detection = TimestampColumnDetector.Detect(table.Headers, table.Rows);
        }
        catch (FormatException exception) when (mapping is not null)
        {
            detection = SyntheticDetection(table, mapping, exception.Message);
        }

        var applied = mapping ?? detection.Mapping;

        if (applied.ColumnIndex < 0 || applied.ColumnIndex >= table.Headers.Count)
        {
            throw new FormatException(
                $"Timestamp column index {applied.ColumnIndex} is outside the CSV header.");
        }

        TimestampColumnDetector.Validate(table.Rows, applied);

        return new TimestampInspection(
            detection,
            applied,
            TimestampColumnDetector.BuildPreview(table.Rows, applied));
    }

    private static TimestampDetection SyntheticDetection(
        CsvTable table,
        TimestampMapping mapping,
        string note)
    {
        if (mapping.ColumnIndex < 0 || mapping.ColumnIndex >= table.Headers.Count)
        {
            throw new FormatException(
                $"Timestamp column index {mapping.ColumnIndex} is outside the CSV header.");
        }

        var columns = table.Headers
            .Select((name, index) => new TimestampColumnCandidate(
                index,
                name,
                index == mapping.ColumnIndex ? [mapping.Format] : []))
            .ToArray();

        return new TimestampDetection(
            mapping.ColumnIndex,
            table.Headers[mapping.ColumnIndex],
            mapping.Format,
            ambiguous: true,
            columns,
            note);
    }
}

public sealed class TimestampInspection
{
    public TimestampInspection(
        TimestampDetection detection,
        TimestampMapping applied,
        IReadOnlyList<TimestampPreviewRow> preview)
    {
        ArgumentNullException.ThrowIfNull(detection);
        ArgumentNullException.ThrowIfNull(applied);
        ArgumentNullException.ThrowIfNull(preview);

        Detection = detection;
        Applied = applied;
        Preview = preview;
    }

    public TimestampDetection Detection { get; }

    public TimestampMapping Applied { get; }

    public IReadOnlyList<TimestampPreviewRow> Preview { get; }
}
