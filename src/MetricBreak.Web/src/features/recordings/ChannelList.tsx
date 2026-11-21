
import { useState } from "react";

import {
  Box,
  IconButton,
  FormControlLabel,
  Paper,
  Switch,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
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

const MAX_CHANNEL_NAME_LENGTH = 16;

function formatChannelValue(sample: CurrentSample | null | undefined, channelId: string): string {
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

function truncateChannelName(name: string): string {
  if (name.length <= MAX_CHANNEL_NAME_LENGTH) {
    return name;
  }

  return `${name.slice(0, MAX_CHANNEL_NAME_LENGTH - 3)}...`;
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

      {/* Channel values */}
      <TableContainer
        sx={{
          width: "100%",
          overflowX: "hidden",
          overflowY: "auto",
          flex: 1,
          minHeight: 0,
        }}
      >
        <Table
          stickyHeader
          size="small"
          sx={{
            width: "100%",
            tableLayout: "fixed",
            "& .MuiTableCell-root": {
              px: 0.75,
              py: 0.75,
              fontSize: "0.72rem",
            },
          }}
        >
          <colgroup>
            <col style={{ width: "55%" }} />
            <col style={{ width: "28%" }} />
            <col style={{ width: "17%" }} />
          </colgroup>

          <TableHead>
            <TableRow>
              <TableCell>Channel</TableCell>
              <TableCell align="right">Value</TableCell>
              <TableCell>Unit</TableCell>
            </TableRow>
          </TableHead>

          <TableBody>
            {visibleChannels.map((channel) => {
              const displayValue = formatChannelValue(sample, channel.channelId);
              const changed = hasChannelChanged(sample, previousSample, channel.channelId);

              return (
                <TableRow key={channel.channelId} hover>
                  {/* Channel name and change navigation */}
                  <TableCell>
                    <Box sx={{ display: "flex", alignItems: "center", minWidth: 0, gap: 0.25 }}>
                      <Tooltip title={channel.name} arrow placement="left">
                        <Typography
                          variant="caption"
                          color="text.secondary"
                          sx={{ flex: 1, minWidth: 0, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}
                        >
                          {truncateChannelName(channel.name)}
                        </Typography>
                      </Tooltip>
                      <Box sx={{ display: "flex", flexShrink: 0 }}>
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
                  </TableCell>

                  {/* Current value */}
                  <TableCell align="right">
                    <Tooltip
                      title={displayValue === "N/A" ? "No value available" : displayValue}
                      arrow
                    >
                      <Typography
                        component="span"
                        variant="caption"
                        sx={{
                          display: "block",
                          overflow: "hidden",
                          textOverflow: "ellipsis",
                          whiteSpace: "nowrap",
                          fontVariantNumeric: "tabular-nums",
                          color: changed ? "warning.main" : "text.primary",
                          fontWeight: changed ? 700 : 400,
                        }}
                      >
                        {displayValue}
                      </Typography>
                    </Tooltip>
                  </TableCell>

                  {/* Unit */}
                  <TableCell>
                    <Tooltip
                      title={channel.unit ?? "No unit"}
                      arrow
                    >
                      <Typography
                        component="span"
                        variant="caption"
                        color="text.secondary"
                        sx={{
                          display: "block",
                          overflow: "hidden",
                          textOverflow: "ellipsis",
                          whiteSpace: "nowrap",
                        }}
                      >
                        {channel.unit ?? "—"}
                      </Typography>
                    </Tooltip>
                  </TableCell>
                </TableRow>
              );
            })}

            {visibleChannels.length === 0 && (
              <TableRow>
                <TableCell colSpan={3}>
                  <Typography
                    variant="caption"
                    color="text.secondary"
                  >
                    No channels selected. Select channels
                    in the Channels tab or enable
                    "Show all channels".
                  </Typography>
                </TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>
      </TableContainer>
    </Paper>
  );
}