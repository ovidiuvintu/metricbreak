using MetricBreak.Api.Investigations;

namespace MetricBreak.Api.Sessions;

public sealed record RecordingTimelineResponse(
    IReadOnlyList<TimelineEntrySnapshot> Events,
    IReadOnlyList<TimelineEntrySnapshot> Stages);