import { useEffect, useRef, useState } from "react";

import {
  Box,
  Button,
  List,
  ListItemButton,
  ListItemText,
  Stack,
  Tab,
  Tabs,
  Typography,
} from "@mui/material";

import {
  eventsInStage,
  timelineEntryAt,
  timelineEntryKey,
  timelineIsEmpty,
  type RecordingTimeline,
  type TimelineEntry,
} from "./recordingTimelineTypes";

interface RecordingTimelinePanelProps {
  timeline: RecordingTimeline | null;
  loading?: boolean;
  currentSequence?: number | null;
  onSeek: (sampleSequence: number) => void;
}

function scrollRowIntoView(container: HTMLElement, row: HTMLElement) {
  const containerRect = container.getBoundingClientRect();
  const rowRect = row.getBoundingClientRect();
  if (rowRect.top < containerRect.top) {
    container.scrollTop -= containerRect.top - rowRect.top;
  } else if (rowRect.bottom > containerRect.bottom) {
    container.scrollTop += rowRect.bottom - containerRect.bottom;
  }
}

export function RecordingTimelinePanel({
  timeline,
  loading = false,
  currentSequence = null,
  onSeek,
}: RecordingTimelinePanelProps) {
  const [tab, setTab] = useState<"events" | "stages">("events");
  const [stageFilterKey, setStageFilterKey] = useState<string | null>(null);
  const [stageFocus, setStageFocus] = useState<{ sequence: number | null; key: string } | null>(null);
  const listRef = useRef<HTMLUListElement>(null);
  const focusedStageKey = stageFocus?.sequence === currentSequence ? stageFocus.key : null;

  const stages = timeline?.stages ?? [];
  const events = timeline?.events ?? [];
  const currentEvent = timelineEntryAt(events, currentSequence);
  const currentStage = timelineEntryAt(stages, currentSequence);
  const filteredStage = stages.find((stage) => timelineEntryKey(stage) === stageFilterKey) ?? null;
  const visibleEvents = filteredStage ? eventsInStage(events, stages, filteredStage) : events;

  const scrollKey =
    tab === "stages"
      ? (focusedStageKey ?? (currentStage ? timelineEntryKey(currentStage) : null))
      : currentEvent && visibleEvents.some((event) => timelineEntryKey(event) === timelineEntryKey(currentEvent))
        ? timelineEntryKey(currentEvent)
        : null;

  useEffect(() => {
    const container = listRef.current;
    if (!container || !scrollKey) return;
    const row = container.querySelector<HTMLElement>(`[data-timeline-key="${CSS.escape(scrollKey)}"]`);
    if (row) scrollRowIntoView(container, row);
  }, [scrollKey, tab, stageFilterKey]);

  if (loading) {
    return (
      <Typography variant="body2" color="text.secondary">
        Loading timeline…
      </Typography>
    );
  }

  if (timelineIsEmpty(timeline)) {
    return (
      <Typography variant="body2" color="text.secondary">
        No events or stages were imported with this recording.
      </Typography>
    );
  }

  const openStage = (stage: TimelineEntry) => {
    setStageFocus({ sequence: currentSequence, key: timelineEntryKey(stage) });
    setTab("stages");
  };

  const openEventsInStage = (stage: TimelineEntry) => {
    setStageFilterKey(timelineEntryKey(stage));
    setTab("events");
  };

  const items = tab === "events" ? visibleEvents : stages;

  return (
    <Stack spacing={1} sx={{ flex: 1, minHeight: 0, overflow: "hidden" }}>
      <Tabs
        value={tab}
        onChange={(_, value: "events" | "stages") => setTab(value)}
        variant="fullWidth"
        sx={{ flexShrink: 0 }}
      >
        <Tab
          value="events"
          label={`Events (${events.length})`}
        />
        <Tab
          value="stages"
          label={`Stages (${stages.length})`}
        />
      </Tabs>

      {tab === "events" && filteredStage && (
        <Stack direction="row" spacing={1} sx={{ alignItems: "center", flexShrink: 0, px: 0.5 }}>
          <Typography variant="caption" color="text.secondary" sx={{ flex: 1 }}>
            {visibleEvents.length === 1 ? "1 event" : `${visibleEvents.length} events`} in {filteredStage.name}
          </Typography>
          <Button size="small" onClick={() => setStageFilterKey(null)}>
            Show all
          </Button>
        </Stack>
      )}

      <List
        ref={listRef}
        dense
        disablePadding
        sx={{
          flex: 1,
          minHeight: 0,
          overflowY: "auto",
          overscrollBehavior: "contain",
        }}
      >
        {items.length === 0 && (
          <Typography variant="body2" color="text.secondary" sx={{ px: 1, py: 1 }}>
            {filteredStage ? `No events in ${filteredStage.name}.` : "Nothing in this list."}
          </Typography>
        )}
        {items.map((item) => {
          const key = timelineEntryKey(item);
          const stage = tab === "events" ? timelineEntryAt(stages, item.sampleSequence) : null;
          const stageEvents = tab === "stages" ? eventsInStage(events, stages, item) : [];
          const isCurrentStage = tab === "stages" && currentStage != null && key === timelineEntryKey(currentStage);
          const selected = tab === "events" ? key === scrollKey : isCurrentStage;

          return (
            <ListItemButton
              key={key}
              data-timeline-key={key}
              selected={selected}
              onClick={() => onSeek(item.sampleSequence)}
            >
              <ListItemText
                primary={
                  isCurrentStage ? (
                    <Box component="span" sx={{ display: "inline-flex", alignItems: "center", gap: 0.75 }}>
                      {item.name}
                      <Typography component="span" variant="caption" color="primary.main" sx={{ fontWeight: 700 }}>
                        Current
                      </Typography>
                    </Box>
                  ) : item.name
                }
                secondary={
                  <TimelineSecondary
                    item={item}
                    stage={stage}
                    eventCount={tab === "stages" ? stageEvents.length : null}
                    onOpenStage={stage ? () => openStage(stage) : undefined}
                    onOpenEvents={tab === "stages" ? () => openEventsInStage(item) : undefined}
                  />
                }
              />
            </ListItemButton>
          );
        })}
      </List>
    </Stack>
  );
}

