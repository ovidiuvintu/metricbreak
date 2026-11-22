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

export function timelineEntryKey(entry: TimelineEntry): string {
  return `${entry.sampleSequence}:${entry.name}`;
}

/** Latest entry whose sample is at or before the cursor. */
export function timelineEntryAt(
  entries: readonly TimelineEntry[],
  sequence: number | null | undefined,
): TimelineEntry | null {
  if (sequence == null) return null;

  let active: TimelineEntry | null = null;
  for (const entry of entries) {
    if (entry.sampleSequence > sequence) continue;
    if (!active || entry.sampleSequence >= active.sampleSequence) {
      active = entry;
    }
  }
  return active;
}

/** Events from this stage's sample until the next stage starts. */
export function eventsInStage(
  events: readonly TimelineEntry[],
  stages: readonly TimelineEntry[],
  stage: TimelineEntry,
): TimelineEntry[] {
  const ordered = [...stages].sort((a, b) => a.sampleSequence - b.sampleSequence);
  const index = ordered.findIndex((item) => timelineEntryKey(item) === timelineEntryKey(stage));
  if (index < 0) return [];

  const end = ordered[index + 1]?.sampleSequence ?? Number.POSITIVE_INFINITY;
  return events.filter(
    (event) => event.sampleSequence >= stage.sampleSequence && event.sampleSequence < end,
  );
}