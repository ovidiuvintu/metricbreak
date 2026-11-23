using MetricBreak.Domain.Recordings;

namespace MetricBreak.Ingestion.Csv;

/// <summary>
/// Parses one CSV cell as a known timestamp format.
/// Detection of the column and format lives elsewhere.
/// </summary>
public static class TimestampParser
{
    private const long _nanosecondsPerSecond = 1_000_000_000L;
    private const long _nanosecondsPerMillisecond = 1_000_000L;
    private const long _nanosecondsPerMicrosecond = 1_000L;

    public static TelemetryTimestamp Parse(
        string? text,
        TimestampFormat format,
        int recordNumber)
    {
        if (!Enum.IsDefined(format))
        {
            throw new ArgumentOutOfRangeException(nameof(format));
        }

        if (!TryParse(text, format, out var timestamp, out var failure))
        {
            throw new FormatException(Describe(failure, format, recordNumber));
        }

        return timestamp;
    }

    public static bool TryParse(
        string? text,
        TimestampFormat format,
        out TelemetryTimestamp timestamp,
        out TimestampParseFailure failure)
    {
        timestamp = default;

        if (!Enum.IsDefined(format))
        {
            throw new ArgumentOutOfRangeException(nameof(format));
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            failure = TimestampParseFailure.Missing;
            return false;
        }

        var ok = format switch
        {
            TimestampFormat.Iso8601 =>
                TryParseIso8601(text.Trim(), out timestamp, out failure),
            TimestampFormat.UnixEpochSeconds =>
                TryParseScaled(
                    text.Trim(),
                    _nanosecondsPerSecond,
                    maxFractionDigits: 9,
                    allowNegative: true,
                    out timestamp,
                    out failure),
            TimestampFormat.UnixEpochMilliseconds =>
                TryParseScaled(
                    text.Trim(),
                    _nanosecondsPerMillisecond,
                    maxFractionDigits: 6,
                    allowNegative: true,
                    out timestamp,
                    out failure),
            TimestampFormat.UnixEpochMicroseconds =>
                TryParseScaled(
                    text.Trim(),
                    _nanosecondsPerMicrosecond,
                    maxFractionDigits: 3,
                    allowNegative: true,
                    out timestamp,
                    out failure),
            TimestampFormat.UnixEpochNanoseconds =>
                TryParseScaled(
                    text.Trim(),
                    nanosecondsPerUnit: 1,
                    maxFractionDigits: 0,
                    allowNegative: true,
                    out timestamp,
                    out failure),
            TimestampFormat.RelativeElapsedSeconds =>
                TryParseScaled(
                    text.Trim(),
                    _nanosecondsPerSecond,
                    maxFractionDigits: 9,
                    allowNegative: false,
                    out timestamp,
                    out failure),
            _ => throw new ArgumentOutOfRangeException(nameof(format))
        };

        return ok;
    }

    public static string Describe(
        TimestampParseFailure failure,
        TimestampFormat format,
        int recordNumber)
    {
        var label = FormatLabel(format);

        return failure switch
        {
            TimestampParseFailure.Missing =>
                $"Missing timestamp at record {recordNumber}.",
            TimestampParseFailure.Malformed =>
                $"Malformed {label} timestamp at record {recordNumber}.",
            TimestampParseFailure.OutOfRange =>
                $"Timestamp at record {recordNumber} is out of range for {label}.",
            _ => $"Invalid timestamp at record {recordNumber}."
        };
    }

    public static string FormatLabel(TimestampFormat format) => format switch
    {
        TimestampFormat.Iso8601 => "ISO-8601",
        TimestampFormat.UnixEpochSeconds => "Unix epoch seconds",
        TimestampFormat.UnixEpochMilliseconds => "Unix epoch milliseconds",
        TimestampFormat.UnixEpochMicroseconds => "Unix epoch microseconds",
        TimestampFormat.UnixEpochNanoseconds => "Unix epoch nanoseconds",
        TimestampFormat.RelativeElapsedSeconds => "relative elapsed seconds",
        _ => format.ToString()
    };

