using MetricBreak.Domain.Recordings;

namespace MetricBreak.Ingestion.Csv;

internal static class TimelineSampleIndex
{
    public static long ResolveSampleSequence(
        IReadOnlyList<TelemetrySample> samples,
        TelemetryTimestamp timestamp)
    {
        if (samples.Count == 0)
            return 0;

        var index = 0;
        while (index < samples.Count &&
               samples[index].Timestamp.CompareTo(timestamp) < 0)
        {
            index++;
        }

        if (index >= samples.Count)
            return samples.Count - 1;

        return index;
    }
}