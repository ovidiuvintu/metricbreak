namespace MetricBreak.Ingestion.Csv;

public sealed record TimestampColumnCandidate(
    int Index,
    string Name,
    IReadOnlyList<TimestampFormat> Formats);

public sealed record TimestampPreviewRow(
    long SampleIndex,
    string Raw,
    string Normalized);

/// <summary>
/// Suggested timestamp column and format for one CSV table.
/// </summary>
public sealed class TimestampDetection
{
    public TimestampDetection(
        int columnIndex,
        string columnName,
        TimestampFormat format,
        bool ambiguous,
        IReadOnlyList<TimestampColumnCandidate> columns,
        string? note)
    {
        if (columnIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(columnIndex));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(columnName);
        if (!Enum.IsDefined(format))
            throw new ArgumentOutOfRangeException(nameof(format));

        ArgumentNullException.ThrowIfNull(columns);

        ColumnIndex = columnIndex;
        ColumnName = columnName;
        Format = format;
        Origin = format == TimestampFormat.RelativeElapsedSeconds
            ? TimestampOrigin.Elapsed
            : TimestampOrigin.Unix;
        Ambiguous = ambiguous;
        Columns = columns;
        Note = note;
    }

    public int ColumnIndex { get; }

    public string ColumnName { get; }

    public TimestampFormat Format { get; }

    public TimestampOrigin Origin { get; }

    public bool Ambiguous { get; }

    public IReadOnlyList<TimestampColumnCandidate> Columns { get; }

    public string? Note { get; }

    public TimestampMapping Mapping => new(ColumnIndex, Format);
}
