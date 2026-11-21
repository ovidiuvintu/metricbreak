using MetricBreak.Domain.Recordings;

namespace MetricBreak.Ingestion.Csv;

/// <summary>
/// Chooses a timestamp column and format from headers and a sample of rows.
/// Parsing a chosen format is handled by <see cref="TimestampParser"/>.
/// </summary>
public static class TimestampColumnDetector
{
    public const int SampleRowCount = 32;

    public const int PreviewRowCount = 5;

    // Modern calendar dates used only to prefer one epoch scale.
    // Values outside this window stay valid when the user selects a format.
    private static readonly long _windowStartNanoseconds =
        TelemetryTimestamp.FromDateTimeOffset(
            new DateTimeOffset(1990, 1, 1, 0, 0, 0, TimeSpan.Zero)).UnixNanoseconds;

    private static readonly long _windowEndNanoseconds =
        TelemetryTimestamp.FromDateTimeOffset(
            new DateTimeOffset(2100, 1, 1, 0, 0, 0, TimeSpan.Zero)).UnixNanoseconds;

    private static readonly TimestampFormat[] _epochFormats =
    [
        TimestampFormat.UnixEpochSeconds,
        TimestampFormat.UnixEpochMilliseconds,
        TimestampFormat.UnixEpochMicroseconds
    ];

    public static TimestampDetection Detect(
        IReadOnlyList<string> headers,
        IReadOnlyList<IReadOnlyList<string>> rows)
    {
        ArgumentNullException.ThrowIfNull(headers);
        ArgumentNullException.ThrowIfNull(rows);

        if (headers.Count < 2)
        {
            throw new FormatException(
                "CSV must contain a timestamp column and at least one telemetry channel.");
        }

        if (rows.Count == 0)
        {
            throw new FormatException("CSV contains no telemetry samples.");
        }

        var sampleCount = Math.Min(SampleRowCount, rows.Count);
        var scored = new List<ScoredColumn>();
        string? rejectedNote = null;

        for (var column = 0; column < headers.Count; column++)
        {
            var name = headers[column];
            var cells = new string[sampleCount];
            for (var row = 0; row < sampleCount; row++)
            {
                cells[row] = column < rows[row].Count ? rows[row][column] : "";
            }

            var hint = HeaderHint(name);
            var nameScore = NameScore(name);
            if (!TryChooseFormat(cells, hint, out var format, out var formatAmbiguous, out var rejection))
            {
                if (nameScore >= 100 && rejectedNote is null)
                    rejectedNote = rejection;
                continue;
            }

            var score = nameScore;
            if (format == TimestampFormat.Iso8601)
                score += 80;
            else if (format != TimestampFormat.RelativeElapsedSeconds)
                score += 60;
            else if (nameScore > 0)
                score += 30;
            else
                continue;

            scored.Add(new ScoredColumn(
                column,
                name,
                format,
                formatAmbiguous,
                score));
        }

        if (scored.Count == 0)
        {
            throw new FormatException(
                rejectedNote ?? "No timestamp column was detected.");
        }

        var bestScore = scored.Max(column => column.Score);
        var best = scored
            .Where(column => column.Score == bestScore)
            .OrderBy(column => column.Format == TimestampFormat.Iso8601 ? 0 : 1)
            .ThenBy(column => column.Index)
            .First();

        var columnAmbiguous = scored.Count(column => column.Score == bestScore) > 1;
        var columns = headers
            .Select((name, index) => new TimestampColumnCandidate(
                index,
                name,
                index == best.Index ? [best.Format] : []))
            .ToArray();

        return new TimestampDetection(
            best.Index,
            best.Name,
            best.Format,
            best.FormatAmbiguous || columnAmbiguous || rejectedNote is not null,
            columns,
            rejectedNote);
    }

    public static IReadOnlyList<TimestampPreviewRow> BuildPreview(
        IReadOnlyList<IReadOnlyList<string>> rows,
        TimestampMapping mapping,
        int count = PreviewRowCount)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(mapping);

        if (count < 1)
            throw new ArgumentOutOfRangeException(nameof(count));

        var take = Math.Min(count, rows.Count);
        var preview = new TimestampPreviewRow[take];
        for (var row = 0; row < take; row++)
        {
            var raw = Cell(rows[row], mapping.ColumnIndex);
            var timestamp = TimestampParser.Parse(raw, mapping.Format, row + 2);
            preview[row] = new TimestampPreviewRow(
                row,
                raw.Trim(),
                TimestampFormatter.Format(timestamp, mapping.Format));
        }

