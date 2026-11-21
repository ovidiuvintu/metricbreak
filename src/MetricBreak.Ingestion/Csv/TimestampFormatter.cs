using System.Globalization;
using MetricBreak.Domain.Recordings;

namespace MetricBreak.Ingestion.Csv;

/// <summary>
/// Renders a normalized timestamp for preview.
/// Relative values stay elapsed durations. Absolute values are UTC.
/// </summary>
public static class TimestampFormatter
{
    private const long _nanosecondsPerSecond = 1_000_000_000L;
    private const long _ticksPerSecond = 10_000_000L;

    public static string Format(TelemetryTimestamp timestamp, TimestampFormat format)
    {
        if (!Enum.IsDefined(format))
            throw new ArgumentOutOfRangeException(nameof(format));

        return format == TimestampFormat.RelativeElapsedSeconds
            ? FormatElapsed(timestamp.UnixNanoseconds)
            : FormatUtc(timestamp.UnixNanoseconds);
    }

    public static string FormatElapsed(long nanoseconds)
    {
        var negative = nanoseconds < 0;
        var magnitude = negative ? checked(-nanoseconds) : nanoseconds;
        var seconds = magnitude / _nanosecondsPerSecond;
        var fraction = magnitude % _nanosecondsPerSecond;
        var body = fraction == 0
            ? seconds.ToString(CultureInfo.InvariantCulture)
            : string.Concat(
                seconds.ToString(CultureInfo.InvariantCulture),
                ".",
                TrimFraction(fraction));

        return negative ? string.Concat("-", body, " s") : string.Concat(body, " s");
    }

    public static string FormatUtc(long nanoseconds)
    {
        var seconds = nanoseconds / _nanosecondsPerSecond;
        var fraction = nanoseconds % _nanosecondsPerSecond;
        if (fraction < 0)
        {
            seconds--;
            fraction += _nanosecondsPerSecond;
        }

        var instant = DateTimeOffset.UnixEpoch.AddTicks(
            checked(seconds * _ticksPerSecond));
        var text = instant.ToString(
            "yyyy-MM-dd'T'HH:mm:ss",
            CultureInfo.InvariantCulture);

        return fraction == 0
            ? string.Concat(text, "Z")
            : string.Concat(text, ".", TrimFraction(fraction), "Z");
    }

    private static string TrimFraction(long fraction) =>
        fraction.ToString("000000000", CultureInfo.InvariantCulture)
            .TrimEnd('0');
}
