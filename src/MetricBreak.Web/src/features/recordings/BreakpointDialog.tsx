
import { useEffect, useMemo, useState } from "react";
import {
  Box,
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  FormControl,
  InputLabel,
  MenuItem,
  Select,
  Stack,
  TextField,
  Typography,
} from "@mui/material";
import { useTheme } from "@mui/material/styles";

import { getSeriesColor } from "../../theme/chartTheme";
import {
  formatChannelExtent,
  type ChannelExtent,
  type ChannelMetadata,
} from "./channelMetadata";
import type {
  ComparisonOperator,
  CreateBreakpointRequest,
  CreateMissingDataBreakpointRequest,
  CreateTimelineBreakpointRequest,
} from "./breakpointApi";
import type { RecordingTimeline, TimelineEntry } from "./recordingTimelineTypes";

export interface BreakpointDialogDraft {
  channelId?: string;
  operator?: ComparisonOperator;
  threshold?: number;
  caption?: string;
  timeline?: {
    kind: "Event" | "Stage";
    name?: string;
  };
}

interface BreakpointDialogProps {
  open: boolean;
  channels: ChannelMetadata[];
  /** Recording minimum and maximum for each numeric channel. */
  channelExtents?: Record<string, ChannelExtent>;
  /** Chart series colors keyed by channel id. Missing channels use the palette by index. */
  seriesColors?: Record<string, string>;
  busy: boolean;
  draft?: BreakpointDialogDraft | null;
  timeline?: RecordingTimeline | null;
  onClose: () => void;
  onCreate: (request: CreateBreakpointRequest) => Promise<void>;
  onCreateMissingData: (request: CreateMissingDataBreakpointRequest) => Promise<void>;
  onCreateTimeline: (request: CreateTimelineBreakpointRequest) => Promise<void>;
}

type BreakpointKind = "comparison" | "missing" | "timeline";

function uniqueMarkerNames(entries: readonly TimelineEntry[]): string[] {
  const seen = new Set<string>();
  const names: string[] = [];
  for (const entry of entries) {
    const key = entry.name.trim().toLowerCase();
    if (!key || seen.has(key)) continue;
    seen.add(key);
    names.push(entry.name.trim());
  }
  return names;
}

function ChannelOptionLabel({
  name,
  color,
  range,
}: {
  name: string;
  color: string;
  range?: string | null;
}) {
  return (
    <Box sx={{ display: "flex", alignItems: "center", gap: 1, minWidth: 0, width: "100%" }}>
      <Box
        aria-hidden
        sx={{
          width: 8,
          height: 8,
          borderRadius: "50%",
          flexShrink: 0,
          bgcolor: color,
        }}
      />
      <Box component="span" sx={{ minWidth: 0, flex: 1, overflow: "hidden", textOverflow: "ellipsis" }}>
        {name}
      </Box>
      {range && (
        <Box
          component="span"
          sx={{
            flexShrink: 0,
            color: "text.secondary",
            fontSize: "0.75rem",
            fontVariantNumeric: "tabular-nums",
          }}
        >
          {range}
        </Box>
      )}
    </Box>
  );
}

const operators: {
  value: ComparisonOperator;
  label: string;
}[] = [
  { value: "GreaterThan", label: ">" },
  { value: "GreaterThanOrEqual", label: ">=" },
  { value: "LessThan", label: "<" },
  { value: "LessThanOrEqual", label: "<=" },
  { value: "Equal", label: "==" },
  { value: "NotEqual", label: "!=" },
  { value: "Changed", label: "Changed" },
  { value: "RisingEdge", label: "Rising Edge" },
  { value: "FallingEdge", label: "Falling Edge" },
];

