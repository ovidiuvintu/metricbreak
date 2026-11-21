namespace MetricBreak.Ingestion.Csv;

/// <summary>
/// A timestamp representation that can be normalized into the replay clock.
/// </summary>
public enum TimestampFormat
{
    Iso8601,
    UnixEpochSeconds,
    UnixEpochMilliseconds,
    UnixEpochMicroseconds,
    RelativeElapsedSeconds
}
