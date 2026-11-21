export interface TimelineEntry {
  timestampNanoseconds: number;
  sampleSequence: number;
  name: string;
  description: string | null;
}

export interface RecordingTimeline {
  events: TimelineEntry[];
  stages: TimelineEntry[];
}

export function timelineIsEmpty(timeline: RecordingTimeline | null | undefined): boolean {
  if (!timeline) return true;
  return timeline.events.length === 0 && timeline.stages.length === 0;
}