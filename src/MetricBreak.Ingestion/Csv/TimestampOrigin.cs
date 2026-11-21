namespace MetricBreak.Ingestion.Csv;

/// <summary>
/// How a normalized timestamp should be read.
/// Elapsed values are nanoseconds from zero, not a calendar date.
/// </summary>
public enum TimestampOrigin
{
    Unix,
    Elapsed
}
