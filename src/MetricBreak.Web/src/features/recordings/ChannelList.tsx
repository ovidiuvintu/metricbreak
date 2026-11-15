
import { useState } from "react";

import {
  Box,
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

import type { CurrentSample } from "./sessionTypes";
import type { ChannelMetadata } from "./channelMetadata";

interface ChannelListProps {
  sample?: CurrentSample | null;
  channels: ChannelMetadata[];
  selectedChannelIds?: string[];
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

function truncateChannelName(name: string): string {
  if (name.length <= MAX_CHANNEL_NAME_LENGTH) {
    return name;
  }

  return `${name.slice(0, MAX_CHANNEL_NAME_LENGTH - 3)}...`;
}

export function ChannelList({
  sample,
  channels,
  selectedChannelIds,
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
        }}
      >
        <Table
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
            <col style={{ width: "48%" }} />
            <col style={{ width: "32%" }} />
            <col style={{ width: "20%" }} />
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

              return (
                <TableRow key={channel.channelId} hover>
                  {/* Channel name */}
                  <TableCell>
                    <Tooltip
                      title={channel.name}
                      arrow
                      placement="left"
                    >
                      <Typography
                        variant="caption"
                        color="text.secondary"
                        sx={{
                          display: "block",
                          overflow: "hidden",
                          textOverflow: "ellipsis",
                          whiteSpace: "nowrap",
                          maxWidth: "100%",
                        }}
                      >
                        {truncateChannelName(channel.name)}
                      </Typography>
                    </Tooltip>
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
