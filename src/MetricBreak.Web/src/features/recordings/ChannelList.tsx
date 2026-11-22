
import { useState } from "react";

import {
  Box,
  IconButton,
  FormControlLabel,
  Paper,
  Switch,
  Tooltip,
  Typography,
} from "@mui/material";

import NavigateBeforeIcon from "@mui/icons-material/NavigateBefore";
import NavigateNextIcon from "@mui/icons-material/NavigateNext";

import type { CurrentSample } from "./sessionTypes";
import type { ChannelMetadata } from "./channelMetadata";
import type { SampleRangeItem } from "./sampleTypes";

interface ChannelListProps {
  sample?: CurrentSample | null;
  previousSample?: SampleRangeItem | null;
  channels: ChannelMetadata[];
  selectedChannelIds?: string[];
  navigationBusy?: boolean;
  onNextChange?: (channelId: string) => void;
  onPreviousChange?: (channelId: string) => void;
}

function formatChannelValue(
  sample: CurrentSample | SampleRangeItem | null | undefined,
  channelId: string,
): string {
  const typed = sample?.typedChannels?.[channelId];
  if (typed !== undefined) {
    if (typed.valueType === "Missing" || typed.value === null) return "N/A";
    switch (typed.valueType) {
      case "Numeric":
        return Number.isFinite(typed.value) ? typed.value.toLocaleString() : "N/A";
      case "Boolean":
        return typed.value ? "True" : "False";
      case "Categorical":
        return typed.value;
    }
  }
  const legacy = sample?.channels?.[channelId];
  return legacy == null || !Number.isFinite(legacy) ? "N/A" : legacy.toLocaleString();
}

// Compare original typed values, not formatted strings (which can round numbers).
function readChannelValue(
  sample: CurrentSample | SampleRangeItem,
  channelId: string,
): { type: string; value: string | number | boolean | null } {
  const typed = sample.typedChannels?.[channelId];
  if (typed) {
    if (typed.valueType === "Missing" || typed.value === null) {
      return { type: "Missing", value: null };
    }
    return { type: typed.valueType, value: typed.value };
  }
  const numeric = sample.channels?.[channelId];
  return numeric == null || !Number.isFinite(numeric)
    ? { type: "Missing", value: null }
    : { type: "Numeric", value: numeric };
}

function hasChannelChanged(
  current: CurrentSample | null | undefined,
  previous: SampleRangeItem | null | undefined,
  channelId: string,
): boolean {
  if (!current || !previous || current.sequence !== previous.sequence + 1) return false;
  const a = readChannelValue(current, channelId);
  const b = readChannelValue(previous, channelId);
  return a.type !== b.type || !Object.is(a.value, b.value);
}