    private static bool TryParseIso8601(
        string text,
        out TelemetryTimestamp timestamp,
        out TimestampParseFailure failure)
    {
        timestamp = default;
        var index = 0;

        if (!TryReadFixedDigits(text, ref index, 4, out var year) ||
            !TryReadChar(text, ref index, '-') ||
            !TryReadFixedDigits(text, ref index, 2, out var month) ||
            !TryReadChar(text, ref index, '-') ||
            !TryReadFixedDigits(text, ref index, 2, out var day))
        {
            failure = TimestampParseFailure.Malformed;
            return false;
        }

        var separator = index < text.Length ? text[index] : '\0';
        if (separator is not ('T' or 't' or ' '))
        {
            failure = TimestampParseFailure.Malformed;
            return false;
        }

        index++;

        if (!TryReadFixedDigits(text, ref index, 2, out var hour) ||
            !TryReadChar(text, ref index, ':') ||
            !TryReadFixedDigits(text, ref index, 2, out var minute) ||
            !TryReadChar(text, ref index, ':') ||
            !TryReadFixedDigits(text, ref index, 2, out var second))
        {
            failure = TimestampParseFailure.Malformed;
            return false;
        }

        long fractionalNanoseconds = 0;
        if (index < text.Length && text[index] == '.')
        {
            index++;
            var start = index;
            while (index < text.Length && text[index] is >= '0' and <= '9')
                index++;

            var digits = index - start;
            if (digits is < 1 or > 9)
            {
                failure = digits > 9
                    ? TimestampParseFailure.OutOfRange
                    : TimestampParseFailure.Malformed;
                return false;
            }

            if (!TryDigitsToInt64(text.AsSpan(start, digits), out fractionalNanoseconds))
            {
                failure = TimestampParseFailure.OutOfRange;
                return false;
            }

            for (var pad = digits; pad < 9; pad++)
            {
                fractionalNanoseconds *= 10;
            }
        }

        if (!TryReadOffset(text, ref index, out var offset) || index != text.Length)
        {
            failure = TimestampParseFailure.Malformed;
            return false;
        }

        DateTimeOffset wholeSecond;
        try
        {
            wholeSecond = new DateTimeOffset(
                year, month, day, hour, minute, second, offset);
        }
        catch (ArgumentOutOfRangeException)
        {
            failure = TimestampParseFailure.Malformed;
            return false;
        }

        try
        {
            var whole = TelemetryTimestamp.FromDateTimeOffset(wholeSecond);
            timestamp = new TelemetryTimestamp(
                checked(whole.UnixNanoseconds + fractionalNanoseconds));
        }
        catch (OverflowException)
        {
            failure = TimestampParseFailure.OutOfRange;
            return false;
        }

        failure = TimestampParseFailure.None;
        return true;
    }

    private static bool TryParseScaled(
        string text,
        long nanosecondsPerUnit,
        int maxFractionDigits,
        bool allowNegative,
        out TelemetryTimestamp timestamp,
        out TimestampParseFailure failure)
    {
        timestamp = default;

        if (!TryReadDecimal(
                text,
                maxFractionDigits,
                out var negative,
                out var whole,
                out var fraction,
                out var fractionDigits,
                out failure))
        {
            return false;
        }

        if (negative && !allowNegative)
        {
            failure = TimestampParseFailure.OutOfRange;
            return false;
        }

        try
        {
            var scaledWhole = checked(whole * nanosecondsPerUnit);
            var scaledFraction = fraction;
            for (var pad = fractionDigits; pad < maxFractionDigits; pad++)
            {
                scaledFraction = checked(scaledFraction * 10);
            }

            // Fraction digits are already in nanoseconds once padded
            // to the unit's exact nanosecond scale.
            var magnitude = checked(scaledWhole + scaledFraction);
            if (negative)
                magnitude = checked(-magnitude);

            timestamp = new TelemetryTimestamp(magnitude);
        }
        catch (OverflowException)
        {
            failure = TimestampParseFailure.OutOfRange;
            return false;
        }

        failure = TimestampParseFailure.None;
        return true;
    }

