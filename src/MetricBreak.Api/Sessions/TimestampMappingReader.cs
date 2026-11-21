using MetricBreak.Ingestion.Csv;

namespace MetricBreak.Api.Sessions;

internal static class TimestampMappingReader
{
    public static TimestampMapping? Read(int? columnIndex, string? format)
    {
        var hasColumn = columnIndex is not null;
        var hasFormat = !string.IsNullOrWhiteSpace(format);

        if (!hasColumn && !hasFormat)
            return null;

        if (columnIndex is not int column || string.IsNullOrWhiteSpace(format))
        {
            throw new ArgumentException(
                "Timestamp column and format must both be provided.");
        }

        if (column < 0)
        {
            throw new ArgumentException(
                "Timestamp column index cannot be negative.");
        }

        if (!Enum.TryParse<TimestampFormat>(format, ignoreCase: true, out var parsed) ||
            !Enum.IsDefined(parsed) ||
            int.TryParse(format, out _))
        {
            throw new ArgumentException(
                $"Unknown timestamp format '{format}'.");
        }

        return new TimestampMapping(column, parsed);
    }
}