export function ChannelList({
  sample,
  previousSample,
  channels,
  selectedChannelIds,
  navigationBusy = false,
  onNextChange,
  onPreviousChange,
}: ChannelListProps) {
  const [showAll, setShowAll] = useState(false);

  const selectedIds = new Set(selectedChannelIds ?? []);

  const visibleChannels =
    showAll || selectedChannelIds === undefined
      ? channels
      : channels.filter((channel) =>
          selectedIds.has(channel.channelId)
        );

  return (
    <Paper
      variant="outlined"
      sx={{
        width: "100%",
        minWidth: 0,
        overflow: "hidden",
        height: "100%",
        minHeight: 0,
        display: "flex",
        flexDirection: "column",
      }}
    >
      {/* Display options */}
      <Box
        sx={{
          px: 1,
          py: 0.5,
          display: "flex",
          alignItems: "center",
          justifyContent: "space-between",
          gap: 1,
          flexShrink: 0,
        }}
      >
        <FormControlLabel
          sx={{
            m: 0,
            "& .MuiFormControlLabel-label": {
              fontSize: "0.75rem",
            },
          }}
          control={
            <Switch
              size="small"
              checked={showAll}
              onChange={(_, checked) => setShowAll(checked)}
            />
          }
          label="Show all channels"
        />

        <Typography
          variant="caption"
          color="text.secondary"
          sx={{ flexShrink: 0 }}
        >
          {visibleChannels.length}/{channels.length}
        </Typography>
      </Box>
      {sample && previousSample && previousSample.sequence + 1 === sample.sequence && (
        <Typography
          variant="caption"
          color="text.secondary"
          sx={{ px: 1, pb: 0.5, display: "block" }}
        >
          A change shows the previous sample, then this one.
        </Typography>
      )}

      {/* Channel values. Name, value, and unit stay together; spare width stays at the end of the row. */}
      <Box
        sx={{
          width: "100%",
          overflowX: "hidden",
          overflowY: "auto",
          flex: 1,
          minHeight: 0,
        }}
      >
        {visibleChannels.map((channel) => {
          const displayValue = formatChannelValue(sample, channel.channelId);
          const changed = hasChannelChanged(sample, previousSample, channel.channelId);
          const previousValue = changed
            ? formatChannelValue(previousSample, channel.channelId)
            : null;
          const comparison = previousValue != null
            ? `${previousValue} → ${displayValue}`
            : displayValue;

          return (
            <Box
              key={channel.channelId}
              sx={{
                display: "flex",
                alignItems: "flex-start",
                gap: 0.75,
                minWidth: 0,
                px: 1,
                py: 0.5,
                "&:hover": { bgcolor: "action.hover" },
              }}
            >
              <Tooltip title={channel.name} arrow placement="left">
                <Typography
                  component="span"
                  variant="caption"
                  color="text.secondary"
                  noWrap
                  sx={{ flex: "0 1 auto", minWidth: 0 }}
                >
                  {channel.name}
                </Typography>
              </Tooltip>

              <Tooltip
                title={comparison === "N/A" ? "No value available" : comparison}
                arrow
              >
                <Box sx={{ flexShrink: 0, lineHeight: 1.2 }}>
                  <Typography
                    component="span"
                    variant="caption"
                    noWrap
                    sx={{
                      display: "block",
                      fontVariantNumeric: "tabular-nums",
                      color: changed ? "warning.main" : "text.primary",
                      fontWeight: changed ? 700 : 400,
                    }}
                  >
                    {displayValue}
                  </Typography>
                  {previousValue != null && (
                    <Typography
                      component="span"
                      variant="caption"
                      color="text.secondary"
                      noWrap
                      sx={{ display: "block", fontSize: "0.65rem", fontVariantNumeric: "tabular-nums" }}
                    >
                      was {previousValue}
                    </Typography>
                  )}
                </Box>
              </Tooltip>

              <Tooltip title={channel.unit ?? "No unit"} arrow>
                <Typography
                  component="span"
                  variant="caption"
                  color="text.secondary"
                  noWrap
                  sx={{ flexShrink: 0 }}
                >
                  {channel.unit ?? "—"}
                </Typography>
              </Tooltip>

              <Box sx={{ display: "flex", flexShrink: 0, ml: "auto" }}>
                <Tooltip title={`Previous change: ${channel.name}`}>
                  <span>
                    <IconButton
                      size="small"
                      sx={{ p: 0.25 }}
                      aria-label={`Previous change for ${channel.name}`}
                      disabled={navigationBusy || !onPreviousChange || !sample || sample.sequence <= 0}
                      onClick={() => onPreviousChange?.(channel.channelId)}
                    >
                      <NavigateBeforeIcon fontSize="small" />
                    </IconButton>
                  </span>
                </Tooltip>
                <Tooltip title={`Next change: ${channel.name}`}>
                  <span>
                    <IconButton
                      size="small"
                      sx={{ p: 0.25 }}
                      aria-label={`Next change for ${channel.name}`}
                      disabled={navigationBusy || !onNextChange || !sample}
                      onClick={() => onNextChange?.(channel.channelId)}
                    >
                      <NavigateNextIcon fontSize="small" />
                    </IconButton>
                  </span>
                </Tooltip>
              </Box>
            </Box>
          );
        })}

        {visibleChannels.length === 0 && (
          <Typography
            variant="caption"
            color="text.secondary"
            sx={{ display: "block", px: 1, py: 0.75 }}
          >
            No channels selected. Select channels
            in the Channels tab or enable
            "Show all channels".
          </Typography>
        )}
      </Box>
    </Paper>
  );
}