function TimelineSecondary({
  item,
  stage,
  eventCount,
  onOpenStage,
  onOpenEvents,
}: {
  item: TimelineEntry;
  stage: TimelineEntry | null;
  eventCount: number | null;
  onOpenStage?: () => void;
  onOpenEvents?: () => void;
}) {
  const eventLabel = eventCount === 1 ? "1 event" : `${eventCount ?? 0} events`;

  return (
    <>
      {`Sample ${item.sampleSequence.toLocaleString()}`}
      {onOpenStage && stage && (
        <>
          {" · "}
          <Box
            component="span"
            role="link"
            tabIndex={0}
            onClick={(event) => {
              event.stopPropagation();
              onOpenStage();
            }}
            onKeyDown={(event) => {
              if (event.key !== "Enter" && event.key !== " ") return;
              event.preventDefault();
              event.stopPropagation();
              onOpenStage();
            }}
            sx={{ color: "primary.main", cursor: "pointer", textDecoration: "underline" }}
          >
            {stage.name}
          </Box>
        </>
      )}
      {onOpenEvents && (
        <>
          {" · "}
          <Box
            component="span"
            role="link"
            tabIndex={0}
            onClick={(event) => {
              event.stopPropagation();
              onOpenEvents();
            }}
            onKeyDown={(event) => {
              if (event.key !== "Enter" && event.key !== " ") return;
              event.preventDefault();
              event.stopPropagation();
              onOpenEvents();
            }}
            sx={{ color: "primary.main", cursor: "pointer", textDecoration: "underline" }}
          >
            {eventLabel}
          </Box>
        </>
      )}
      {item.description ? ` · ${item.description}` : ""}
    </>
  );
}
