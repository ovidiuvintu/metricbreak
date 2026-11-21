namespace MetricBreak.Ingestion.Csv;

/// <summary>
/// An explicit timestamp column and format chosen for one CSV import.
/// </summary>
public sealed record TimestampMapping(int ColumnIndex, TimestampFormat Format)
{
    public TimestampOrigin Origin =>
        Format == TimestampFormat.RelativeElapsedSeconds
            ? TimestampOrigin.Elapsed
            : TimestampOrigin.Unix;
}
