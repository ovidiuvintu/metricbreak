namespace MetricBreak.Domain.Recordings;

public readonly record struct TelemetryTimestamp(
    long UnixMicroseconds)
{
    public static TelemetryTimestamp FromDateTimeOffset(
        DateTimeOffset timestamp)
    {
        long microseconds =
            (timestamp.UtcTicks -
             DateTime.UnixEpoch.Ticks) / 10;

        return new TelemetryTimestamp(microseconds);
    }
}