export function BreakpointDialog({
  open,
  channels,
  channelExtents = {},
  seriesColors = {},
  busy,
  draft,
  timeline = null,
  onClose,
  onCreate,
  onCreateMissingData,
  onCreateTimeline,
}: BreakpointDialogProps) {
  const theme = useTheme();
  const [kind, setKind] = useState<BreakpointKind>("comparison");
  const [channelId, setChannelId] = useState("");
  const [operator, setOperator] =
    useState<ComparisonOperator>("GreaterThan");
  const [threshold, setThreshold] = useState("");
  const [typedValue, setTypedValue] = useState("");
  const [missingSeconds, setMissingSeconds] = useState("5");
  const [missingChannelId, setMissingChannelId] = useState("recording");
  const [markerKind, setMarkerKind] = useState<"Event" | "Stage">("Event");
  const [markerName, setMarkerName] = useState("");

  useEffect(() => {
    if (!open) return;
    setKind(draft?.timeline ? "timeline" : "comparison");
    setChannelId(draft?.channelId ?? "");
    setOperator(draft?.operator ?? "GreaterThan");
    setThreshold(
      draft?.threshold !== undefined && Number.isFinite(draft.threshold)
        ? String(draft.threshold)
        : "",
    );
    setTypedValue("");
    setMissingSeconds("5");
    setMissingChannelId("recording");
    const events = timeline?.events ?? [];
    const stages = timeline?.stages ?? [];
    const preferredKind = draft?.timeline?.kind
      ?? (events.length > 0 || stages.length === 0 ? "Event" : "Stage");
    setMarkerKind(preferredKind);
    setMarkerName(draft?.timeline?.name?.trim() ?? "");
  }, [open, draft]);

  const seriesColorById = useMemo(() => {
    const colors: Record<string, string> = {};
    channels.forEach((channel, index) => {
      colors[channel.channelId] =
        seriesColors[channel.channelId] ?? getSeriesColor(theme.palette.mode, index);
    });
    return colors;
  }, [channels, seriesColors, theme.palette.mode]);
  const selectedType = channels.find(c => c.channelId === channelId)?.valueType ?? "Numeric";
  const allowedOperators = operators.filter(o => selectedType === "Numeric" ||
    (selectedType === "Boolean" && ["Equal", "NotEqual", "Changed", "RisingEdge", "FallingEdge"].includes(o.value)) ||
    (selectedType === "Categorical" && ["Equal", "NotEqual", "Changed"].includes(o.value)));
  const needsThreshold = selectedType === "Numeric" && operator !== "Changed";
  const numericThreshold = needsThreshold ? Number(threshold) : 0;

  const needsTypedValue = selectedType !== "Numeric" && ["Equal", "NotEqual"].includes(operator);
  const missingDuration = Number(missingSeconds);
  const validMissing =
    missingSeconds.trim() !== "" &&
    Number.isFinite(missingDuration) &&
    missingDuration > 0 &&
    (missingChannelId === "recording" || channels.some((channel) => channel.channelId === missingChannelId));
  const valid =
    channelId !== "" &&
    channels.some((channel) => channel.channelId === channelId) &&
    (!needsThreshold || threshold.trim() !== "") &&
    Number.isFinite(numericThreshold) &&
    (!needsTypedValue || (selectedType === "Boolean" ? ["true", "false"].includes(typedValue) : typedValue.length > 0));
  const markerEntries = markerKind === "Stage"
    ? (timeline?.stages ?? [])
    : (timeline?.events ?? []);
  const markerNames = uniqueMarkerNames(markerEntries);
  const markerNameOptions = markerName &&
    !markerNames.some((name) => name.toLowerCase() === markerName.toLowerCase())
    ? [markerName, ...markerNames]
    : markerNames;
  const validTimeline = markerEntries.length > 0;
  const markerNoun = markerKind === "Stage" ? "stage" : "event";

  const handleCreate = async () => {
    if (busy) return;

    if (kind === "missing") {
      if (!validMissing) return;
      await onCreateMissingData({
        minimumSeconds: missingDuration,
        ...(missingChannelId !== "recording" ? { channelId: missingChannelId } : {}),
      });
      return;
    }

    if (kind === "timeline") {
      if (!validTimeline) return;
      await onCreateTimeline({
        kind: markerKind,
        ...(markerName.trim() ? { name: markerName.trim() } : {}),
      });
      return;
    }

    if (!valid) return;

    await onCreate({
      channelId,
      operator,
      threshold: numericThreshold,
      ...(needsTypedValue ? { value: typedValue } : {}),
    });
  };

  return (
    <Dialog open={open} onClose={busy ? undefined : onClose}
      fullWidth maxWidth="sm">
      <DialogTitle>Add Breakpoint</DialogTitle>

      <DialogContent>
        <Stack spacing={2} sx={{ pt: 1 }}>
          {draft?.caption && (
            <Typography variant="caption" color="text.secondary">
              {draft.caption}
            </Typography>
          )}
          <FormControl fullWidth size="small">
            <InputLabel>Type</InputLabel>
            <Select
              label="Type"
              value={kind}
              onChange={(event) => setKind(event.target.value as BreakpointKind)}
            >
              <MenuItem value="comparison">Value comparison</MenuItem>
              <MenuItem value="missing">Missing data</MenuItem>
              <MenuItem value="timeline">Event or stage</MenuItem>
            </Select>
          </FormControl>
          {kind === "timeline" ? (
            <>
              <FormControl fullWidth size="small">
                <InputLabel>Marker</InputLabel>
                <Select
                  label="Marker"
                  value={markerKind}
                  onChange={(event) => {
                    setMarkerKind(event.target.value as "Event" | "Stage");
                    setMarkerName("");
                  }}
                >
                  <MenuItem value="Event">Event</MenuItem>
                  <MenuItem value="Stage">Stage</MenuItem>
                </Select>
              </FormControl>
              <FormControl fullWidth size="small" disabled={!validTimeline}>
                <InputLabel>Name</InputLabel>
                <Select
                  label="Name"
                  value={markerName}
                  onChange={(event) => setMarkerName(event.target.value)}
                >
                  <MenuItem value="">Any</MenuItem>
                  {markerNameOptions.map((name) => (
                    <MenuItem key={name} value={name}>{name}</MenuItem>
                  ))}
                </Select>
              </FormControl>
              <Typography variant="caption" color="text.secondary">
                {timeline == null
                  ? "Events and stages load with the recording."
                  : validTimeline
                    ? `Stops on the sample where this ${markerNoun} is recorded. Any stops on every ${markerNoun}. A stage stops at its start, not on the samples that follow.`
                    : `This recording has no ${markerNoun}s.`}
              </Typography>
            </>
          ) : kind === "missing" ? (
            <>
              <FormControl fullWidth size="small">
                <InputLabel>Scope</InputLabel>
                <Select
                  label="Scope"
                  value={missingChannelId}
                  onChange={(event) => setMissingChannelId(event.target.value)}
                >
                  <MenuItem value="recording">Recording time gap</MenuItem>
                  {channels.map((channel) => (
                    <MenuItem key={channel.channelId} value={channel.channelId}>
                      <ChannelOptionLabel
                        name={`${channel.name} empty`}
                        color={seriesColorById[channel.channelId]}
                        range={formatChannelExtent(channelExtents[channel.channelId])}
                      />
                    </MenuItem>
                  ))}
                </Select>
              </FormControl>
              <TextField
                label="Longer than (seconds)"
                type="number"
                size="small"
                value={missingSeconds}
                onChange={(event) => setMissingSeconds(event.target.value)}
                helperText="Stops when the gap is longer than this. A recording time gap is the clock jump between samples. A channel stops once that channel has stayed empty for longer than this."
                fullWidth
              />
            </>
          ) : (
          <>
          <FormControl fullWidth size="small">
            <InputLabel>Channel</InputLabel>
            <Select
              label="Channel"
              value={channelId}
              onChange={(event) => {
                setChannelId(event.target.value);
                const nextType = channels.find(c => c.channelId === event.target.value)?.valueType ?? "Numeric";
                if (nextType !== "Numeric") setOperator("Changed");
                setTypedValue("");
              }}
              renderValue={(selected) => {
                const channel = channels.find((item) => item.channelId === selected);
                if (!channel) return "";
                return (
                  <ChannelOptionLabel
                    name={channel.name}
                    color={seriesColorById[channel.channelId]}
                    range={formatChannelExtent(channelExtents[channel.channelId])}
                  />
                );
              }}
            >
              {channels.map((channel) => (
                <MenuItem
                  key={channel.channelId}
                  value={channel.channelId}
                  sx={{ width: "100%" }}
                >
                  <ChannelOptionLabel
                    name={channel.name}
                    color={seriesColorById[channel.channelId]}
                    range={formatChannelExtent(channelExtents[channel.channelId])}
                  />
                </MenuItem>
              ))}
            </Select>
          </FormControl>

          <FormControl fullWidth size="small">
            <InputLabel>Operator</InputLabel>
            <Select
              label="Operator"
              value={operator}
              onChange={(event) =>
                setOperator(event.target.value as ComparisonOperator)
              }
            >
              {allowedOperators.map((item) => (
                <MenuItem key={item.value} value={item.value}>
                  {item.label}
                </MenuItem>
              ))}
            </Select>
          </FormControl>

          {needsTypedValue && (selectedType === "Boolean" ?
            <FormControl fullWidth size="small"><InputLabel>Value</InputLabel>
              <Select label="Value" value={typedValue} onChange={event => setTypedValue(event.target.value)}>
                <MenuItem value="true">true</MenuItem><MenuItem value="false">false</MenuItem>
              </Select></FormControl> :
            <TextField label="Value" size="small" value={typedValue}
              onChange={event => setTypedValue(event.target.value)} fullWidth />)}
          {needsThreshold && <TextField
            label="Threshold"
            type="number"
            size="small"
            value={threshold}
            onChange={(event) => setThreshold(event.target.value)}
            fullWidth
          />}
          </>
          )}
        </Stack>
      </DialogContent>

      <DialogActions>
        <Button onClick={onClose} disabled={busy}>
          Cancel
        </Button>
        <Button
          variant="contained"
          onClick={handleCreate}
          disabled={(kind === "missing" ? !validMissing : kind === "timeline" ? !validTimeline : !valid) || busy}
        >
          Create Breakpoint
        </Button>
      </DialogActions>
    </Dialog>
  );
}
