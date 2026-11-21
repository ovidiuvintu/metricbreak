import type { TimelineEntry } from "./recordingTimelineTypes";

export type ChartTimelineEntry = Pick<
  TimelineEntry,
  "sampleSequence" | "name" | "description"
>;

export const TIMELINE_EVENTS_SERIES_ID = "__timeline_events__";

export function resolveActiveStage(
  stages: ChartTimelineEntry[],
  sampleSequence: number,
): ChartTimelineEntry | null {
  if (stages.length === 0) {
    return null;
  }

  let active: ChartTimelineEntry | null = null;
  for (const stage of stages) {
    if (stage.sampleSequence <= sampleSequence) {
      if (
        !active ||
        stage.sampleSequence >= active.sampleSequence
      ) {
        active = stage;
      }
    }
  }

  return active;
}

export function timelineEventsOnPage(
  events: ChartTimelineEntry[],
  pageStart: number,
  pageEnd: number,
): ChartTimelineEntry[] {
  return events.filter(
    (event) =>
      event.sampleSequence >= pageStart && event.sampleSequence < pageEnd,
  );
}

/** Places event dots slightly above the bottom of the visible value range. */
export function timelineEventMarkerY(dataMin: number, dataMax: number): number {
  if (!Number.isFinite(dataMin) || !Number.isFinite(dataMax)) return 0;
  const span = Math.max(
    dataMax - dataMin,
    Math.abs(dataMax) * 0.02,
    Math.abs(dataMin) * 0.02,
    1e-9,
  );
  return dataMin + span * 0.08;
}

export type TimelineEventScatterPoint = {
  value: [number, number];
  eventName: string;
  eventDescription: string | null;
};

export function buildTimelineEventScatterSeries(options: {
  events: ChartTimelineEntry[];
  markerY: number;
  yAxisIndex: 0 | 1;
  darkMode: boolean;
}): Record<string, unknown> | null {
  const { events, markerY, yAxisIndex, darkMode } = options;
  if (events.length === 0) return null;

  const dotColor = darkMode ? "#2DD4BF" : "#0D9488";
  const borderColor = darkMode ? "#5EEAD4" : "#115E59";

  const data: TimelineEventScatterPoint[] = events.map((event) => ({
    value: [event.sampleSequence, markerY],
    eventName: event.name,
    eventDescription: event.description,
  }));

  return {
    id: TIMELINE_EVENTS_SERIES_ID,
    name: TIMELINE_EVENTS_SERIES_ID,
    type: "scatter" as const,
    yAxisIndex,
    xAxisIndex: 0,
    data,
    symbol: "circle",
    symbolSize: 9,
    itemStyle: {
      color: dotColor,
      borderColor,
      borderWidth: 1,
    },
    emphasis: {
      scale: 1.45,
      itemStyle: {
        borderWidth: 2,
      },
    },
    cursor: "pointer",
    z: 8,
    legendHoverLink: false,
    tooltip: {
      show: true,
    },
  };
}

export function formatTimelineEventTooltip(
  param: {
    data?: TimelineEventScatterPoint;
  },
): string {
  const data = param.data;
  if (!data) return "";
  const lines = [
    data.eventName,
    `Sample ${data.value[0].toLocaleString()}`,
  ];
  const description = data.eventDescription?.trim();
  if (description) lines.push(description);
  return lines.join("\n");
}