        return preview;
    }

    public static void Validate(
        IReadOnlyList<IReadOnlyList<string>> rows,
        TimestampMapping mapping)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(mapping);

        for (var row = 0; row < rows.Count; row++)
        {
            var raw = Cell(rows[row], mapping.ColumnIndex);
            _ = TimestampParser.Parse(raw, mapping.Format, row + 2);
        }
    }

    private static bool TryChooseFormat(
        IReadOnlyList<string> cells,
        TimestampFormat? hint,
        out TimestampFormat format,
        out bool ambiguous,
        out string rejection)
    {
        format = default;
        ambiguous = false;
        rejection = "";

        if (All(cells, TimestampFormat.Iso8601))
        {
            format = TimestampFormat.Iso8601;
            ambiguous = hint is not null && hint != TimestampFormat.Iso8601;
            return true;
        }

        var plausible = _epochFormats
            .Where(candidate => AllInWindow(cells, candidate))
            .ToArray();

        if (plausible.Length == 1)
        {
            format = plausible[0];
            ambiguous = hint is not null && hint != format && All(cells, hint.Value);
            return true;
        }

        if (plausible.Length > 1)
        {
            format = hint is not null && plausible.Contains(hint.Value)
                ? hint.Value
                : plausible[0];
            ambiguous = true;
            return true;
        }

        if (hint is TimestampFormat hinted)
        {
            var hintedFailure = FirstFailure(cells, hinted);
            if (hintedFailure is TimestampParseFailure.None)
            {
                format = hinted;
                ambiguous = false;
                return true;
            }

            if (hintedFailure is TimestampParseFailure.Missing or TimestampParseFailure.OutOfRange)
            {
                rejection = hintedFailure == TimestampParseFailure.Missing
                    ? "A timestamp cell in the sampled rows is empty."
                    : $"A sampled value is out of range for {TimestampParser.FormatLabel(hinted)}.";
                return false;
            }
        }

        if (All(cells, TimestampFormat.RelativeElapsedSeconds))
        {
            format = TimestampFormat.RelativeElapsedSeconds;
            ambiguous = hint != TimestampFormat.RelativeElapsedSeconds &&
                All(cells, TimestampFormat.UnixEpochSeconds);
            return true;
        }

        if (All(cells, TimestampFormat.UnixEpochSeconds))
        {
            format = TimestampFormat.UnixEpochSeconds;
            ambiguous = true;
            return true;
        }

        rejection = DescribeUnparsed(cells);
        return false;
    }

    private static string DescribeUnparsed(IReadOnlyList<string> cells)
    {
        if (cells.Any(cell => string.IsNullOrWhiteSpace(cell)))
        {
            return "A timestamp cell in the sampled rows is empty.";
        }

        if (IsInconsistent(cells))
        {
            return "The sampled timestamp values do not share one format.";
        }

        var sawOutOfRange = cells.Any(cell =>
            TimestampFormatList.All.Any(format =>
                !TimestampParser.TryParse(cell, format, out _, out var failure) &&
                failure == TimestampParseFailure.OutOfRange));

        return sawOutOfRange
            ? "The sampled timestamp values are out of range."
            : "The sampled values are not a recognized timestamp format.";
    }

    private static bool IsInconsistent(IReadOnlyList<string> cells)
    {
        var signatures = new HashSet<string>(StringComparer.Ordinal);
        foreach (var cell in cells)
        {
            if (string.IsNullOrWhiteSpace(cell))
                continue;

            var matched = TimestampFormatList.All
                .Where(format => TimestampParser.TryParse(cell, format, out _, out _))
                .Select(format => format.ToString());
            signatures.Add(string.Join(',', matched));
        }

        return signatures.Count > 1;
    }

    private static TimestampParseFailure FirstFailure(
        IReadOnlyList<string> cells,
        TimestampFormat format)
    {
        foreach (var cell in cells)
        {
            if (!TimestampParser.TryParse(cell, format, out _, out var failure))
                return failure;
        }

        return TimestampParseFailure.None;
    }

    private static bool All(IReadOnlyList<string> cells, TimestampFormat format) =>
        cells.All(cell => TimestampParser.TryParse(cell, format, out _, out _));

    private static bool AllInWindow(IReadOnlyList<string> cells, TimestampFormat format)
    {
        foreach (var cell in cells)
        {
            if (!TimestampParser.TryParse(cell, format, out TelemetryTimestamp timestamp, out _))
                return false;

            if (timestamp.UnixNanoseconds < _windowStartNanoseconds ||
                timestamp.UnixNanoseconds >= _windowEndNanoseconds)
            {
                return false;
            }
        }

        return cells.Count > 0;
    }

    private static string Cell(IReadOnlyList<string> row, int columnIndex)
    {
        if (columnIndex < 0 || columnIndex >= row.Count)
        {
            throw new FormatException(
                $"Timestamp column index {columnIndex} is outside the CSV record.");
        }

        return row[columnIndex];
    }

    private static int NameScore(string name)
    {
        var key = Normalize(name);
        if (HeaderHints.ContainsKey(key))
            return 100;

        if (key.Contains("time", StringComparison.Ordinal) ||
            key.Contains("date", StringComparison.Ordinal) ||
            key.Contains("stamp", StringComparison.Ordinal) ||
            key.Contains("epoch", StringComparison.Ordinal))
        {
            return 40;
        }

        return 0;
    }

    private static TimestampFormat? HeaderHint(string name)
    {
        HeaderHints.TryGetValue(Normalize(name), out var hint);
        return hint;
    }

    private static string Normalize(string name)
    {
        var chars = name.Trim().ToLowerInvariant()
            .Where(c => c is not (' ' or '_' or '-' or '(' or ')'))
            .ToArray();
        return new string(chars);
    }

    private static readonly Dictionary<string, TimestampFormat?> HeaderHints =
        new(StringComparer.Ordinal)
        {
            ["timestamp"] = null,
            ["time"] = null,
            ["datetime"] = null,
            ["date"] = null,
            ["ts"] = null,
            ["t"] = null,
            ["utc"] = null,
            ["createdat"] = null,
            ["eventtime"] = null,
            ["loggedat"] = null,
            ["elapsed"] = TimestampFormat.RelativeElapsedSeconds,
            ["relative"] = TimestampFormat.RelativeElapsedSeconds,
            ["offset"] = TimestampFormat.RelativeElapsedSeconds,
            ["duration"] = TimestampFormat.RelativeElapsedSeconds,
            ["elapsedseconds"] = TimestampFormat.RelativeElapsedSeconds,
            ["timesec"] = TimestampFormat.RelativeElapsedSeconds,
            ["epoch"] = TimestampFormat.UnixEpochSeconds,
            ["unix"] = TimestampFormat.UnixEpochSeconds,
            ["unixtime"] = TimestampFormat.UnixEpochSeconds,
            ["epochseconds"] = TimestampFormat.UnixEpochSeconds,
            ["timestampms"] = TimestampFormat.UnixEpochMilliseconds,
            ["timems"] = TimestampFormat.UnixEpochMilliseconds,
            ["epochms"] = TimestampFormat.UnixEpochMilliseconds,
            ["unixms"] = TimestampFormat.UnixEpochMilliseconds,
            ["milliseconds"] = TimestampFormat.UnixEpochMilliseconds,
            ["millis"] = TimestampFormat.UnixEpochMilliseconds,
            ["timestampus"] = TimestampFormat.UnixEpochMicroseconds,
            ["timeus"] = TimestampFormat.UnixEpochMicroseconds,
            ["epochus"] = TimestampFormat.UnixEpochMicroseconds,
            ["unixus"] = TimestampFormat.UnixEpochMicroseconds,
            ["microseconds"] = TimestampFormat.UnixEpochMicroseconds,
            ["micros"] = TimestampFormat.UnixEpochMicroseconds
        };

    private sealed record ScoredColumn(
        int Index,
        string Name,
        TimestampFormat Format,
        bool FormatAmbiguous,
        int Score);
}

internal static class TimestampFormatList
{
    public static readonly TimestampFormat[] All =
    [
        TimestampFormat.Iso8601,
        TimestampFormat.UnixEpochSeconds,
        TimestampFormat.UnixEpochMilliseconds,
        TimestampFormat.UnixEpochMicroseconds,
        TimestampFormat.RelativeElapsedSeconds
    ];
}
