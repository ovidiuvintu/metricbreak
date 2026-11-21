using MetricBreak.Domain.Recordings;

namespace MetricBreak.Ingestion.Csv;

internal static class CsvAnnotationSectionReader
{
    public static RecordingTimeline TryRead(
        TextReader reader,
        TimestampFormat timestampFormat,
        IReadOnlyList<TelemetrySample> samples)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(samples);

        var header = ReadNextNonBlankRecord(reader);
        if (header is null || !TryMapColumns(header, out var columns))
            return RecordingTimeline.Empty;

        var events = new List<RecordingTimelineItem>();
        var stages = new List<RecordingTimelineItem>();

        while (true)
        {
            var fields = CsvRecordReader.ReadRecord(reader);
            if (fields is null)
                break;

            if (IsBlankRow(fields))
                continue;

            var recordNumber = events.Count + stages.Count + 2;
            var timestampText = FieldAt(fields, columns.TimestampIndex);
            if (string.IsNullOrWhiteSpace(timestampText))
                continue;

            var name = FieldAt(fields, columns.NameIndex);
            if (string.IsNullOrWhiteSpace(name))
                continue;

            var kind = FieldAt(fields, columns.TypeIndex);
            if (string.IsNullOrWhiteSpace(kind))
                continue;

            var timestamp = TimestampParser.Parse(
                timestampText,
                timestampFormat,
                recordNumber);

            var description = columns.DescriptionIndex >= 0
                ? NormalizeOptional(FieldAt(fields, columns.DescriptionIndex))
                : null;

            var item = new RecordingTimelineItem(
                timestamp.UnixNanoseconds,
                TimelineSampleIndex.ResolveSampleSequence(samples, timestamp),
                name.Trim(),
                description);

            if (IsStage(kind))
                stages.Add(item);
            else if (IsEvent(kind))
                events.Add(item);
        }

        if (events.Count == 0 && stages.Count == 0)
            return RecordingTimeline.Empty;

        return new RecordingTimeline(
            Sort(events),
            Sort(stages));
    }

    private static IReadOnlyList<RecordingTimelineItem> Sort(
        List<RecordingTimelineItem> items) =>
        items
            .OrderBy(item => item.SampleSequence)
            .ThenBy(item => item.TimestampNanoseconds)
            .ThenBy(item => item.Name, StringComparer.Ordinal)
            .ToArray();

    private static bool TryMapColumns(
        IReadOnlyList<string> header,
        out AnnotationColumns columns)
    {
        columns = default;
        if (header.Count < 2)
            return false;

        var timestamp = FindColumn(header, "timestamp", "time");
        var type = FindColumn(header, "type", "kind");
        var name = FindColumn(header, "name", "label", "title");

        if (timestamp < 0 || type < 0 || name < 0)
            return false;

        var description = FindColumn(
            header,
            "description",
            "desc",
            "note",
            "comment");

        columns = new AnnotationColumns(timestamp, type, name, description);
        return true;
    }

    private static int FindColumn(
        IReadOnlyList<string> header,
        params string[] aliases)
    {
        for (var index = 0; index < header.Count; index++)
        {
            var normalized = NormalizeHeader(header[index]);
            if (aliases.Any(alias =>
                    string.Equals(normalized, alias, StringComparison.Ordinal)))
            {
                return index;
            }
        }

        return -1;
    }

    private static string NormalizeHeader(string value) =>
        value.Trim().ToLowerInvariant();

    private static bool IsEvent(string kind) =>
        string.Equals(kind.Trim(), "event", StringComparison.OrdinalIgnoreCase);

    private static bool IsStage(string kind) =>
        string.Equals(kind.Trim(), "stage", StringComparison.OrdinalIgnoreCase);

    private static List<string>? ReadNextNonBlankRecord(TextReader reader)
    {
        while (true)
        {
            var fields = CsvRecordReader.ReadRecord(reader);
            if (fields is null)
                return null;

            if (!IsBlankRow(fields))
                return fields;
        }
    }

    private static bool IsBlankRow(IReadOnlyList<string> fields) =>
        fields.All(string.IsNullOrWhiteSpace);

    private static string FieldAt(IReadOnlyList<string> fields, int index) =>
        index >= 0 && index < fields.Count
            ? fields[index]
            : string.Empty;

    private static string? NormalizeOptional(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private readonly record struct AnnotationColumns(
        int TimestampIndex,
        int TypeIndex,
        int NameIndex,
        int DescriptionIndex);
}