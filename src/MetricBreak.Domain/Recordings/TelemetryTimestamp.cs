
namespace MetricBreak.Domain.Recordings;

/// <summary>
/// Represents an absolute UTC instant as nanoseconds
/// elapsed since the Unix epoch (1970-01-01T00:00:00Z).
/// </summary>
public readonly record struct TelemetryTimestamp(
    long UnixNanoseconds) : IComparable<TelemetryTimestamp>
{
    private const long _nanosecondsPerTick = 100;
    private const long _nanosecondsPerSecond = 1_000_000_000;

    public static TelemetryTimestamp FromDateTimeOffset(
        DateTimeOffset value)
    {
        long ticksSinceEpoch = checked(
            value.UtcTicks - DateTime.UnixEpoch.Ticks);

        return new TelemetryTimestamp(
            checked(ticksSinceEpoch * _nanosecondsPerTick));
    }

    public static TelemetryTimestamp FromUnixMilliseconds(
        long milliseconds)
    {
        return new TelemetryTimestamp(
            checked(milliseconds * 1_000_000));
    }

    public static TelemetryTimestamp FromUnixMicroseconds(
        long microseconds)
    {
        return new TelemetryTimestamp(
            checked(microseconds * 1_000));
    }

    public int CompareTo(TelemetryTimestamp other) =>
        UnixNanoseconds.CompareTo(other.UnixNanoseconds);

    public DateTimeOffset ToDateTimeOffset()
    {
        long ticks = Math.DivRem(
            UnixNanoseconds,
            _nanosecondsPerTick,
            out long remainder);

        // Floor division is needed for timestamps before 1970.
        if (remainder < 0)
            ticks--;

        return DateTimeOffset.UnixEpoch.AddTicks(ticks);
    }

    public static long DifferenceInNanoseconds(
        TelemetryTimestamp later,
        TelemetryTimestamp earlier) =>
        checked(later.UnixNanoseconds - earlier.UnixNanoseconds);
}