    private static bool TryReadDecimal(
        string text,
        int maxFractionDigits,
        out bool negative,
        out long whole,
        out long fraction,
        out int fractionDigits,
        out TimestampParseFailure failure)
    {
        negative = false;
        whole = 0;
        fraction = 0;
        fractionDigits = 0;

        if (text.Length == 0)
        {
            failure = TimestampParseFailure.Missing;
            return false;
        }

        var index = 0;
        if (text[0] == '+' || text[0] == '-')
        {
            negative = text[0] == '-';
            index++;
        }

        var sawDigit = false;
        var sawDot = false;
        try
        {
            for (; index < text.Length; index++)
            {
                var c = text[index];
                if (c is >= '0' and <= '9')
                {
                    sawDigit = true;
                    var digit = c - '0';
                    if (!sawDot)
                    {
                        whole = checked(whole * 10 + digit);
                    }
                    else
                    {
                        fractionDigits++;
                        if (fractionDigits > maxFractionDigits)
                        {
                            failure = TimestampParseFailure.OutOfRange;
                            return false;
                        }

                        fraction = checked(fraction * 10 + digit);
                    }
                }
                else if (c == '.' && !sawDot)
                {
                    sawDot = true;
                }
                else
                {
                    failure = TimestampParseFailure.Malformed;
                    return false;
                }
            }
        }
        catch (OverflowException)
        {
            failure = TimestampParseFailure.OutOfRange;
            return false;
        }

        if (!sawDigit)
        {
            failure = TimestampParseFailure.Malformed;
            return false;
        }

        failure = TimestampParseFailure.None;
        return true;
    }

    private static bool TryReadOffset(
        string text,
        ref int index,
        out TimeSpan offset)
    {
        offset = default;
        if (index >= text.Length)
        {
            // Wall-clock values such as "2026-11-22 08:00:00" carry no zone.
            // They are absolute UTC so every recording shares one timeline.
            offset = TimeSpan.Zero;
            return true;
        }

        var signChar = text[index];
        if (signChar is 'Z' or 'z')
        {
            index++;
            offset = TimeSpan.Zero;
            return true;
        }

        if (signChar is not '+' and not '-')
            return false;

        var sign = signChar == '+' ? 1 : -1;
        index++;
        var start = index;
        while (index < text.Length && text[index] is >= '0' and <= '9' or ':')
            index++;

        var token = text.AsSpan(start, index - start);
        int hours;
        int minutes;
        if (token.Length == 5 && token[2] == ':')
        {
            if (!TryDigits(token[..2], out hours) || !TryDigits(token[3..], out minutes))
                return false;
        }
        else if (token.Length == 4)
        {
            if (!TryDigits(token[..2], out hours) || !TryDigits(token[2..], out minutes))
                return false;
        }
        else if (token.Length == 2)
        {
            if (!TryDigits(token, out hours))
                return false;
            minutes = 0;
        }
        else
        {
            return false;
        }

        if (hours > 14 || minutes > 59)
            return false;

        offset = new TimeSpan(sign * hours, sign * minutes, 0);
        return true;
    }

    private static bool TryReadChar(string text, ref int index, char expected)
    {
        if (index >= text.Length || text[index] != expected)
            return false;

        index++;
        return true;
    }

    private static bool TryReadFixedDigits(
        string text,
        ref int index,
        int count,
        out int value)
    {
        value = 0;
        if (index + count > text.Length)
            return false;

        for (var i = 0; i < count; i++)
        {
            var c = text[index + i];
            if (c is < '0' or > '9')
                return false;

            value = value * 10 + (c - '0');
        }

        index += count;
        return true;
    }

    private static bool TryDigits(ReadOnlySpan<char> text, out int value)
    {
        value = 0;
        if (text.Length == 0)
            return false;

        foreach (var c in text)
        {
            if (c is < '0' or > '9')
                return false;

            value = value * 10 + (c - '0');
        }

        return true;
    }

    private static bool TryDigitsToInt64(ReadOnlySpan<char> text, out long value)
    {
        value = 0;
        foreach (var c in text)
        {
            value = value * 10 + (c - '0');
        }

        return true;
    }
}
