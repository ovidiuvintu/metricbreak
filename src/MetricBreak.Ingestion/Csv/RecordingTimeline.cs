namespace MetricBreak.Ingestion.Csv;

public sealed record RecordingTimelineItem(
    long TimestampNanoseconds,
    long SampleSequence,
    string Name,
    string? Description);

public sealed class RecordingTimeline
{
    public static readonly RecordingTimeline Empty = new([], []);

    public RecordingTimeline(
        IReadOnlyList<RecordingTimelineItem> events,
        IReadOnlyList<RecordingTimelineItem> stages)
    {
        Events = events ?? Array.Empty<RecordingTimelineItem>();
        Stages = stages ?? Array.Empty<RecordingTimelineItem>();
    }

    public IReadOnlyList<RecordingTimelineItem> Events { get; }

    public IReadOnlyList<RecordingTimelineItem> Stages { get; }

    public bool IsEmpty => Events.Count == 0 && Stages.Count == 0;
}