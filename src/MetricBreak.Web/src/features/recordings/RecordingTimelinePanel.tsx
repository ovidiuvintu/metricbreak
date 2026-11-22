import { useState } from "react";

import {
  List,
  ListItemButton,
  ListItemText,
  Stack,
  Tab,
  Tabs,
  Typography,
} from "@mui/material";

import type { RecordingTimeline } from "./recordingTimelineTypes";
import { timelineIsEmpty } from "./recordingTimelineTypes";

interface RecordingTimelinePanelProps {
  timeline: RecordingTimeline | null;
  loading?: boolean;
  onSeek: (sampleSequence: number) => void;
}

export function RecordingTimelinePanel({
  timeline,
  loading = false,
  onSeek,
}: RecordingTimelinePanelProps) {
  const [tab, setTab] = useState<"events" | "stages">("events");

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

  const items =
    tab === "events" ? timeline!.events : timeline!.stages;

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
          label={`Events (${timeline!.events.length})`}
        />
        <Tab
          value="stages"
          label={`Stages (${timeline!.stages.length})`}
        />
      </Tabs>

      <List
        dense
        disablePadding
        sx={{
          flex: 1,
          minHeight: 0,
          overflowY: "auto",
          overscrollBehavior: "contain",
        }}
      >
        {items.map((item) => (
          <ListItemButton
            key={`${item.sampleSequence}-${item.name}`}
            onClick={() => onSeek(item.sampleSequence)}
          >
            <ListItemText
              primary={item.name}
              secondary={
                item.description
                  ? `Sample ${item.sampleSequence.toLocaleString()} · ${item.description}`
                  : `Sample ${item.sampleSequence.toLocaleString()}`
              }
            />
          </ListItemButton>
        ))}
      </List>
    </Stack>
  );
}