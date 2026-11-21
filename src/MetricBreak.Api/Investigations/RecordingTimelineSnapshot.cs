using MetricBreak.Ingestion.Csv;

namespace MetricBreak.Api.Investigations;

public sealed record TimelineEntrySnapshot(
    long TimestampNanoseconds,
    long SampleSequence,
    string Name,
    string? Description);

public sealed record RecordingTimelineSnapshot(
    IReadOnlyList<TimelineEntrySnapshot> Events,
    IReadOnlyList<TimelineEntrySnapshot> Stages)
{
    public static readonly RecordingTimelineSnapshot Empty = new([], []);

    public bool IsEmpty => Events.Count == 0 && Stages.Count == 0;
}

internal static class RecordingTimelineMapping
{
    public static RecordingTimelineSnapshot ToSnapshot(RecordingTimeline? timeline)
    {
        if (timeline is null || timeline.IsEmpty)
            return RecordingTimelineSnapshot.Empty;

        return new RecordingTimelineSnapshot(
            timeline.Events.Select(ToEntry).ToArray(),
            timeline.Stages.Select(ToEntry).ToArray());
    }

    private static TimelineEntrySnapshot ToEntry(RecordingTimelineItem item) =>
        new(
            item.TimestampNanoseconds,
            item.SampleSequence,
            item.Name,
            item.Description);
}