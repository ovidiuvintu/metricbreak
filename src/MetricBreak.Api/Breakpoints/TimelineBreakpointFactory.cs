using MetricBreak.Domain.Breakpoints;
using MetricBreak.Ingestion.Csv;

namespace MetricBreak.Api.Breakpoints;

internal static class TimelineBreakpointFactory
{
    public static TimelineBreakpoint Create(
        Guid id,
        string kind,
        string? name,
        RecordingTimeline? timeline)
    {
        if (!Enum.TryParse<TimelineMarkerKind>(kind, ignoreCase: true, out var markerKind) ||
            !Enum.IsDefined(markerKind))
        {
            throw new ArgumentException(
                "Timeline breakpoint kind must be Event or Stage.");
        }

        return Create(id, markerKind, name, timeline);
    }

    public static TimelineBreakpoint Create(
        Guid id,
        TimelineMarkerKind kind,
        string? name,
        RecordingTimeline? timeline)
    {
        var source = timeline ?? RecordingTimeline.Empty;
        var markers = kind == TimelineMarkerKind.Stage ? source.Stages : source.Events;
        var trimmed = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
        var selected = new Dictionary<long, string>();
        string? canonical = null;

        foreach (var item in markers)
        {
            if (trimmed is not null &&
                !string.Equals(item.Name, trimmed, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (item.SampleSequence < 0 || string.IsNullOrWhiteSpace(item.Name))
                continue;

            if (!selected.TryAdd(item.SampleSequence, item.Name.Trim()))
                continue;

            canonical ??= item.Name.Trim();
        }

        if (selected.Count == 0)
        {
            var label = kind == TimelineMarkerKind.Stage ? "stage" : "event";
            throw new ArgumentException(
                trimmed is null
                    ? $"This recording has no {label}s."
                    : $"No {label} named '{trimmed}'.");
        }

        return new TimelineBreakpoint(
            id,
            kind,
            trimmed is null ? null : canonical,
            selected);
    }
}
