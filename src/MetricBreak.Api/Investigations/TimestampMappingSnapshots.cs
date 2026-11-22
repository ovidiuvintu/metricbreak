using MetricBreak.Ingestion.Csv;

namespace MetricBreak.Api.Investigations;

internal static class TimestampMappingSnapshots
{
    public static TimestampMappingSnapshot? From(TimestampMapping? mapping) =>
        mapping is null
            ? null
            : new TimestampMappingSnapshot(mapping.ColumnIndex, mapping.Format.ToString());

    public static TimestampMapping? ToMapping(TimestampMappingSnapshot? snapshot)
    {
        if (snapshot is null || snapshot.ColumnIndex < 0 ||
            string.IsNullOrWhiteSpace(snapshot.Format))
        {
            return null;
        }

        if (!Enum.TryParse<TimestampFormat>(snapshot.Format, ignoreCase: true, out var format) ||
            !Enum.IsDefined(format))
        {
            return null;
        }

        return new TimestampMapping(snapshot.ColumnIndex, format);
    }
}
