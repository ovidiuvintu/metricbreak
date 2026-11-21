namespace MetricBreak.Ingestion.Csv;

public enum TimestampParseFailure
{
    None,
    Missing,
    Malformed,
    OutOfRange
}
